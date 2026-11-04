namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Fila de la tabla de paso colamensajeswhatsapp para consulta en pantalla.
/// Cliente y CodigoVenta se resuelven por la venta asociada (pueden venir vacíos).
/// </summary>
public record MensajeWhatsAppDto(
    long Id,
    string Telefono,
    string? Cliente,
    string Mensaje,
    string Estado,
    int Intentos,
    string? TipoOrigen,
    long? IdVenta,
    string? CodigoVenta,
    long? IdPago,
    string? ErrorLog,
    DateTimeOffset FechaCreacion,
    DateTimeOffset? FechaEnvio);
