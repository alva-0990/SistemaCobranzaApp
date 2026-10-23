using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoRuta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public RutaAdminDto? Ruta { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoRuta Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoRuta NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoRuta Ok(RutaAdminDto r) => new() { Exito = true, Ruta = r };
}

public class ServicioRutasAdmin
{
    private const string Proceso = "ServicioRutasAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioRutasAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // Listado operativo (solo activas) para selectores/combos comunes.
    public async Task<List<RutaAdminDto>> ListarActivasAsync(long? idZona, CancellationToken ct = default)
    {
        try
        {
            var q = from r in _db.Rutas.AsNoTracking()
                    where r.Estatus == Estatus.Activo
                    join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                    from z in zj.DefaultIfEmpty()
                    select new { r, z };

            if (idZona is { } zona) q = q.Where(x => x.r.IdZona == zona);

            return await q.OrderBy(x => x.r.Descripcion)
                .Select(x => new RutaAdminDto(x.r.IdRuta, x.r.Descripcion, x.r.IdZona,
                    x.z != null ? x.z.Descripcion : null, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener las rutas activas.");
            throw;
        }
    }

    public async Task<List<RutaAdminDto>> ListarAsync(bool? incluirInactivas, long? idZona, CancellationToken ct = default)
    {
        try
        {
            var incluir = incluirInactivas ?? false;
            var q = from r in _db.Rutas.AsNoTracking()
                    join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                    from z in zj.DefaultIfEmpty()
                    select new { r, z };

            if (!incluir) q = q.Where(x => x.r.Estatus == Estatus.Activo);
            if (idZona is { } zona) q = q.Where(x => x.r.IdZona == zona);

            return await q.OrderBy(x => x.r.Descripcion)
                .Select(x => new RutaAdminDto(x.r.IdRuta, x.r.Descripcion, x.r.IdZona,
                    x.z != null ? x.z.Descripcion : null, x.r.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { incluirInactivas, idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener las rutas.");
            throw;
        }
    }

    public async Task<RutaAdminDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var fila = await (from r in _db.Rutas.AsNoTracking()
                              where r.IdRuta == id
                              join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                              from z in zj.DefaultIfEmpty()
                              select new { r, z }).FirstOrDefaultAsync(ct);
            return fila is null ? null
                : new RutaAdminDto(fila.r.IdRuta, fila.r.Descripcion, fila.r.IdZona,
                    fila.z?.Descripcion, fila.r.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la ruta #{id}.");
            throw;
        }
    }

    public async Task<ResultadoRuta> CrearAsync(string descripcion, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoRuta.Fallo(error);

        try
        {
            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoRuta.Fallo($"La zona #{z} no existe.");

            var ruta = new Ruta { Descripcion = descripcion.Trim(), IdZona = idZona, Estatus = Estatus.Activo };
            _db.Rutas.Add(ruta);
            await _db.SaveChangesAsync(ct);
            return ResultadoRuta.Ok((await ObtenerAsync(ruta.IdRuta, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion, idZona });
            return ResultadoRuta.Fallo("Error en la base de datos al crear la ruta.");
        }
    }

    public async Task<ResultadoRuta> ActualizarAsync(long id, string descripcion, long? idZona, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoRuta.Fallo(error);

        try
        {
            var ruta = await _db.Rutas.FirstOrDefaultAsync(r => r.IdRuta == id, ct);
            if (ruta is null) return ResultadoRuta.NoExiste($"No se encontró la ruta #{id}.");

            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoRuta.Fallo($"La zona #{z} no existe.");

            ruta.Descripcion = descripcion.Trim();
            ruta.IdZona = idZona;
            ruta.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoRuta.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion, idZona, activo });
            return ResultadoRuta.Fallo("Error en la base de datos al actualizar la ruta.");
        }
    }

    public async Task<ResultadoRuta> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var ruta = await _db.Rutas.FirstOrDefaultAsync(r => r.IdRuta == id, ct);
            if (ruta is null) return ResultadoRuta.NoExiste($"No se encontró la ruta #{id}.");

            ruta.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoRuta.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoRuta.Fallo("Error en la base de datos al dar de baja la ruta.");
        }
    }
}
