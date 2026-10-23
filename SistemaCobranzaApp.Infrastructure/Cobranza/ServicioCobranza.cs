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
/// Registro de pago optimizado para trabajar en simbiosis con el Trigger de PostgreSQL.
/// El disparador en la BD gestiona la amortización en cascada sobre las cuotas y los saldos reales.
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

        // 1. Idempotencia: Si el UUID inmutable de .NET MAUI ya existe en el servidor, lo retornamos directo
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
                return ResultadoPago.Ok(new RegistrarPagoResponse(repetido.IdPago, repetido.PagoUuid, repetido.IdVenta,
                    repetido.FolioPago ?? "", repetido.ConsecutivoPago ?? 0, repetido.SaldoAnterior,
                    repetido.SaldoActual, repetido.MontoAbona, repetido.SaldoActual <= 0));
            if (venta.IdEstatusVenta == EstatusVentaIds.Cancelada)
                return ResultadoPago.Fallo("La venta está cancelada.");

            // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' proveniente de tu base de datos real
            var saldoAnterior = CalculoCobranza.CalcularSaldoAnterior(
                venta.TotalFinal, venta.Enganche, venta.TotalAbonado);

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

            await tx.CommitAsync(ct);

            return ResultadoPago.Ok(new RegistrarPagoResponse(
                pago.IdPago, 
                pago.PagoUuid, 
                venta.IdVenta, 
                folio, 
                consecutivo,
                pago.SaldoAnterior, // Datos inyectados de forma segura por el Trigger
                pago.SaldoActual,   // Datos inyectados de forma segura por el Trigger
                pago.MontoAbona, 
                pago.SaldoActual <= 0));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
