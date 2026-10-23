using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoFrecuenciaPago
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CatalogoFrecuenciaPagoDto? FrecuenciaPago { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoFrecuenciaPago Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoFrecuenciaPago NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoFrecuenciaPago Ok(CatalogoFrecuenciaPagoDto f) => new() { Exito = true, FrecuenciaPago = f };
}

public class ServicioFrecuenciasPagoAdmin
{
    private const string Proceso = "ServicioFrecuenciasPagoAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioFrecuenciasPagoAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activas para los selectores comunes)
    public async Task<List<CatalogoFrecuenciaPagoDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoFrecuenciasPago
                .AsNoTracking()
                .Where(f => f.Estatus == Estatus.Activo)
                .OrderBy(f => f.DiasPeriodo) 
                .Select(f => new CatalogoFrecuenciaPagoDto(f.IdFrecuenciaPago, f.Descripcion, f.DiasPeriodo, true)) // Si entra aquí, es activa (true)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener las frecuencias de pago activas.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo evaluando el Enum como bool)
    public async Task<List<CatalogoFrecuenciaPagoDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoFrecuenciasPago
                .AsNoTracking()
                .IgnoreQueryFilters()
                .OrderBy(f => f.DiasPeriodo)
                .Select(f => new CatalogoFrecuenciaPagoDto(f.IdFrecuenciaPago, f.Descripcion, f.DiasPeriodo, f.Estatus == Estatus.Activo)) // Corrección aquí
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener todas las frecuencias de pago.");
            throw;
        }
    }

    // 3. Obtener por ID
    public async Task<CatalogoFrecuenciaPagoDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var f = await _db.CatalogoFrecuenciasPago
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.IdFrecuenciaPago == id, ct);

            return f is null ? null : new CatalogoFrecuenciaPagoDto(f.IdFrecuenciaPago, f.Descripcion, f.DiasPeriodo, f.Estatus == Estatus.Activo); // Corrección aquí
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la frecuencia de pago #{id}.");
            throw;
        }
    }

    // 4. Crear Esquema Financiero
    public async Task<ResultadoFrecuenciaPago> CrearAsync(string descripcion, int diasPeriodo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoFrecuenciaPago.Fallo(error);

        if (diasPeriodo <= 0) 
            return ResultadoFrecuenciaPago.Fallo("Los días del periodo deben ser mayores a cero.");

        var desc = descripcion.Trim().ToUpper();

        try
        {
            if (await _db.CatalogoFrecuenciasPago.IgnoreQueryFilters().AnyAsync(f => f.Descripcion == desc, ct))
                return ResultadoFrecuenciaPago.Fallo("Ya existe una frecuencia de pago con esa descripción.");

            var frecuencia = new CatalogoFrecuenciaPago 
            { 
                Descripcion = desc, 
                DiasPeriodo = diasPeriodo,
                Estatus = Estatus.Activo 
            };
            
            _db.CatalogoFrecuenciasPago.Add(frecuencia);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoFrecuenciaPago.Ok(new CatalogoFrecuenciaPagoDto(frecuencia.IdFrecuenciaPago, frecuencia.Descripcion, frecuencia.DiasPeriodo, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc, diasPeriodo });
            return ResultadoFrecuenciaPago.Fallo("Error en la base de datos al crear la frecuencia de pago.");
        }
    }

    // 5. Actualizar (Recibe un bool y lo traduce al Enum del dominio para guardar)
    public async Task<ResultadoFrecuenciaPago> ActualizarAsync(long id, string descripcion, int diasPeriodo, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoFrecuenciaPago.Fallo(error);

        if (diasPeriodo <= 0) 
            return ResultadoFrecuenciaPago.Fallo("Los días del periodo deben ser mayores a cero.");

        try
        {
            var f = await _db.CatalogoFrecuenciasPago.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdFrecuenciaPago == id, ct);
            if (f is null) return ResultadoFrecuenciaPago.NoExiste($"No se encontró la frecuencia de pago #{id}.");

            var desc = descripcion.Trim().ToUpper();
            if (await _db.CatalogoFrecuenciasPago.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.IdFrecuenciaPago != id, ct))
                return ResultadoFrecuenciaPago.Fallo("Ya existe otra frecuencia de pago con esa descripción.");

            f.Descripcion = desc;
            f.DiasPeriodo = diasPeriodo;
            f.Estatus = activo ? Estatus.Activo : Estatus.Inactivo; // Mapeo de bool a Enum hacia la BD
            
            await _db.SaveChangesAsync(ct);
            return ResultadoFrecuenciaPago.Ok(new CatalogoFrecuenciaPagoDto(f.IdFrecuenciaPago, f.Descripcion, f.DiasPeriodo, f.Estatus == Estatus.Activo)); // Corrección aquí
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion, diasPeriodo, activo });
            return ResultadoFrecuenciaPago.Fallo("Error en la base de datos al actualizar la frecuencia de pago.");
        }
    }

    // 6. Dar de Baja 
    public async Task<ResultadoFrecuenciaPago> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var f = await _db.CatalogoFrecuenciasPago.IgnoreQueryFilters().FirstOrDefaultAsync(z => z.IdFrecuenciaPago == id, ct);
            if (f is null) return ResultadoFrecuenciaPago.NoExiste($"No se encontró la frecuencia de pago #{id}.");

            f.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoFrecuenciaPago.Ok(new CatalogoFrecuenciaPagoDto(f.IdFrecuenciaPago, f.Descripcion, f.DiasPeriodo, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoFrecuenciaPago.Fallo("Error en la base de datos al dar de baja la frecuencia de pago.");
        }
    }
}
