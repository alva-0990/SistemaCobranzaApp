using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Clientes;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Clientes;

public class ResultadoCliente
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ClienteDto? Cliente { get; init; }

    public static ResultadoCliente Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoCliente Ok(ClienteDto c) => new() { Exito = true, Cliente = c };
}

/// <summary>
/// CRUD de administración de clientes con validación de datos y de FKs.
/// La baja es lógica (Estatus = Inactivo), coherente con el resto del sistema.
/// </summary>
public class ServicioClientesAdmin
{
    private readonly CobranzaDbContext _db;

    public ServicioClientesAdmin(CobranzaDbContext db)
    {
        _db = db;
    }

    public async Task<ClientesPaginaResponse> ListarAsync(
        string? texto, long? idRuta, bool incluirInactivos,
        int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = pagina < 1 ? 1 : pagina;
        tamanoPagina = tamanoPagina is < 1 or > 200 ? 50 : tamanoPagina;

        var q = from c in _db.Clientes.AsNoTracking()
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                select new { c, r };

        if (!incluirInactivos)
            q = q.Where(x => x.c.Estatus == Estatus.Activo);

        if (idRuta is { } ruta)
            q = q.Where(x => x.c.IdRuta == ruta);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var like = BusquedaHelper.ParaLike(texto);
            q = q.Where(x =>
                EF.Functions.ILike(x.c.Nombre, like)
                || EF.Functions.ILike(x.c.ApPaterno ?? "", like)
                || EF.Functions.ILike(x.c.ApMaterno ?? "", like));
        }

        var total = await q.CountAsync(ct);

        var filas = await q
            .OrderBy(x => x.c.Nombre).ThenBy(x => x.c.ApPaterno).ThenBy(x => x.c.IdCliente)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = filas.Select(x => Mapear(x.c, x.r)).ToList();
        return new ClientesPaginaResponse(items, total, pagina, tamanoPagina);
    }

    public async Task<ClienteDto?> ObtenerPorIdAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from c in _db.Clientes.AsNoTracking()
                          where c.IdCliente == id
                          join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                          from r in rj.DefaultIfEmpty()
                          select new { c, r }).FirstOrDefaultAsync(ct);

        return fila is null ? null : Mapear(fila.c, fila.r);
    }

    public async Task<ResultadoCliente> CrearAsync(CrearClienteRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCliente.Validar(req.Nombre, req.IdRuta, req.IdVendedor, req.IdClasificacion);
        if (error is not null)
            return ResultadoCliente.Fallo(error);

        var errorFk = await ValidarFksAsync(req.IdRuta, req.IdVendedor, req.IdClasificacion, ct);
        if (errorFk is not null)
            return ResultadoCliente.Fallo(errorFk);

        var ahora = DateTimeOffset.UtcNow;
        var cliente = new Cliente
        {
            Nombre = req.Nombre.Trim(),
            ApPaterno = Limpiar(req.ApPaterno),
            ApMaterno = Limpiar(req.ApMaterno),
            IdRuta = req.IdRuta,
            IdVendedor = req.IdVendedor,
            IdClasificacion = req.IdClasificacion,
            Observaciones = Limpiar(req.Observaciones),
            IdUsuarioRegistro = req.IdUsuarioRegistro,
            Estatus = Estatus.Activo,
            FechaAlta = ahora,
            FechaModificacion = ahora
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(cliente.IdCliente, ct);
        return ResultadoCliente.Ok(dto!);
    }

    public async Task<ResultadoCliente> ActualizarAsync(long id, ActualizarClienteRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCliente.Validar(req.Nombre, req.IdRuta, req.IdVendedor, req.IdClasificacion);
        if (error is not null)
            return ResultadoCliente.Fallo(error);

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return ResultadoCliente.Fallo($"No se encontró el cliente #{id}.");

        var errorFk = await ValidarFksAsync(req.IdRuta, req.IdVendedor, req.IdClasificacion, ct);
        if (errorFk is not null)
            return ResultadoCliente.Fallo(errorFk);

        cliente.Nombre = req.Nombre.Trim();
        cliente.ApPaterno = Limpiar(req.ApPaterno);
        cliente.ApMaterno = Limpiar(req.ApMaterno);
        cliente.IdRuta = req.IdRuta;
        cliente.IdVendedor = req.IdVendedor;
        cliente.IdClasificacion = req.IdClasificacion;
        cliente.Observaciones = Limpiar(req.Observaciones);
        cliente.FechaModificacion = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(id, ct);
        return ResultadoCliente.Ok(dto!);
    }

    /// <summary>Baja lógica: marca el cliente como Inactivo.</summary>
    public async Task<ResultadoCliente> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return ResultadoCliente.Fallo($"No se encontró el cliente #{id}.");

        cliente.Estatus = Estatus.Inactivo;
        cliente.FechaModificacion = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(id, ct);
        return ResultadoCliente.Ok(dto!);
    }

    // ---------------- helpers ----------------

    private async Task<string?> ValidarFksAsync(long idRuta, long? idVendedor, long? idClasificacion, CancellationToken ct)
    {
        var ruta = await _db.Rutas.AsNoTracking().FirstOrDefaultAsync(r => r.IdRuta == idRuta, ct);
        if (ruta is null)
            return $"La ruta #{idRuta} no existe.";

        if (idVendedor is { } v)
        {
            var vendedor = await _db.Vendedores.AsNoTracking().FirstOrDefaultAsync(x => x.IdVendedor == v, ct);
            if (vendedor is null)
                return $"El vendedor #{v} no existe.";

            if (ruta.IdZona is not null && vendedor.IdZona != ruta.IdZona)
                return "El vendedor no pertenece a la zona de la ruta seleccionada.";
        }

        if (idClasificacion is { } c && !await _db.Clasificaciones.AnyAsync(x => x.IdClasificacion == c, ct))
            return $"La clasificación #{c} no existe.";

        return null;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ClienteDto Mapear(Cliente c, Ruta? r) => new(
        c.IdCliente, c.Nombre, c.ApPaterno, c.ApMaterno,
        BusquedaHelper.NombreCompleto(c.Nombre, c.ApPaterno, c.ApMaterno),
        c.IdRuta, r?.Descripcion, r?.IdZona,
        c.IdVendedor, c.IdClasificacion, c.Observaciones);
}
