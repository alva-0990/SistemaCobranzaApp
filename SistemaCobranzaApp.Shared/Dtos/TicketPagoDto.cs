namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Comprobante de PAGO (abono) para impresión térmica.
/// El QR se genera a partir de <see cref="CodigoVenta"/> para poder localizar la venta al escanearlo.
/// </summary>
public sealed record TicketPagoDto(
    string NombreNegocio,
    string FolioPago,
    DateTime Fecha,
    ClienteTicketDto Cliente,
    // Código de la venta: va al QR del comprobante (clave para re-localizar la cuenta).
    string CodigoVenta,
    long IdVenta,
    decimal SaldoAnterior,
    decimal MontoPagado,
    decimal SaldoActual,
    string? Movimiento,
    string? Cobrador,
    bool Liquidada,
    // Solo para comprobantes de ACTUALIZACIÓN: monto del pago original antes de la corrección.
    // Null en pagos y reversiones normales.
    decimal? MontoAnterior = null,
    // Formato del comprobante: true = ticket compacto; false = ticket detallado (actual).
    // Se resuelve desde el parámetro de sistema TICKET_PAGO_COMPACTO.
    bool Compacto = false);
