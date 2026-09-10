namespace SistemaCobranzaApp.Shared.Dtos;

public record EstadoCuentaVentaDto(
    long IdVenta,
    string? CodigoVenta,
    decimal TotalFinal,
    decimal Enganche,
    decimal TotalAbonos,
    decimal Saldo,
    string? Estado,
    DateTimeOffset FechaCompra,
    DateTimeOffset? FechaUltimoPago);

public record EstadoCuentaPagoDto(
    long IdPago,
    string? FolioPago,
    long IdVenta,
    decimal MontoAbona,
    decimal SaldoActual,
    string? Cobrador,
    DateTimeOffset FechaPago);

/// <summary>Estado de cuenta consolidado de un cliente.</summary>
public record EstadoCuentaResponse(
    long IdCliente,
    string Cliente,
    string? Ruta,
    string? Zona,
    decimal TotalVendido,
    decimal TotalPagado,
    decimal SaldoTotal,
    IReadOnlyList<EstadoCuentaVentaDto> Ventas,
    IReadOnlyList<EstadoCuentaPagoDto> Pagos);
