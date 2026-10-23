using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoParametro
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ParametroSistemaDto? Parametro { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoParametro Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoParametro NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoParametro Ok(ParametroSistemaDto p) => new() { Exito = true, Parametro = p };
}

/// <summary>
/// Administración de los parámetros configurables del sistema (tabla ParametrosSistema).
/// La clave es la llave primaria (texto). Soporta valor numérico y/o de texto.
/// Estos parámetros alimentan los triggers de promociones de cobranza.
/// </summary>
public class ServicioParametrosAdmin
{
    private const string Proceso = "ServicioParametrosAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioParametrosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    private static ParametroSistemaDto AMapa(ParametroSistema p) =>
        new(p.Clave, p.Descripcion, p.ValorNumerico, p.ValorTexto, p.FechaModificacion);

    // 1. Listado completo (orden alfabético por clave).
    public async Task<List<ParametroSistemaDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.ParametrosSistema
                .AsNoTracking()
                .OrderBy(p => p.Clave)
                .Select(p => new ParametroSistemaDto(p.Clave, p.Descripcion, p.ValorNumerico, p.ValorTexto, p.FechaModificacion))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener los parámetros del sistema.");
            throw;
        }
    }

    // 2. Obtener por clave.
    public async Task<ParametroSistemaDto?> ObtenerAsync(string clave, CancellationToken ct = default)
    {
        try
        {
            var k = (clave ?? string.Empty).Trim().ToUpper();
            var p = await _db.ParametrosSistema.AsNoTracking().FirstOrDefaultAsync(x => x.Clave == k, ct);
            return p is null ? null : AMapa(p);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { clave });
            _log.Error(Proceso, $"Error en la base de datos al obtener el parámetro '{clave}'.");
            throw;
        }
    }

    // 3. Crear parámetro.
    public async Task<ResultadoParametro> CrearAsync(CrearParametroSistemaRequest req, CancellationToken ct = default)
    {
        var errorClave = ValidacionCatalogo.ValidarTextoRequerido(req.Clave, "La clave");
        if (errorClave is not null) return ResultadoParametro.Fallo(errorClave);

        var errorDesc = ValidacionCatalogo.ValidarTextoRequerido(req.Descripcion, "La descripción");
        if (errorDesc is not null) return ResultadoParametro.Fallo(errorDesc);

        if (req.ValorNumerico is null && string.IsNullOrWhiteSpace(req.ValorTexto))
            return ResultadoParametro.Fallo("Debe capturar un valor numérico o un valor de texto.");

        var clave = req.Clave.Trim().ToUpper();

        try
        {
            if (await _db.ParametrosSistema.AnyAsync(p => p.Clave == clave, ct))
                return ResultadoParametro.Fallo($"Ya existe un parámetro con la clave '{clave}'.");

            var entidad = new ParametroSistema
            {
                Clave = clave,
                Descripcion = req.Descripcion.Trim(),
                ValorNumerico = req.ValorNumerico,
                ValorTexto = string.IsNullOrWhiteSpace(req.ValorTexto) ? null : req.ValorTexto.Trim(),
                FechaModificacion = DateTimeOffset.UtcNow
            };

            _db.ParametrosSistema.Add(entidad);
            await _db.SaveChangesAsync(ct);

            return ResultadoParametro.Ok(AMapa(entidad));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { clave });
            return ResultadoParametro.Fallo("Error en la base de datos al crear el parámetro.");
        }
    }

    // 4. Actualizar parámetro (la clave es inmutable).
    public async Task<ResultadoParametro> ActualizarAsync(string clave, ActualizarParametroSistemaRequest req, CancellationToken ct = default)
    {
        var errorDesc = ValidacionCatalogo.ValidarTextoRequerido(req.Descripcion, "La descripción");
        if (errorDesc is not null) return ResultadoParametro.Fallo(errorDesc);

        if (req.ValorNumerico is null && string.IsNullOrWhiteSpace(req.ValorTexto))
            return ResultadoParametro.Fallo("Debe capturar un valor numérico o un valor de texto.");

        try
        {
            var k = (clave ?? string.Empty).Trim().ToUpper();
            var p = await _db.ParametrosSistema.FirstOrDefaultAsync(x => x.Clave == k, ct);
            if (p is null) return ResultadoParametro.NoExiste($"No se encontró el parámetro '{clave}'.");

            p.Descripcion = req.Descripcion.Trim();
            p.ValorNumerico = req.ValorNumerico;
            p.ValorTexto = string.IsNullOrWhiteSpace(req.ValorTexto) ? null : req.ValorTexto.Trim();
            p.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ResultadoParametro.Ok(AMapa(p));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { clave });
            return ResultadoParametro.Fallo("Error en la base de datos al actualizar el parámetro.");
        }
    }

    // 5. Eliminar parámetro (borrado físico: la tabla no maneja estatus).
    public async Task<ResultadoParametro> EliminarAsync(string clave, CancellationToken ct = default)
    {
        try
        {
            var k = (clave ?? string.Empty).Trim().ToUpper();
            var p = await _db.ParametrosSistema.FirstOrDefaultAsync(x => x.Clave == k, ct);
            if (p is null) return ResultadoParametro.NoExiste($"No se encontró el parámetro '{clave}'.");

            var dto = AMapa(p);
            _db.ParametrosSistema.Remove(p);
            await _db.SaveChangesAsync(ct);
            return ResultadoParametro.Ok(dto);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { clave });
            return ResultadoParametro.Fallo("Error en la base de datos al eliminar el parámetro.");
        }
    }
}
