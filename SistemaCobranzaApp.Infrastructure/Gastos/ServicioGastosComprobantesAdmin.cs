
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioGastosComprobantesAdmin
{
    private const string Proceso = "ServicioGastosComprobantesAdmin";

    private static readonly string[] TiposValidos =
        ["TICKET", "FACTURA", "RECIBO", "FOTOGRAFIA", "OTRO"];

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioGastosComprobantesAdmin(
        CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<GastoComprobante>> ListarPorGastoAsync(
        long idGasto, CancellationToken ct = default)
    {
        try
        {
            return await _db.GastosComprobantes
                .AsNoTracking()
                .Where(x => x.IdGasto == idGasto)
                .OrderByDescending(x => x.FechaRegistro)
                .Select(x => new GastoComprobante
                {
                    IdComprobante = x.IdComprobante,
                    IdGasto = x.IdGasto,
                    TipoComprobante = x.TipoComprobante,
                    NombreArchivo = x.NombreArchivo,
                    MimeType = x.MimeType,
                    ArchivoUrl = x.ArchivoUrl,
                    Observaciones = x.Observaciones,
                    IdUsuarioRegistro = x.IdUsuarioRegistro,
                    FechaRegistro = x.FechaRegistro
                })
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idGasto });
            throw;
        }
    }

    public async Task<ResultadoOperacion<GastoComprobante>> ObtenerAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            var comprobante = await _db.GastosComprobantes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdComprobante == id, ct);

            if (comprobante is null)
                return ResultadoOperacion<GastoComprobante>
                    .NoExiste($"No se encontró el comprobante #{id}.");

            return ResultadoOperacion<GastoComprobante>.Ok(comprobante);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<GastoComprobante>
                .Fallo("No fue posible consultar el comprobante.");
        }
    }

    /// <summary>
    /// Obtiene el comprobante incluyendo el contenido binario (ArchivoBytes),
    /// para la descarga del archivo almacenado en la base de datos.
    /// </summary>
    public async Task<ResultadoOperacion<GastoComprobante>> ObtenerConArchivoAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            var comprobante = await _db.GastosComprobantes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdComprobante == id, ct);

            if (comprobante is null)
                return ResultadoOperacion<GastoComprobante>
                    .NoExiste($"No se encontró el comprobante #{id}.");

            return ResultadoOperacion<GastoComprobante>.Ok(comprobante);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<GastoComprobante>
                .Fallo("No fue posible consultar el comprobante.");
        }
    }

    public async Task<ResultadoOperacion<GastoComprobante>> CrearAsync(
        long idGasto,
        string tipoComprobante,
        long idUsuarioRegistro,
        string? nombreArchivo = null,
        string? mimeType = null,
        byte[]? archivoBytes = null,
        string? archivoUrl = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        tipoComprobante = string.IsNullOrWhiteSpace(tipoComprobante)
            ? "TICKET"
            : tipoComprobante.Trim().ToUpperInvariant();

        if (!TiposValidos.Contains(tipoComprobante))
            return ResultadoOperacion<GastoComprobante>
                .Fallo("El tipo de comprobante no es válido.");

        if (archivoBytes is not { Length: > 0 } &&
            string.IsNullOrWhiteSpace(archivoUrl))
        {
            return ResultadoOperacion<GastoComprobante>
                .Fallo("Debes proporcionar el archivo o una URL.");
        }

        if (nombreArchivo?.Length > 250 ||
            mimeType?.Length > 100 ||
            archivoUrl?.Length > 1000 ||
            observaciones?.Length > 250)
        {
            return ResultadoOperacion<GastoComprobante>
                .Fallo("Uno de los campos del comprobante excede su longitud permitida.");
        }

        if (idUsuarioRegistro <= 0)
            return ResultadoOperacion<GastoComprobante>
                .Fallo("El usuario que registra el comprobante no es válido.");

        try
        {
            var gastoExiste = await _db.Gastos
                .AnyAsync(x => x.IdGasto == idGasto, ct);

            if (!gastoExiste)
                return ResultadoOperacion<GastoComprobante>
                    .NoExiste($"No se encontró el gasto #{idGasto}.");

            var entidad = new GastoComprobante
            {
                IdGasto = idGasto,
                TipoComprobante = tipoComprobante,
                NombreArchivo = nombreArchivo?.Trim(),
                MimeType = mimeType?.Trim(),
                ArchivoBytes = archivoBytes is { Length: > 0 }
                    ? archivoBytes
                    : null,
                ArchivoUrl = string.IsNullOrWhiteSpace(archivoUrl)
                    ? null
                    : archivoUrl.Trim(),
                Observaciones = observaciones?.Trim(),
                IdUsuarioRegistro = idUsuarioRegistro,
                FechaRegistro = DateTimeOffset.UtcNow
            };

            _db.GastosComprobantes.Add(entidad);
            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<GastoComprobante>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idGasto });
            return ResultadoOperacion<GastoComprobante>
                .Fallo("No fue posible registrar el comprobante.");
        }
    }

    public async Task<ResultadoOperacion<bool>> EliminarAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            var comprobante = await _db.GastosComprobantes
                .FirstOrDefaultAsync(x => x.IdComprobante == id, ct);

            if (comprobante is null)
                return ResultadoOperacion<bool>
                    .NoExiste($"No se encontró el comprobante #{id}.");

            _db.GastosComprobantes.Remove(comprobante);
            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<bool>
                .Fallo("No fue posible eliminar el comprobante.");
        }
    }
}
