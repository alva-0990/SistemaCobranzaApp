using System.Data;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Infrastructure.Clientes;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Ventas;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Sincronizacion;

public class ServicioJornada(
    CobranzaDbContext db, ServicioClientesAdmin clientes, ServicioProductosAdmin productos,
    ServicioVentasAdmin ventas, ServicioCobranza cobranza, ServicioInventario inventario,
    ServicioCondicionCreditoAdmin condiciones, ServicioFrecuenciasPagoAdmin frecuencias, ServicioMesesAdmin meses)
{
    private async Task<Usuario> UsuarioActivo(long id, CancellationToken ct)
        => await db.Usuarios.Include(u => u.Rol).AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdUsuario == id && u.Estatus == Estatus.Activo, ct)
            ?? throw new UnauthorizedAccessException("El usuario no está activo.");

    private async Task<RutaJornadaDto> Asignacion(long usuario, long ruta, CancellationToken ct)
    {
        var u = await UsuarioActivo(usuario, ct);
        var perfiles = u.IdRol == 3
            ? await db.Vendedores.AsNoTracking().Where(v => v.IdUsuario == usuario && v.Estatus == Estatus.Activo)
                .Select(v => new { Id = v.IdVendedor, v.IdZona }).ToListAsync(ct)
            : u.IdRol == 4
                ? await db.Cobradores.AsNoTracking().Where(c => c.IdUsuario == usuario && c.Estatus == Estatus.Activo)
                    .Select(c => new { Id = c.IdCobrador, c.IdZona }).ToListAsync(ct)
                : throw new UnauthorizedAccessException("La jornada requiere rol Vendedor o Cobrador.");
        if (perfiles.Count != 1 || perfiles[0].IdZona is not { } zona)
            throw new UnauthorizedAccessException("El usuario debe tener un único vendedor o cobrador activo con zona.");
        var z = await db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.IdZona == zona && z.Estatus == Estatus.Activo, ct)
            ?? throw new UnauthorizedAccessException("La zona del usuario no está activa.");
        if (ruta != 0 && !await db.Rutas.AnyAsync(r => r.IdRuta == ruta && r.IdZona == zona && r.Estatus == Estatus.Activo, ct))
            throw new UnauthorizedAccessException("La ruta no pertenece a la zona del usuario.");
        // IdRuta=0 representa toda la zona; se conserva el contrato anterior para las colas ya guardadas.
        return new(usuario, 0, z.Descripcion, zona, u.IdRol == 3 ? perfiles[0].Id : null,
            u.IdRol == 4 ? perfiles[0].Id : null);
    }

    public Task<RutaJornadaDto> ObtenerZonaAsync(long usuario, CancellationToken ct)
        => Asignacion(usuario, 0, ct);

    public async Task<List<RutaJornadaDto>> RutasAsync(long usuario, CancellationToken ct)
    {
        var a = await Asignacion(usuario, 0, ct);
        return await db.Rutas.AsNoTracking().Where(r => r.IdZona == a.IdZona && r.Estatus == Estatus.Activo)
            .Select(r => new RutaJornadaDto(usuario, r.IdRuta, r.Descripcion, a.IdZona, a.IdVendedor, a.IdCobrador)).ToListAsync(ct);
    }

    public async Task<JornadaDescargaDto> DescargarAsync(long usuario, long ruta, CancellationToken ct)
    {
        // La cartera, pagos e inventario pertenecen al mismo corte de la base.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var a = await Asignacion(usuario, ruta, ct);
        var cs = new List<ClienteDto>();
        var rutas = await RutasAsync(usuario, ct);
        foreach (var r in rutas)
            for (var pagina = 1; ; pagina++)
            {
                var lote = await clientes.ListarAsync(null, r.IdRuta, false, pagina, 200, ct);
                cs.AddRange(lote.Items);
                if (pagina * 200 >= lote.TotalItems) break;
            }
        var ids = cs.Select(c => c.IdCliente).ToList();
        var vs = new List<VentaDto>();
        var idsVentas = await db.Ventas.AsNoTracking().Where(v => ids.Contains(v.IdCliente)).Select(v => v.IdVenta).ToListAsync(ct);
        foreach (var id in idsVentas)
        {
            var v = await ventas.ObtenerAsync(id, ct);
            if (v is not null) vs.Add(v);
        }
        var ps = await db.Pagos.AsNoTracking().Where(p => ids.Contains(p.IdCliente))
            .Select(p => new PagoSyncDto(p.IdPago, p.PagoUuid, p.IdVenta, p.IdCliente, p.MontoAbona,
                p.SaldoAnterior, p.SaldoActual, p.FolioPago, p.ConsecutivoPago, p.IdCobrador, p.FechaPago, p.FechaModificacion)
                { IdMovimiento = p.IdMovimiento, Observaciones = p.Observaciones,
                  Cobrador = db.Cobradores.Where(c => c.IdCobrador == p.IdCobrador).Select(c => c.Nombre).FirstOrDefault(),
                  Movimiento = db.Movimientos.Where(m => m.IdMovimiento == p.IdMovimiento).Select(m => m.Descripcion).FirstOrDefault() })
            .ToListAsync(ct);
        var arts = new List<ProductoDto>();
        if (a.IdVendedor is not null)
            for (var pagina = 1; ; pagina++)
            {
                var lote = await productos.ListarAsync(null, false, pagina, 200, ct);
                arts.AddRange(lote.Items);
                if (arts.Count >= lote.Total) break;
            }
        var resultado = new JornadaDescargaDto(usuario, DateTimeOffset.UtcNow, a, cs, vs, ps, arts,
            a.IdVendedor is null ? new() : await inventario.ExistenciasZonaAsync(a.IdZona, ct),
            await condiciones.ListarActivasAsync(ct), await frecuencias.ListarActivasAsync(ct), await meses.ListarActivasAsync(ct))
        { EsZonaCompleta = true, Rutas = rutas,
            Clasificaciones = await db.Set<CatalogoClasificacion>().AsNoTracking().Where(c => c.Estatus == Estatus.Activo)
                .Select(c => new ClasificaionClienteDto(c.IdClasificacion, c.Descripcion, true)).ToListAsync(ct) };
        await tx.CommitAsync(ct);
        return resultado;
    }

    public async Task<ResultadoOperacionJornada> CrearClienteAsync(long usuario, ClienteJornadaRequest req, CancellationToken ct)
    {
        var a = await Asignacion(usuario, req.Cliente.IdRuta, ct);
        if (a.IdVendedor is null) throw new UnauthorizedAccessException("Solo los vendedores pueden dar de alta clientes en la jornada.");
        if (req.ClienteUuid == Guid.Empty) return new(false, false, "Falta el identificador del cliente.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var llave = BitConverter.ToInt64(req.ClienteUuid.ToByteArray(), 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({llave})", ct);
        var existente = await db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.ClienteUuid == req.ClienteUuid, ct);
        if (existente is not null)
        {
            if (existente.IdUsuarioRegistro != usuario || existente.IdRuta != req.Cliente.IdRuta) throw new UnauthorizedAccessException("El identificador pertenece a otra operación.");
            var dto = await clientes.ObtenerPorIdAsync(existente.IdCliente, ct);
            await tx.CommitAsync(ct);
            return new(true, true, null, Cliente: dto);
        }
        if (req.FechaCaptura > DateTimeOffset.UtcNow.AddMinutes(5)) return new(false, false, "Revisa el reloj del dispositivo.");
        var resultado = await clientes.CrearAsync(req.Cliente with { IdVendedor = a.IdVendedor, IdUsuarioRegistro = usuario }, ct);
        if (!resultado.Exito || resultado.Cliente is null) return new(false, false, resultado.Error);
        var entidad = await db.Clientes.FindAsync([resultado.Cliente.IdCliente], ct);
        entidad!.ClienteUuid = req.ClienteUuid;
        entidad.FechaAlta = req.FechaCaptura.ToUniversalTime();
        entidad.FechaModificacion = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var creado = await clientes.ObtenerPorIdAsync(entidad.IdCliente, ct);
        await tx.CommitAsync(ct);
        return new(true, false, null, Cliente: creado);
    }

    public async Task<ResultadoOperacionJornada> VenderAsync(long usuario, long ruta, VentaJornadaRequest req, CancellationToken ct)
    {
        var a = await Asignacion(usuario, ruta, ct);
        if (a.IdVendedor is null) throw new UnauthorizedAccessException("Este usuario no tiene permiso de venta.");
        if (req.VentaUuid == Guid.Empty) return new(false, false, "Falta el identificador de venta.");
        if (!await db.Clientes.AnyAsync(c => c.IdCliente == req.Venta.IdCliente && db.Rutas.Any(r => r.IdRuta == c.IdRuta && r.IdZona == a.IdZona && r.Estatus == Estatus.Activo) && c.Estatus == Estatus.Activo, ct))
            throw new UnauthorizedAccessException("El cliente no pertenece a la zona del usuario.");
        var existente = await db.Ventas.AsNoTracking().FirstOrDefaultAsync(v => v.VentaUuid == req.VentaUuid, ct);
        if (existente is not null)
        {
            if (existente.IdUsuarioRegistro != usuario || existente.IdCliente != req.Venta.IdCliente)
                throw new UnauthorizedAccessException("El identificador pertenece a otra operación.");
            return new(true, true, null, await ventas.ObtenerAsync(existente.IdVenta, ct));
        }
        if (req.FechaCaptura > DateTimeOffset.UtcNow.AddMinutes(5)) return new(false, false, "La fecha del dispositivo está adelantada. Revisa su reloj.");
        var solicitud = req.Venta with { IdVendedor = a.IdVendedor,
            VentaUuid = req.VentaUuid, FechaCaptura = req.FechaCaptura.ToUniversalTime(),
            FechaPrimerVence = req.Venta.FechaPrimerVence?.ToUniversalTime() };
        try
        {
            long? origen = req.IdVentaVieja;
            if (req.VentaViejaUuid is { } uuidOrigen)
            {
                origen = await db.Ventas.Where(v => v.VentaUuid == uuidOrigen && v.IdUsuarioRegistro == usuario)
                    .Select(v => (long?)v.IdVenta).SingleOrDefaultAsync(ct);
                if (origen is null) return new(false, false, "La cuenta anterior todavía no está sincronizada.");
            }
            var r = origen is > 0
                ? await ventas.RenovarVentaAsync(origen.Value, solicitud with { SaldoRenovacionEsperado = req.SaldoArrastre }, usuario, ct)
                : await ventas.CrearAsync(solicitud, usuario, ct);
            return new(r.Exito, false, r.Error, r.Venta);
        }
        catch (InvalidOperationException ex) { return new(false, false, ex.Message); }
    }

    public async Task<ResultadoOperacionJornada> CobrarAsync(long usuario, PagoJornadaRequest req, CancellationToken ct)
    {
        var a = await Asignacion(usuario, req.IdRuta, ct);
        if (a.IdCobrador is null) throw new UnauthorizedAccessException("Este usuario no tiene permiso de cobranza.");
        var idVenta = req.Pago.IdVenta;
        if (req.VentaLocalUuid is { } uuid)
            idVenta = await db.Ventas.Where(v => v.VentaUuid == uuid && v.IdUsuarioRegistro == usuario).Select(v => v.IdVenta).FirstOrDefaultAsync(ct);
        if (!await (from v in db.Ventas join c in db.Clientes on v.IdCliente equals c.IdCliente
                    where v.IdVenta == idVenta && db.Rutas.Any(r => r.IdRuta == c.IdRuta && r.IdZona == a.IdZona && r.Estatus == Estatus.Activo) select v.IdVenta).AnyAsync(ct))
            throw new UnauthorizedAccessException("La venta no pertenece a la zona del usuario.");
        // La jornada offline admite abonos (mov. 1) y el ajuste de condonación (mov. 5) que
        // acompaña a una liquidación por acuerdo. Ambos llegan como operaciones de pago positivas.
        if (req.Pago.PagoUuid == Guid.Empty || req.Pago.MontoAbona <= 0
            || req.Pago.IdMovimiento is not (null or MovimientoIds.Abono or MovimientoIds.Ajuste))
            return new(false, false, "Solo se admiten abonos o ajustes de liquidación positivos en la jornada offline.");
        var existente = await db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.PagoUuid == req.Pago.PagoUuid, ct);
        if (existente is not null && (existente.IdUsuarioRegistro != usuario || existente.IdVenta != idVenta))
            throw new UnauthorizedAccessException("El identificador pertenece a otro pago.");
        var r = await cobranza.RegistrarPagoAsync(req.Pago with { IdVenta = idVenta, IdCobrador = a.IdCobrador,
            IdUsuarioRegistro = usuario, FechaPago = req.Pago.FechaPago?.ToUniversalTime() }, ct);
        return new(r.Exito, existente is not null, r.Error, Pago: r.Respuesta);
    }
}
