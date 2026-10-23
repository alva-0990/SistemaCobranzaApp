using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoClasificacionCliente
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ClasificaionClienteDto? ClasificacionCliente { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoClasificacionCliente Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoClasificacionCliente NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoClasificacionCliente Ok(ClasificaionClienteDto z) => new() { Exito = true, ClasificacionCliente = z };
}

public class ServicioClasificacionClienteAdmin
{
    private const string Proceso = "ServicioClasificacionClienteAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioClasificacionClienteAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activas para los selectores/combos comunes)
    public async Task<List<ClasificaionClienteDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Clasificaciones
                .AsNoTracking()
                .Where(z => z.Estatus == Estatus.Activo)
                .OrderBy(z => z.Descripcion)
                .Select(z => new ClasificaionClienteDto(z.IdClasificacion, z.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener clasificaciones cliente activas.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<ClasificaionClienteDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Clasificaciones
                .AsNoTracking()
                .IgnoreQueryFilters() // Rompe el filtro global para el panel de administración
                .OrderBy(z => z.Descripcion)
                .Select(z => new ClasificaionClienteDto(z.IdClasificacion, z.Descripcion, z.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las clasificaciones cliente.");
            throw;
        }
    }

    // 3. Obtener por ID (Permite ver el detalle de zonas inactivas)
    public async Task<ClasificaionClienteDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var z = await _db.Clasificaciones
                .AsNoTracking()
                .IgnoreQueryFilters() // Permite al admin auditar o inspeccionar zonas inactivas
                .FirstOrDefaultAsync(x => x.IdClasificacion == id, ct);

            return z is null ? null : new ClasificaionClienteDto(z.IdClasificacion, z.Descripcion, z.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la clasificaciones cliente #{id}.");
            throw;
        }
    }

    public async Task<ResultadoClasificacionCliente> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoClasificacionCliente.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            // Buscamos ignorando filtros por si quieren recrear una zona que ya existía inactiva
            if (await _db.Clasificaciones.IgnoreQueryFilters().AnyAsync(z => z.Descripcion == desc, ct))
                return ResultadoClasificacionCliente.Fallo("Ya existe una clasificaciones cliente con esa descripción.");

            var clasificacion = new CatalogoClasificacion { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Clasificaciones.Add(clasificacion);
            await _db.SaveChangesAsync(ct);
            return ResultadoClasificacionCliente.Ok(new ClasificaionClienteDto(clasificacion.IdClasificacion, clasificacion.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoClasificacionCliente.Fallo("Error en la base de datos al crear la clasificaciones cliente.");
        }
    }

    // 4. Actualizar (Permite reactivar o corregir zonas inactivas)
    public async Task<ResultadoClasificacionCliente> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoClasificacionCliente.Fallo(error);

        try
        {
            var clasificacion = await _db.Clasificaciones.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdClasificacion == id, ct);
            if (clasificacion is null) return ResultadoClasificacionCliente.NoExiste($"No se encontró la clasificaciones cliente #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Clasificaciones.IgnoreQueryFilters().AnyAsync(z => z.Descripcion == desc && z.IdClasificacion != id, ct))
                return ResultadoClasificacionCliente.Fallo("Ya existe otra clasificaciones cliente con esa descripción.");

            clasificacion.Descripcion = desc;
            clasificacion.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoClasificacionCliente.Ok(new ClasificaionClienteDto(clasificacion.IdClasificacion, clasificacion.Descripcion, clasificacion.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoClasificacionCliente.Fallo("Error en la base de datos al actualizar la clasificaciones cliente.");
        }
    }

    // 5. Dar de Baja (Permite interactuar con el registro si ya estaba filtrado)
    public async Task<ResultadoClasificacionCliente> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var clasificacion = await _db.Clasificaciones.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdClasificacion == id, ct);
            if (clasificacion is null) return ResultadoClasificacionCliente.NoExiste($"No se encontró la clasificaciones cliente #{id}.");

            clasificacion.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoClasificacionCliente.Ok(new ClasificaionClienteDto(clasificacion.IdClasificacion, clasificacion.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoClasificacionCliente.Fallo("Error en la base de datos al dar de baja la clasificaciones cliente.");
        }
    }
}
