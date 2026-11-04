using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Whatsapp;
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
/// Registro de pago optimizado para trabajar en simbiosis con el Trigger de PostgreSQL.
/// El disparador en la BD gestiona la amortización en cascada sobre las cuotas y los saldos reales.
/// </summary>
public class ServicioCobranza
{
    private readonly CobranzaDbContext _db;
    private readonly ServicioColaWhatsApp _colaWhatsApp;

    public ServicioCobranza(CobranzaDbContext db, ServicioColaWhatsApp colaWhatsApp)
    {
        _db = db;
        _colaWhatsApp = colaWhatsApp;
    }

    public async Task<ResultadoPago> RegistrarPagoAsync(RegistrarPagoRequest req, CancellationToken ct = default)
    {
        if (req.MontoAbona <= 0)
            return ResultadoPago.Fallo("El monto del abono debe ser mayor a cero.");

        // 1. Idempotencia: Si el UUID inmutable de .NET MAUI ya existe en el servidor, lo retornamos directo
        var existente = await _db.Pagos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PagoUuid == req.PagoUuid, ct);
            
        if (existente is not null)
        {
            return ResultadoPago.Ok(await ConstruirRespuestaAsync(existente, ct));
        }

        var idMovimiento = req.IdMovimiento ?? MovimientoIds.Abono;

        // 2. Apertura del bloque transaccional atómico
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Bloqueamos la fila de la cabecera mediante la lectura para evitar colisiones de concurrencia en la ruta
            var venta = await _db.Ventas.FromSqlInterpolated(
                $"SELECT * FROM venta WHERE idventa = {req.IdVenta} FOR UPDATE").FirstOrDefaultAsync(ct);
            if (venta is null)
                return ResultadoPago.Fallo($"No se encontró la venta transaccional #{req.IdVenta}.");
            await _db.Entry(venta).ReloadAsync(ct);
            // Otra solicitud pudo confirmar el mismo UUID mientras esperábamos el bloqueo.
            var repetido = await _db.Pagos.AsNoTracking().FirstOrDefaultAsync(p => p.PagoUuid == req.PagoUuid, ct);
            if (repetido is not null)
                return ResultadoPago.Ok(await ConstruirRespuestaAsync(repetido, ct));
            if (venta.IdEstatusVenta == EstatusVentaIds.Cancelada)
                return ResultadoPago.Fallo("La venta está cancelada.");

            // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' proveniente de tu base de datos real
            // El saldo autoritativo es el SaldoPendiente que mantiene el trigger: además de los abonos
            // descuenta las bonificaciones automáticas (racha de pagos, pronto pago), que NO suman a
            // TotalAbonado. Se toma el menor para no aceptar un monto que el trigger rechazaría.
            var saldoAnterior = Math.Min(
                CalculoCobranza.CalcularSaldoAnterior(venta.TotalFinal, venta.Enganche, venta.TotalAbonado),
                venta.SaldoPendiente);

            var validacion = CalculoCobranza.ValidarAbono(req.MontoAbona, saldoAnterior);
            switch (validacion)
            {
                case CalculoCobranza.ResultadoValidacion.YaLiquidada:
                    return ResultadoPago.Fallo("Esta venta ya se encuentra liquidada de forma completa.");
                case CalculoCobranza.ResultadoValidacion.AbonoExcedeSaldo:
                    return ResultadoPago.Fallo(
                        $"El pago de {req.MontoAbona:C2} supera el saldo pendiente autoritativo de {saldoAnterior:C2}.");
                case CalculoCobranza.ResultadoValidacion.MontoInvalido:
                    return ResultadoPago.Fallo("El monto del abono en efectivo debe ser mayor a cero.");
            }

            // 3. Generación del Folio definitivo Server-Side (MAX por Venta)
            var maxConsecutivo = await _db.Pagos
                .Where(p => p.IdVenta == req.IdVenta)
                .MaxAsync(p => (int?)p.ConsecutivoPago, ct) ?? 0;
                
            var consecutivo = maxConsecutivo + 1;
            var folio = CalculoCobranza.FormatearFolio(idMovimiento, consecutivo);

            var fechaPago = req.FechaPago ?? DateTimeOffset.UtcNow;
            var ahora = DateTimeOffset.UtcNow;

            // 4. Instanciar e insertar la transacción en la tabla Pagos
            var pago = new Pago
            {
                PagoUuid = req.PagoUuid == Guid.Empty ? Guid.NewGuid() : req.PagoUuid,
                IdVenta = venta.IdVenta,
                IdCliente = venta.IdCliente,
                MontoAbona = req.MontoAbona,
                IdCobrador = req.IdCobrador,
                FechaPago = fechaPago,
                IdMovimiento = idMovimiento,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = req.IdUsuarioRegistro,
                FolioPago = folio,
                ConsecutivoPago = consecutivo,
                FechaModificacion = ahora
                
                // Nota: Las columnas 'SaldoAnterior' y 'SaldoActual' no se mapean manualmente aquí.
                // El Trigger 'BEFORE INSERT' en PostgreSQL las interceptará, inyectará sus valores 
                // reales y computará la cascada de amortizaciones de forma segura.
            };
            
            _db.Pagos.Add(pago);
            await _db.SaveChangesAsync(ct); 
            
            // 5. Sincronizar el estado del contexto de EF Core con los cambios inyectados por el Trigger
            // Al hacer Reload, EF Core descarga los campos que modificó la base de datos de manera oculta
            await _db.Entry(pago).ReloadAsync(ct);
            await _db.Entry(venta).ReloadAsync(ct);

            // 6. Liquidación por acuerdo: el cliente paga el monto convenido y se condona el resto.
            // El saldo a condonar se toma DESPUÉS del abono, cuando el trigger ya aplicó la cascada y
            // cualquier bonificación automática (racha / pronto pago). El AJUSTE usa el consecutivo
            // siguiente, que es el que limpia el trigger de reversión si se revierte el abono.
            if (idMovimiento == MovimientoIds.Abono && MarcasPago.EsLiquidacionAcuerdo(pago.Observaciones)
                && venta.SaldoPendiente > 0)
            {
                var condonar = venta.SaldoPendiente;
                _db.Pagos.Add(new Pago
                {
                    PagoUuid = Guid.NewGuid(),
                    IdVenta = venta.IdVenta,
                    IdCliente = venta.IdCliente,
                    MontoAbona = condonar,
                    IdCobrador = req.IdCobrador,
                    FechaPago = fechaPago,
                    IdMovimiento = MovimientoIds.Ajuste,
                    Observaciones = $"CONDONACIÓN POR ACUERDO DE LIQUIDACIÓN (folio {folio}). Saldo perdonado de {condonar:C2}.",
                    IdUsuarioRegistro = req.IdUsuarioRegistro,
                    FolioPago = CalculoCobranza.FormatearFolio(MovimientoIds.Ajuste, consecutivo + 1),
                    ConsecutivoPago = consecutivo + 1,
                    FechaModificacion = ahora
                });
                await _db.SaveChangesAsync(ct);
                await _db.Entry(venta).ReloadAsync(ct);
            }

            // Encola la notificación de WhatsApp dentro de la misma transacción, usando ya el saldo
            // real inyectado por el trigger (disponible tras ReloadAsync). Solo Add + un SaveChanges
            // adicional; si algo falla, el rollback revierte también el mensaje encolado.
            // La respuesta ya trae el estado final (con condonación si fue liquidación por acuerdo),
            // así el cliente recibe UN solo mensaje con todo el detalle.
            var respuesta = await ConstruirRespuestaAsync(pago, ct);
            var encolado = await _colaWhatsApp.EncolarPagoAsync(respuesta, ct);
            if (encolado) await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            // Despierta al worker solo después de confirmar la transacción.
            if (encolado) _colaWhatsApp.Senal.Avisar();

            return ResultadoPago.Ok(respuesta);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "P0001" } pg)
        {
            // Regla de negocio rechazada por el trigger (RAISE EXCEPTION): se devuelve como error
            // controlado para que el cliente lo muestre, en lugar de un 500.
            await tx.RollbackAsync(ct);
            return ResultadoPago.Fallo(pg.MessageText);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Respuesta del pago. En una liquidación por acuerdo refleja el estado FINAL de la operación:
    /// incluye la bonificación automática del mismo consecutivo y el AJUSTE de condonación del
    /// consecutivo siguiente, por lo que el saldo actual queda en 0 y la cuenta como liquidada.
    /// </summary>
    private async Task<RegistrarPagoResponse> ConstruirRespuestaAsync(Pago p, CancellationToken ct)
    {
        decimal bonificado = 0m, condonado = 0m;
        if (p.IdMovimiento == MovimientoIds.Abono && MarcasPago.EsLiquidacionAcuerdo(p.Observaciones)
            && p.ConsecutivoPago is { } c)
        {
            var asociados = await _db.Pagos.AsNoTracking()
                .Where(x => x.IdVenta == p.IdVenta && x.IdPago != p.IdPago
                            && ((x.ConsecutivoPago == c && (x.IdMovimiento == MovimientoIds.BonificacionProntoPago
                                                             || x.IdMovimiento == MovimientoIds.BonificacionPromocion))
                                || (x.ConsecutivoPago == c + 1 && x.IdMovimiento == MovimientoIds.Ajuste)))
                .Select(x => new { x.IdMovimiento, x.MontoAbona })
                .ToListAsync(ct);
            bonificado = asociados.Where(x => x.IdMovimiento != MovimientoIds.Ajuste).Sum(x => x.MontoAbona);
            condonado = asociados.Where(x => x.IdMovimiento == MovimientoIds.Ajuste).Sum(x => x.MontoAbona);
        }

        var saldoFinal = Math.Max(0m, p.SaldoActual - bonificado - condonado);
        return new RegistrarPagoResponse(
            p.IdPago, p.PagoUuid, p.IdVenta, p.FolioPago ?? string.Empty, p.ConsecutivoPago ?? 0,
            p.SaldoAnterior, saldoFinal, p.MontoAbona, saldoFinal <= 0,
            MontoCondonado: condonado, MontoBonificado: bonificado);
    }
}
