using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

public class ResultadoCancelacionPago
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public bool NoEncontrado { get; init; }
    public CancelarPagoResponse? Respuesta { get; init; }

    public static ResultadoCancelacionPago Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoCancelacionPago NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoCancelacionPago Ok(CancelarPagoResponse r) => new() { Exito = true, Respuesta = r };
}

public class ResultadoCancelacionVenta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public bool NoEncontrado { get; init; }
    public CancelarVentaResponse? Respuesta { get; init; }

    public static ResultadoCancelacionVenta Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoCancelacionVenta NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoCancelacionVenta Ok(CancelarVentaResponse r) => new() { Exito = true, Respuesta = r };
}

/// <summary>
/// Cancelación de pagos y ventas.
///  - Cancelar pago: elimina el pago y REVIERTE su efecto en la venta
///    (resta el abono de TotalAbonos, recalcula saldos y estatus), transaccional.
///  - Cancelar venta: la marca como CANCELADA (no borra histórico).
/// </summary>
public class ServicioCancelaciones
{
    private readonly CobranzaDbContext _db;

    public ServicioCancelaciones(CobranzaDbContext db) => _db = db;

    public async Task<ResultadoCancelacionPago> CancelarPagoAsync(long idPago, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var pago = await _db.Pagos.FirstOrDefaultAsync(p => p.IdPago == idPago, ct);
            if (pago is null)
                return ResultadoCancelacionPago.NoExiste($"No se encontró el pago #{idPago}.");

            var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.IdVenta == pago.IdVenta, ct);
            if (venta is null)
                return ResultadoCancelacionPago.Fallo($"No se encontró la venta #{pago.IdVenta} del pago.");

            if (venta.IdEstatusVenta == EstatusVentaIds.Cancelada)
                return ResultadoCancelacionPago.Fallo("La venta está cancelada; no se pueden cancelar sus pagos.");

            var monto = pago.MontoAbona;

            // Revertir el abono en la venta.
            var nuevoTotalAbonos = CalculoCobranza.RevertirTotalAbonos(venta.TotalAbonos, monto);
            var nuevoSaldoPagado = venta.Enganche + nuevoTotalAbonos;
            var nuevoSaldo = CalculoCobranza.CalcularNuevoSaldo(venta.TotalFinal, venta.Enganche, nuevoTotalAbonos);

            venta.TotalAbonos = nuevoTotalAbonos;
            venta.SaldoPagado = nuevoSaldoPagado;
            venta.SaldoPendiente = nuevoSaldo;
            venta.IdEstatusVenta = nuevoSaldo <= 0
                ? EstatusVentaIds.Pagada
                : (nuevoSaldoPagado > 0 ? EstatusVentaIds.Abonando : EstatusVentaIds.Pendiente);
            venta.FechaModificacion = DateTimeOffset.UtcNow;

            _db.Pagos.Remove(pago);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoCancelacionPago.Ok(new CancelarPagoResponse(
                idPago, venta.IdVenta, monto, nuevoSaldo, venta.IdEstatusVenta));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ResultadoCancelacionVenta> CancelarVentaAsync(long idVenta, CancellationToken ct = default)
    {
        var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.IdVenta == idVenta, ct);
        if (venta is null)
            return ResultadoCancelacionVenta.NoExiste($"No se encontró la venta #{idVenta}.");

        if (venta.IdEstatusVenta == EstatusVentaIds.Cancelada)
            return ResultadoCancelacionVenta.Fallo("La venta ya está cancelada.");

        venta.IdEstatusVenta = EstatusVentaIds.Cancelada;
        venta.FechaModificacion = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var estatus = await _db.EstatusVenta.AsNoTracking()
            .Where(e => e.IdEstatusVenta == EstatusVentaIds.Cancelada)
            .Select(e => e.Descripcion).FirstOrDefaultAsync(ct);

        return ResultadoCancelacionVenta.Ok(new CancelarVentaResponse(idVenta, venta.IdEstatusVenta, estatus));
    }
}
