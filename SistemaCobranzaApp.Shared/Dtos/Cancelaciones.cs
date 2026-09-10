namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Resultado de cancelar un pago (con el saldo de la venta ya recalculado).</summary>
public record CancelarPagoResponse(
    long IdPago,
    long IdVenta,
    decimal MontoRevertido,
    decimal SaldoPendienteVenta,
    long IdEstatusVenta);

/// <summary>Resultado de cancelar una venta.</summary>
public record CancelarVentaResponse(
    long IdVenta,
    long IdEstatusVenta,
    string? Estatus);
