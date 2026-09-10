namespace SistemaCobranzaApp.Shared.Dtos;

// ===================== PUSH (subir cola de pagos) =====================

/// <summary>
/// Lote de pagos capturados offline en el móvil, para subir al servidor.
/// </summary>
public record PushPagosRequest(IReadOnlyList<RegistrarPagoRequest> Pagos);

/// <summary>
/// Resultado de sincronización de un pago individual.
/// </summary>
public enum EstadoSincronizacionPago
{
    /// <summary>Aplicado por primera vez.</summary>
    Aplicado,
    /// <summary>Ya existía (misma UUID); no se duplicó.</summary>
    Duplicado,
    /// <summary>No se aplicó: el monto excede el saldo real del servidor u otra regla.</summary>
    Conflicto,
    /// <summary>Error de datos (venta inexistente, monto inválido, etc.).</summary>
    Error
}

public record PushPagoResultado(
    Guid PagoUuid,
    EstadoSincronizacionPago Estado,
    string? Mensaje,
    RegistrarPagoResponse? Resultado);

public record PushPagosResponse(
    IReadOnlyList<PushPagoResultado> Resultados,
    int Aplicados,
    int Duplicados,
    int Conflictos,
    int Errores);

// ===================== PULL (bajar cambios) =====================

/// <summary>
/// Solicitud de cambios incrementales. El móvil envía la última marca
/// que sincronizó; el servidor devuelve lo modificado desde entonces.
/// Si Desde es null, se devuelve todo (primera sincronización).
/// </summary>
public record PullCambiosRequest(
    DateTimeOffset? Desde,
    long? IdCobrador = null,
    long? IdZona = null);

public record PullCambiosResponse(
    DateTimeOffset MarcaServidor,
    IReadOnlyList<CarteraCuentaDto> VentasModificadas,
    IReadOnlyList<PagoSyncDto> PagosModificados);

/// <summary>
/// Representación de un pago para bajar al móvil (cache local).
/// </summary>
public record PagoSyncDto(
    long IdPago,
    Guid PagoUuid,
    long IdVenta,
    long IdCliente,
    decimal MontoAbona,
    decimal SaldoAnterior,
    decimal SaldoActual,
    string? FolioPago,
    int? ConsecutivoPago,
    long? IdCobrador,
    DateTimeOffset FechaPago,
    DateTimeOffset FechaModificacion);
