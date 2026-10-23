using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.CatalogoComisiones;

public class ResultadoCatalogoComision
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CatalogoComisionDto? Comision { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoCatalogoComision Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoCatalogoComision NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoCatalogoComision Ok(CatalogoComisionDto c) => new() { Exito = true, Comision = c };
}

public class ServicioCatalogoComisionesAdmin
{
    private const string Proceso = "ServicioCatalogoComisionesAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCatalogoComisionesAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activos para los selectores/combos de la UI)
    public async Task<List<CatalogoComisionDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoComisiones
                .AsNoTracking()
                .Where(c => c.Activo)
                .OrderBy(c => c.NombreEsquema)
                .Select(c => new CatalogoComisionDto(
                    c.IdCatalogoComision, 
                    c.NombreEsquema, 
                    c.TipoCalculo, 
                    c.ComisionContado, 
                    c.ComisionCredito, 
                    true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener los esquemas de comisión activos.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo ignorando filtros globales)
    public async Task<List<CatalogoComisionDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoComisiones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .OrderBy(c => c.NombreEsquema)
                .Select(c => new CatalogoComisionDto(
                    c.IdCatalogoComision, 
                    c.NombreEsquema, 
                    c.TipoCalculo, 
                    c.ComisionContado, 
                    c.ComisionCredito, 
                    c.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener todos los esquemas de comisión.");
            throw;
        }
    }

    // 3. Obtener por ID único (Permite auditar registros inactivos)
    public async Task<CatalogoComisionDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var c = await _db.CatalogoComisiones
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.IdCatalogoComision == id, ct);

            return c is null ? null : new CatalogoComisionDto(
                c.IdCatalogoComision, 
                c.NombreEsquema, 
                c.TipoCalculo, 
                c.ComisionContado, 
                c.ComisionCredito, 
                c.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el esquema de comisión #{id}.");
            throw;
        }
    }

    // 4. Crear Esquema Comercial
    public async Task<ResultadoCatalogoComision> CrearAsync(CrearCatalogoComisionRequest req, CancellationToken ct = default)
    {
        var errorNombre = ValidacionCatalogo.ValidarTextoRequerido(req.NombreEsquema, "El nombre del esquema");
        if (errorNombre is not null) return ResultadoCatalogoComision.Fallo(errorNombre);

        var tipo = req.TipoCalculo.Trim().ToUpper();
        if (tipo != "PORCENTAJE" && tipo != "FIJO")
            return ResultadoCatalogoComision.Fallo("El tipo de cálculo debe ser 'PORCENTAJE' o 'FIJO'.");

        if (req.ComisionContado < 0 || req.ComisionCredito < 0)
            return ResultadoCatalogoComision.Fallo("Los montos o valores de comisión no pueden ser negativos.");

        var nombre = req.NombreEsquema.Trim();

        try
        {
            if (await _db.CatalogoComisiones.IgnoreQueryFilters().AnyAsync(c => c.NombreEsquema == nombre, ct))
                return ResultadoCatalogoComision.Fallo("Ya existe un esquema de comisión con ese nombre.");

            var esquema = new CatalogoComision
            {
                NombreEsquema = nombre,
                TipoCalculo = tipo,
                ComisionContado = req.ComisionContado,
                ComisionCredito = req.ComisionCredito,
                Activo = true,
                FechaRegistro = DateTimeOffset.UtcNow // Mapeado seguro a UTC / Hora de persistencia central
            };

            _db.CatalogoComisiones.Add(esquema);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoCatalogoComision.Ok(new CatalogoComisionDto(
                esquema.IdCatalogoComision, 
                esquema.NombreEsquema, 
                esquema.TipoCalculo, 
                esquema.ComisionContado, 
                esquema.ComisionCredito, 
                true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: req);
            return ResultadoCatalogoComision.Fallo("Error en la base de datos al crear el esquema de comisión.");
        }
    }

    // 5. Actualizar Esquema Comercial (Permite reactivación)
    public async Task<ResultadoCatalogoComision> ActualizarAsync(long id, ActualizarCatalogoComisionRequest req, CancellationToken ct = default)
    {
        var errorNombre = ValidacionCatalogo.ValidarTextoRequerido(req.NombreEsquema, "El nombre del esquema");
        if (errorNombre is not null) return ResultadoCatalogoComision.Fallo(errorNombre);

        var tipo = req.TipoCalculo.Trim().ToUpper();
        if (tipo != "PORCENTAJE" && tipo != "FIJO")
            return ResultadoCatalogoComision.Fallo("El tipo de cálculo debe ser 'PORCENTAJE' o 'FIJO'.");

        if (req.ComisionContado < 0 || req.ComisionCredito < 0)
            return ResultadoCatalogoComision.Fallo("Los montos o valores de comisión no pueden ser negativos.");

        try
        {
            var esquema = await _db.CatalogoComisiones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.IdCatalogoComision == id, ct);
            if (esquema is null) return ResultadoCatalogoComision.NoExiste($"No se encontró el esquema de comisión #{id}.");

            var nombre = req.NombreEsquema.Trim();
            if (await _db.CatalogoComisiones.IgnoreQueryFilters().AnyAsync(c => c.NombreEsquema == nombre && c.IdCatalogoComision != id, ct))
                return ResultadoCatalogoComision.Fallo("Ya existe otro esquema de comisión con ese nombre.");

            esquema.NombreEsquema = nombre;
            esquema.TipoCalculo = tipo;
            esquema.ComisionContado = req.ComisionContado;
            esquema.ComisionCredito = req.ComisionCredito;
            esquema.Activo = req.Activo;

            await _db.SaveChangesAsync(ct);
            return ResultadoCatalogoComision.Ok(new CatalogoComisionDto(
                esquema.IdCatalogoComision, 
                esquema.NombreEsquema, 
                esquema.TipoCalculo, 
                esquema.ComisionContado, 
                esquema.ComisionCredito, 
                esquema.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { Id = id, Request = req });
            return ResultadoCatalogoComision.Fallo("Error en la base de datos al actualizar el esquema de comisión.");
        }
    }

    // 6. Dar de Baja (Desactivación lógica del esquema)
    public async Task<ResultadoCatalogoComision> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var esquema = await _db.CatalogoComisiones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.IdCatalogoComision == id, ct);
            if (esquema is null) return ResultadoCatalogoComision.NoExiste($"No se encontró el esquema de comisión #{id}.");

            esquema.Activo = false;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoCatalogoComision.Ok(new CatalogoComisionDto(
                esquema.IdCatalogoComision, 
                esquema.NombreEsquema, 
                esquema.TipoCalculo, 
                esquema.ComisionContado, 
                esquema.ComisionCredito, 
                false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoCatalogoComision.Fallo("Error en la base de datos al dar de baja el esquema de comisión.");
        }
    }
}
