using SistemaCobranzaApp.Domain.Comun;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.CatalogoComisiones;

public class ServicioPagosComisionesAdmin
{
    private const string Proceso = "ServicioPagosComisionesAdmin (BD)";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioPagosComisionesAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. LISTAR LAS QUE ESTÁN PENDIENTES DE LIBERAR (PESTAÑA 1)
    public async Task<List<ComisionPendientePagoDto>> ListarPendientesLiberarAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        try
        {
            var query = from c in _db.ComisionesPorPagar.AsNoTracking()
                        join v in _db.Ventas.AsNoTracking() on c.IdVenta equals v.IdVenta
                        join dv in _db.DetallesVenta.AsNoTracking() on c.IdDetalle equals dv.IdDetalle
                        join p in _db.Productos.AsNoTracking() on dv.IdProducto equals p.IdProducto
                        join vend in _db.Vendedores.AsNoTracking() on c.IdVendedor equals vend.IdVendedor
                        join z in _db.Zonas.AsNoTracking() on vend.IdZona equals z.IdZona
                        where c.EstatusComision == "PENDIENTE"
                        select new { c, v, p, vend, z };

            if (filtros.IdVendedor.HasValue) query = query.Where(x => x.c.IdVendedor == filtros.IdVendedor.Value);
            if (filtros.IdZona.HasValue) query = query.Where(x => x.vend.IdZona == filtros.IdZona.Value);
            if (filtros.FechaDesde.HasValue) query = query.Where(x => x.c.FechaCalculo >= FechaProyecto.InicioDiaUtc(filtros.FechaDesde.Value));
            if (filtros.FechaHasta.HasValue) query = query.Where(x => x.c.FechaCalculo <= FechaProyecto.FinDiaUtc(filtros.FechaHasta.Value));

            return await query.OrderByDescending(x => x.c.FechaCalculo)
                .Select(x => new ComisionPendientePagoDto(
                    x.c.IdComision, x.c.IdVenta, x.v.CodigoVenta ?? "SIN FOLIO", x.p.Descripcion,
                    x.c.IdVendedor, x.vend.Nombre, x.z.Descripcion, x.c.MontoComision,
                    x.c.EstatusComision, x.c.FechaCalculo
                )).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: filtros);
            throw;
        }
    }

    // 2. LISTAR LAS QUE ESTÁN DISPONIBLES EN CAJA (PESTAÑA 2)
    public async Task<List<ComisionPendientePagoDto>> ListarDisponiblesAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        try
        {
            var query = from c in _db.ComisionesPorPagar.AsNoTracking()
                        join v in _db.Ventas.AsNoTracking() on c.IdVenta equals v.IdVenta
                        join dv in _db.DetallesVenta.AsNoTracking() on c.IdDetalle equals dv.IdDetalle
                        join p in _db.Productos.AsNoTracking() on dv.IdProducto equals p.IdProducto
                        join vend in _db.Vendedores.AsNoTracking() on c.IdVendedor equals vend.IdVendedor
                        join z in _db.Zonas.AsNoTracking() on vend.IdZona equals z.IdZona
                        where c.EstatusComision == "DISPONIBLE"
                        select new { c, v, p, vend, z };

            if (filtros.IdVendedor.HasValue) query = query.Where(x => x.c.IdVendedor == filtros.IdVendedor.Value);
            if (filtros.IdZona.HasValue) query = query.Where(x => x.vend.IdZona == filtros.IdZona.Value);
            if (filtros.FechaDesde.HasValue) query = query.Where(x => x.c.FechaCalculo >= FechaProyecto.InicioDiaUtc(filtros.FechaDesde.Value));
            if (filtros.FechaHasta.HasValue) query = query.Where(x => x.c.FechaCalculo <= FechaProyecto.FinDiaUtc(filtros.FechaHasta.Value));

            return await query.OrderByDescending(x => x.c.FechaCalculo)
                .Select(x => new ComisionPendientePagoDto(
                    x.c.IdComision, x.c.IdVenta, x.v.CodigoVenta ?? "SIN FOLIO", x.p.Descripcion,
                    x.c.IdVendedor, x.vend.Nombre, x.z.Descripcion, x.c.MontoComision,
                    x.c.EstatusComision, x.c.FechaCalculo
                )).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: filtros);
            throw;
        }
    }

    // 3. LISTAR BITÁCORA DE HISTORIAL PAGADAS (PESTAÑA 3)
    public async Task<List<ComisionPendientePagoDto>> ListarHistorialAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        try
        {
            var query = from c in _db.ComisionesPorPagar.AsNoTracking()
                        join v in _db.Ventas.AsNoTracking() on c.IdVenta equals v.IdVenta
                        join dv in _db.DetallesVenta.AsNoTracking() on c.IdDetalle equals dv.IdDetalle
                        join p in _db.Productos.AsNoTracking() on dv.IdProducto equals p.IdProducto
                        join vend in _db.Vendedores.AsNoTracking() on c.IdVendedor equals vend.IdVendedor
                        join z in _db.Zonas.AsNoTracking() on vend.IdZona equals z.IdZona
                        where c.EstatusComision == "PAGADA"
                        select new { c, v, p, vend, z };

            if (filtros.IdVendedor.HasValue) query = query.Where(x => x.c.IdVendedor == filtros.IdVendedor.Value);
            if (filtros.IdZona.HasValue) query = query.Where(x => x.vend.IdZona == filtros.IdZona.Value);
            if (filtros.FechaDesde.HasValue) query = query.Where(x => x.c.FechaCalculo >= FechaProyecto.InicioDiaUtc(filtros.FechaDesde.Value));
            if (filtros.FechaHasta.HasValue) query = query.Where(x => x.c.FechaCalculo <= FechaProyecto.FinDiaUtc(filtros.FechaHasta.Value));

            return await query.OrderByDescending(x => x.c.FechaPago)
                .Select(x => new ComisionPendientePagoDto(
                    x.c.IdComision, x.c.IdVenta, x.v.CodigoVenta ?? "SIN FOLIO", x.p.Descripcion,
                    x.c.IdVendedor, x.vend.Nombre, x.z.Descripcion, x.c.MontoComision,
                    x.c.EstatusComision, x.c.FechaPago ?? x.c.FechaCalculo
                )).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: filtros);
            throw;
        }
    }

    // 4. PROCESAR LA LIBERACIÓN MANUAL (PENDIENTE -> DISPONIBLE)
    public async Task<ResultadoPagoComision> LiberarComisionesManualAsync(ProcesarPagoComisionesRequest req, CancellationToken ct = default)
    {
        if (req.IdsComisiones is null || !req.IdsComisiones.Any())
            return ResultadoPagoComision.Fallo("Debe seleccionar al menos una comisión para liberar.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var comisiones = await _db.ComisionesPorPagar
                .Where(c => req.IdsComisiones.Contains(c.IdComision) && c.EstatusComision == "PENDIENTE")
                .ToListAsync(ct);

            if (!comisiones.Any())
                return ResultadoPagoComision.Fallo("No se encontraron comisiones pendientes válidas para liberar.");

            foreach (var comision in comisiones)
            {
                comision.EstatusComision = "DISPONIBLE";
                _db.ComisionesPorPagar.Update(comision);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoPagoComision.Ok(comisiones.Count);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error(Proceso, ex, datos: req);
            return ResultadoPagoComision.Fallo("Error al liberar las comisiones en la base de datos.");
        }
    }

    // 5. LIQUIDAR DE FORMA MANUAL UN BLOQUE DE COMISIONES SELECCIONADAS (DISPONIBLE -> PAGADA)
    public async Task<ResultadoPagoComision> PagarComisionesAsync(ProcesarPagoComisionesRequest req, long? idUsuarioPago = null, CancellationToken ct = default)
    {
        if (req.IdsComisiones is null || !req.IdsComisiones.Any())
            return ResultadoPagoComision.Fallo("Debe seleccionar al menos una comisión para pagar.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var comisiones = await _db.ComisionesPorPagar
                .Where(c => req.IdsComisiones.Contains(c.IdComision) && c.EstatusComision == "DISPONIBLE")
                .ToListAsync(ct);

            if (!comisiones.Any())
                return ResultadoPagoComision.Fallo("Ninguna de las comisiones seleccionadas califica para pago o ya fueron procesadas.");

            var ahora = DateTimeOffset.UtcNow;

            foreach (var comision in comisiones)
            {
                comision.EstatusComision = "PAGADA";
                comision.FechaPago = ahora;
                comision.IdUsuarioPago = idUsuarioPago;
                
                _db.ComisionesPorPagar.Update(comision);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _log.Info(Proceso, $"Pago manual exitoso para {comisiones.Count} comisiones.", datos: req.IdsComisiones);
            return ResultadoPagoComision.Ok(comisiones.Count);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error(Proceso, ex, datos: new { Request = req, Usuario = idUsuarioPago });
            return ResultadoPagoComision.Fallo("Error en la base de datos al asentar el pago manual de comisiones.");
        }
    }
}