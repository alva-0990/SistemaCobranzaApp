using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoCondicionCredito
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CondicionCreditoDto? CondicionCredito { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoCondicionCredito Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoCondicionCredito NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoCondicionCredito Ok(CondicionCreditoDto c) => new() { Exito = true, CondicionCredito = c };
}

public class ServicioCondicionCreditoAdmin
{
    private const string Proceso = "ServicioCondicionCreditoAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCondicionCreditoAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activas para los selectores/combos comunes)
    public async Task<List<CondicionCreditoDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Condiciones
                .AsNoTracking()
                .Where(c => c.Estatus == Estatus.Activo)
                .OrderBy(c => c.Descripcion)
                .Select(c => new CondicionCreditoDto(c.IdCondicion, c.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las condiciones de crédito activas.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<CondicionCreditoDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Condiciones
                .AsNoTracking()
                .IgnoreQueryFilters() // Rompe el filtro global para el panel de administración
                .OrderBy(c => c.Descripcion)
                .Select(c => new CondicionCreditoDto(c.IdCondicion, c.Descripcion, c.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las condiciones de crédito.");
            throw;
        }
    }

    // 3. Obtener por ID (Permite ver el detalle de condiciones inactivas)
    public async Task<CondicionCreditoDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var c = await _db.Condiciones
                .AsNoTracking()
                .IgnoreQueryFilters() // Permite al admin auditar o inspeccionar condiciones inactivas
                .FirstOrDefaultAsync(x => x.IdCondicion == id, ct);

            return c is null ? null : new CondicionCreditoDto(c.IdCondicion, c.Descripcion, c.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la condición de crédito #{id}.");
            throw;
        }
    }

    // 4. Crear (Alta de Condición de Crédito)
    public async Task<ResultadoCondicionCredito> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCondicionCredito.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            // Buscamos ignorando filtros por si quieren recrear una condición que ya existía inactiva
            if (await _db.Condiciones.IgnoreQueryFilters().AnyAsync(c => c.Descripcion == desc, ct))
                return ResultadoCondicionCredito.Fallo("Ya existe una condición de crédito con esa descripción.");

            // Nota: Cambia 'CatalogoCondicionCredito' por el nombre exacto de tu clase Entidad si varía
            var condicion = new CatalogoCondicion() { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Condiciones.Add(condicion);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoCondicionCredito.Ok(new CondicionCreditoDto(condicion.IdCondicion, condicion.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoCondicionCredito.Fallo("Error en la base de datos al crear la condición de crédito.");
        }
    }

    // 5. Actualizar (Permite reactivar o corregir condiciones inactivas)
    public async Task<ResultadoCondicionCredito> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCondicionCredito.Fallo(error);

        try
        {
            var condicion = await _db.Condiciones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.IdCondicion == id, ct);
            if (condicion is null) return ResultadoCondicionCredito.NoExiste($"No se encontró la condición de crédito #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Condiciones.IgnoreQueryFilters().AnyAsync(c => c.Descripcion == desc && c.IdCondicion != id, ct))
                return ResultadoCondicionCredito.Fallo("Ya existe otra condición de crédito con esa descripción.");

            condicion.Descripcion = desc;
            condicion.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoCondicionCredito.Ok(new CondicionCreditoDto(condicion.IdCondicion, condicion.Descripcion, condicion.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoCondicionCredito.Fallo("Error en la base de datos al actualizar la condición de crédito.");
        }
    }

    // 6. Dar de Baja (Inactivar condición)
    public async Task<ResultadoCondicionCredito> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var condicion = await _db.Condiciones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.IdCondicion == id, ct);
            if (condicion is null) return ResultadoCondicionCredito.NoExiste($"No se encontró la condición de crédito #{id}.");

            condicion.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoCondicionCredito.Ok(new CondicionCreditoDto(condicion.IdCondicion, condicion.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoCondicionCredito.Fallo("Error en la base de datos al dar de baja la condición de crédito.");
        }
    }
}
