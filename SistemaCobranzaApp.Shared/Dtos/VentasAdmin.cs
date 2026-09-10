namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Una línea de la venta a crear.</summary>
public record CrearDetalleVentaRequest(
    long IdProducto,
    int Cantidad,
    decimal PrecioCobrado);

/// <summary>Datos para crear una venta con sus detalles.</summary>
public record CrearVentaRequest(
    long IdCliente,
    long? IdVendedor,
    long IdCondicion,
    decimal Enganche,
    decimal? PagoRequerido,
    string? Observaciones,
    long? IdUsuarioRegistro,
    IReadOnlyList<CrearDetalleVentaRequest> Detalles);

/// <summary>Renglón de detalle en la respuesta.</summary>
public record DetalleVentaDto(
    long IdDetalle,
    long IdProducto,
    string? Producto,
    int Cantidad,
    decimal PrecioCobrado,
    decimal Importe);

/// <summary>Venta completa con sus líneas.</summary>
public record VentaCompletaDto(
    long IdVenta,
    string? CodigoVenta,
    long IdCliente,
    string Cliente,
    long? IdVendedor,
    long IdCondicion,
    decimal Subtotal,
    decimal Enganche,
    decimal TotalFinal,
    decimal SaldoPagado,
    decimal SaldoPendiente,
    long IdEstatusVenta,
    string? Estatus,
    decimal? PagoRequerido,
    string? Observaciones,
    DateTimeOffset FechaCompra,
    IReadOnlyList<DetalleVentaDto> Detalles);
