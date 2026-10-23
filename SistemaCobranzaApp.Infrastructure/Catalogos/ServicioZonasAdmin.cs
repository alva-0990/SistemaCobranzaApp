using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoZona
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ZonaDto? Zona { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoZona Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoZona NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoZona Ok(ZonaDto z) => new() { Exito = true, Zona = z };
}

public class ServicioZonasAdmin
{
    private const string Proceso = "ServicioZonasAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioZonasAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activas para los selectores/combos comunes)
    public async Task<List<ZonaDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Zonas
                .AsNoTracking()
                .Where(z => z.Estatus == Estatus.Activo)
                .OrderBy(z => z.Descripcion)
                .Select(z => new ZonaDto(z.IdZona, z.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las zonas activas.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<ZonaDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Zonas
                .AsNoTracking()
                .IgnoreQueryFilters() // Rompe el filtro global para el panel de administración
                .OrderBy(z => z.Descripcion)
                .Select(z => new ZonaDto(z.IdZona, z.Descripcion, z.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las zonas.");
            throw;
        }
    }

    // 3. Obtener por ID (Permite ver el detalle de zonas inactivas)
    public async Task<ZonaDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var z = await _db.Zonas
                .AsNoTracking()
                .IgnoreQueryFilters() // Permite al admin auditar o inspeccionar zonas inactivas
                .FirstOrDefaultAsync(x => x.IdZona == id, ct);

            return z is null ? null : new ZonaDto(z.IdZona, z.Descripcion, z.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la zona #{id}.");
            throw;
        }
    }

    public async Task<ResultadoZona> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoZona.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            // Buscamos ignorando filtros por si quieren recrear una zona que ya existía inactiva
            if (await _db.Zonas.IgnoreQueryFilters().AnyAsync(z => z.Descripcion == desc, ct))
                return ResultadoZona.Fallo("Ya existe una zona con esa descripción.");

            var zona = new Zona { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Zonas.Add(zona);
            await _db.SaveChangesAsync(ct);
            return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoZona.Fallo("Error en la base de datos al crear la zona.");
        }
    }

    // 4. Actualizar (Permite reactivar o corregir zonas inactivas)
    public async Task<ResultadoZona> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoZona.Fallo(error);

        try
        {
            var zona = await _db.Zonas.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdZona == id, ct);
            if (zona is null) return ResultadoZona.NoExiste($"No se encontró la zona #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Zonas.IgnoreQueryFilters().AnyAsync(z => z.Descripcion == desc && z.IdZona != id, ct))
                return ResultadoZona.Fallo("Ya existe otra zona con esa descripción.");

            zona.Descripcion = desc;
            zona.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, zona.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoZona.Fallo("Error en la base de datos al actualizar la zona.");
        }
    }

    // 5. Dar de Baja (Permite interactuar con el registro si ya estaba filtrado)
    public async Task<ResultadoZona> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var zona = await _db.Zonas.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdZona == id, ct);
            if (zona is null) return ResultadoZona.NoExiste($"No se encontró la zona #{id}.");

            zona.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoZona.Fallo("Error en la base de datos al dar de baja la zona.");
        }
    }
}
