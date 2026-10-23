
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioGastosPagosAdmin
{
    private const string Proceso = "ServicioGastosPagosAdmin";

    private static readonly string[] FormasPagoValidas =
        ["EFECTIVO", "TRANSFERENCIA", "TARJETA", "CHEQUE", "OTRO"];

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioGastosPagosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<GastoPago>> ListarPorGastoAsync(
        long idGasto, CancellationToken ct = default)
    {
        try
        {
            return await _db.GastosPagos
                .AsNoTracking()
                .Where(x => x.IdGasto == idGasto)
                .OrderByDescending(x => x.FechaPago)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idGasto });
            throw;
        }
    }

    public async Task<ResultadoOperacion<GastoPago>> RegistrarAsync(
        long idGasto,
        long idCuentaGasto,
        decimal importe,
        string formaPago,
        long idUsuarioRegistro,
        DateTimeOffset? fechaPago = null,
        string? referencia = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        formaPago = formaPago?.Trim().ToUpperInvariant() ?? string.Empty;

        if (importe <= 0)
            return ResultadoOperacion<GastoPago>
                .Fallo("El importe del pago debe ser mayor que cero.");

        if (!FormasPagoValidas.Contains(formaPago))
            return ResultadoOperacion<GastoPago>
                .Fallo("La forma de pago no es válida.");

        if (referencia?.Length > 100)
            return ResultadoOperacion<GastoPago>
                .Fallo("La referencia no puede exceder 100 caracteres.");

        if (idUsuarioRegistro <= 0)
            return ResultadoOperacion<GastoPago>
                .Fallo("El usuario que registra el pago no es válido.");

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var gasto = await _db.Gastos
                .FirstOrDefaultAsync(x => x.IdGasto == idGasto, ct);

            if (gasto is null)
                return ResultadoOperacion<GastoPago>
                    .NoExiste($"No se encontró el gasto #{idGasto}.");

            if (gasto.Estatus is "PENDIENTE" or "CANCELADO")
                return ResultadoOperacion<GastoPago>
                    .Fallo("El gasto debe estar autorizado antes de registrar pagos.");

            var cuenta = await _db.CuentasGasto
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == idCuentaGasto, ct);

            if (cuenta is null || cuenta.Estatus != 1)
                return ResultadoOperacion<GastoPago>
                    .Fallo("La cuenta no existe o está inactiva.");

            var totalPagado = await _db.GastosPagos
                .Where(x => x.IdGasto == idGasto)
                .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;

            var saldoPendiente = gasto.Importe - totalPagado;

            if (importe > saldoPendiente)
                return ResultadoOperacion<GastoPago>.Fallo(
                    $"El pago excede el saldo pendiente de {saldoPendiente:C2}.");

            var saldoCuenta =
                cuenta.SaldoInicial +
                (await _db.MovimientosCuentaGasto
                    .Where(x => x.IdCuentaGasto == idCuentaGasto &&
                                x.TipoMovimiento == "ENTRADA")
                    .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m) -
                (await _db.MovimientosCuentaGasto
                    .Where(x => x.IdCuentaGasto == idCuentaGasto &&
                                x.TipoMovimiento == "SALIDA")
                    .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m);

            if (importe > saldoCuenta)
                return ResultadoOperacion<GastoPago>.Fallo(
                    $"La cuenta no tiene saldo suficiente. Saldo actual: {saldoCuenta:C2}.");

            var fecha = (fechaPago ?? DateTimeOffset.UtcNow).ToUniversalTime();

            var pago = new GastoPago
            {
                IdGasto = idGasto,
                IdCuentaGasto = idCuentaGasto,
                FechaPago = fecha,
                Importe = importe,
                FormaPago = formaPago,
                Referencia = referencia?.Trim(),
                Observaciones = observaciones?.Trim(),
                IdUsuarioRegistro = idUsuarioRegistro,
                FechaAlta = DateTimeOffset.UtcNow
            };

            _db.GastosPagos.Add(pago);
            await _db.SaveChangesAsync(ct);

            var movimiento = new MovimientoCuentaGasto
            {
                IdCuentaGasto = idCuentaGasto,
                IdPagoGasto = pago.IdPagoGasto,
                TipoMovimiento = "SALIDA",
                Importe = importe,
                FechaMovimiento = fecha,
                Concepto = $"Pago de gasto {gasto.FolioGasto}",
                Referencia = referencia?.Trim(),
                Observaciones = observaciones?.Trim(),
                IdUsuarioRegistro = idUsuarioRegistro
            };

            _db.MovimientosCuentaGasto.Add(movimiento);

            var nuevoTotalPagado = totalPagado + importe;
            gasto.Estatus = nuevoTotalPagado >= gasto.Importe
                ? "PAGADO"
                : "PARCIAL";
            gasto.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoOperacion<GastoPago>.Ok(pago);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idGasto, idCuentaGasto });
            return ResultadoOperacion<GastoPago>
                .Fallo("No fue posible registrar el pago.");
        }
    }
}
