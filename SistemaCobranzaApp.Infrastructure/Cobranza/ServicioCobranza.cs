using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

public class ResultadoPago
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public RegistrarPagoResponse? Respuesta { get; init; }

    public static ResultadoPago Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoPago Ok(RegistrarPagoResponse r) => new() { Exito = true, Respuesta = r };
}

/// <summary>
/// Registro de pago portado de VistaPagoAgregar.BtnAplicarPago_Click
/// (app WPF, ~L1750-2014) y UtilidadesSistema.GenerarFolioMovimiento.
///
/// Diferencias clave frente a la app WPF (por la arquitectura offline):
///  * El folio definitivo se genera SIEMPRE en el servidor.
///  * PagoUuid da idempotencia: reintentos de sincronización no duplican.
///  * El servidor es la autoridad del saldo.
/// </summary>
public class ServicioCobranza
{
    private readonly CobranzaDbContext _db;

    public ServicioCobranza(CobranzaDbContext db)
    {
        _db = db;
    }

    public async Task<ResultadoPago> RegistrarPagoAsync(RegistrarPagoRequest req, CancellationToken ct = default)
    {
        if (req.MontoAbona <= 0)
            return ResultadoPago.Fallo("El monto del abono debe ser mayor a cero.");

        // Idempotencia: si el pago ya fue registrado (misma UUID), devolverlo.
        var existente = await _db.Pagos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PagoUuid == req.PagoUuid, ct);
        if (existente is not null)
        {
            return ResultadoPago.Ok(new RegistrarPagoResponse(
                existente.IdPago, existente.PagoUuid, existente.IdVenta,
                existente.FolioPago ?? string.Empty, existente.ConsecutivoPago ?? 0,
                existente.SaldoAnterior, existente.SaldoActual, existente.MontoAbona,
                existente.SaldoActual <= 0));
        }

        var idMovimiento = req.IdMovimiento ?? MovimientoIds.Abono;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.IdVenta == req.IdVenta, ct);
            if (venta is null)
                return ResultadoPago.Fallo($"No se encontró la venta #{req.IdVenta}.");

            // Saldo autoritativo del servidor.
            var saldoAnterior = CalculoCobranza.CalcularSaldoAnterior(
                venta.TotalFinal, venta.Enganche, venta.TotalAbonos);

            var validacion = CalculoCobranza.ValidarAbono(req.MontoAbona, saldoAnterior);
            switch (validacion)
            {
                case CalculoCobranza.ResultadoValidacion.YaLiquidada:
                    return ResultadoPago.Fallo("Esta venta ya está liquidada.");
                case CalculoCobranza.ResultadoValidacion.AbonoExcedeSaldo:
                    return ResultadoPago.Fallo(
                        $"El pago de {req.MontoAbona:C2} es mayor al saldo pendiente de {saldoAnterior:C2}.");
                case CalculoCobranza.ResultadoValidacion.MontoInvalido:
                    return ResultadoPago.Fallo("El monto del abono debe ser mayor a cero.");
            }

            var nuevoTotalAbonos = venta.TotalAbonos + req.MontoAbona;
            var nuevoSaldoPagado = venta.Enganche + nuevoTotalAbonos;
            var nuevoSaldo = CalculoCobranza.CalcularNuevoSaldo(
                venta.TotalFinal, venta.Enganche, nuevoTotalAbonos);

            // Folio definitivo server-side: MAX(ConsecutivoPago)+1 por venta.
            var maxConsecutivo = await _db.Pagos
                .Where(p => p.IdVenta == req.IdVenta)
                .MaxAsync(p => (int?)p.ConsecutivoPago, ct) ?? 0;
            var consecutivo = maxConsecutivo + 1;
            var folio = CalculoCobranza.FormatearFolio(idMovimiento, consecutivo);

            var fechaPago = req.FechaPago ?? DateTimeOffset.UtcNow;
            var ahora = DateTimeOffset.UtcNow;

            var pago = new Pago
            {
                PagoUuid = req.PagoUuid == Guid.Empty ? Guid.NewGuid() : req.PagoUuid,
                IdVenta = venta.IdVenta,
                IdCliente = venta.IdCliente,
                SaldoAnterior = saldoAnterior,
                SaldoActual = nuevoSaldo,
                MontoAbona = req.MontoAbona,
                IdCobrador = req.IdCobrador,
                FechaPago = fechaPago,
                IdMovimiento = idMovimiento,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = req.IdUsuarioRegistro,
                FolioPago = folio,
                ConsecutivoPago = consecutivo,
                FechaModificacion = ahora
            };
            _db.Pagos.Add(pago);

            // Actualiza la venta (totales, saldos, estatus).
            venta.FechaUltimoPago = fechaPago;
            venta.TotalAbonos = nuevoTotalAbonos;
            venta.SaldoPagado = nuevoSaldoPagado;
            venta.SaldoPendiente = nuevoSaldo;
            venta.IdEstatusVenta = CalculoCobranza.DeterminarEstatusVenta(nuevoSaldo);
            venta.FechaModificacion = ahora;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoPago.Ok(new RegistrarPagoResponse(
                pago.IdPago, pago.PagoUuid, venta.IdVenta, folio, consecutivo,
                saldoAnterior, nuevoSaldo, req.MontoAbona, nuevoSaldo <= 0));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
