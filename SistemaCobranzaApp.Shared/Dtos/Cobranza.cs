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
    DateTimeOffset? FechaPago,
    // Geolocalización del dispositivo al registrar el cobro (auditoría de cobranza en campo).
    // Opcionales al final para no romper los call-sites existentes.
    decimal? Latitud = null,
    decimal? Longitud = null);

public record RegistrarPagoResponse(
    long IdPago,
    Guid PagoUuid,
    long IdVenta,
    string FolioPago,
    int ConsecutivoPago,
    decimal SaldoAnterior,
    decimal SaldoActual,
    decimal MontoAbona,
    bool Liquidada,
    // Liquidación por acuerdo: saldo condonado por el AJUSTE automático y bonificaciones
    // automáticas del trigger (racha / pronto pago) aplicadas con este abono. 0 si no aplica.
    decimal MontoCondonado = 0m,
    decimal MontoBonificado = 0m);
