namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Solicitud de registro de pago. El cliente móvil genera PagoUuid
/// (idempotencia offline). El servidor asigna el folio definitivo.
/// </summary>
public record RegistrarPagoRequest(
    Guid PagoUuid,
    long IdVenta,
    decimal MontoAbona,
    long? IdCobrador,
    long? IdMovimiento,
    long IdUsuarioRegistro,
    string? Observaciones,
    DateTimeOffset? FechaPago);

public record RegistrarPagoResponse(
    long IdPago,
    Guid PagoUuid,
    long IdVenta,
    string FolioPago,
    int ConsecutivoPago,
    decimal SaldoAnterior,
    decimal SaldoActual,
    decimal MontoAbona,
    bool Liquidada);
