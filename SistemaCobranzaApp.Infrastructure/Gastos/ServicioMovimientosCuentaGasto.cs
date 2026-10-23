
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioMovimientosCuentaGasto
{
    private const string Proceso = "ServicioMovimientosCuentaGasto";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioMovimientosCuentaGasto(
        CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<MovimientoCuentaGasto>> ListarAsync(
        long idCuenta,
        DateTimeOffset? desde = null,
        DateTimeOffset? hasta = null,
        CancellationToken ct = default)
    {
        try
        {
            desde = desde?.ToUniversalTime();
            hasta = hasta?.ToUniversalTime();

            var query = _db.MovimientosCuentaGasto
                .AsNoTracking()
                .Where(x => x.IdCuentaGasto == idCuenta);

            if (desde.HasValue)
                query = query.Where(x => x.FechaMovimiento >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(x => x.FechaMovimiento <= hasta.Value);

            return await query
                .OrderByDescending(x => x.FechaMovimiento)
                .ThenByDescending(x => x.IdMovimientoCuenta)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idCuenta });
            throw;
        }
    }

    public async Task<ResultadoOperacion<MovimientoCuentaGasto>> RegistrarManualAsync(
        long idCuenta,
        string tipoMovimiento,
        decimal importe,
        string concepto,
        long idUsuarioRegistro,
        string? referencia = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        tipoMovimiento = tipoMovimiento?.Trim().ToUpperInvariant() ?? string.Empty;
        concepto = concepto?.Trim() ?? string.Empty;

        if (tipoMovimiento is not ("ENTRADA" or "SALIDA"))
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("El tipo debe ser ENTRADA o SALIDA.");

        if (importe <= 0)
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("El importe debe ser mayor que cero.");

        if (string.IsNullOrWhiteSpace(concepto) || concepto.Length > 250)
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("El concepto es obligatorio y debe tener máximo 250 caracteres.");

        if (referencia?.Length > 100)
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("La referencia no puede exceder 100 caracteres.");

        if (idUsuarioRegistro <= 0)
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("El usuario que registra el movimiento no es válido.");

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var cuenta = await _db.CuentasGasto
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == idCuenta, ct);

            if (cuenta is null || cuenta.Estatus != 1)
                return ResultadoOperacion<MovimientoCuentaGasto>
                    .Fallo("La cuenta no existe o está inactiva.");

            if (tipoMovimiento == "SALIDA")
            {
                var saldo =
                    cuenta.SaldoInicial +
                    (await _db.MovimientosCuentaGasto
                        .Where(x => x.IdCuentaGasto == idCuenta &&
                                    x.TipoMovimiento == "ENTRADA")
                        .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m) -
                    (await _db.MovimientosCuentaGasto
                        .Where(x => x.IdCuentaGasto == idCuenta &&
                                    x.TipoMovimiento == "SALIDA")
                        .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m);

                if (importe > saldo)
                    return ResultadoOperacion<MovimientoCuentaGasto>
                        .Fallo($"Saldo insuficiente. Saldo actual: {saldo:C2}.");
            }

            var movimiento = new MovimientoCuentaGasto
            {
                IdCuentaGasto = idCuenta,
                IdPagoGasto = null,
                TipoMovimiento = tipoMovimiento,
                Importe = importe,
                FechaMovimiento = DateTimeOffset.UtcNow,
                Concepto = concepto,
                Referencia = referencia?.Trim(),
                Observaciones = observaciones?.Trim(),
                IdUsuarioRegistro = idUsuarioRegistro
            };

            _db.MovimientosCuentaGasto.Add(movimiento);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoOperacion<MovimientoCuentaGasto>.Ok(movimiento);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idCuenta, tipoMovimiento });
            return ResultadoOperacion<MovimientoCuentaGasto>
                .Fallo("No fue posible registrar el movimiento.");
        }
    }
}
