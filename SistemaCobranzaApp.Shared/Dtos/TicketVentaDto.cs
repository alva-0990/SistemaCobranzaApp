namespace SistemaCobranzaApp.Shared.Dtos;

public sealed record TicketVentaDto(
    string NombreNegocio,
    string CodigoVenta,
    DateTime Fecha,
    ClienteTicketDto Cliente,
    IReadOnlyList<TicketDetalleDto> Detalles,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    decimal Enganche,
    decimal MontoFinanciado,
    string? FrecuenciaPago,
    decimal CuotaFija,
    int NumeroCuotas,
    DateTimeOffset? FechaPrimerVence,
    IReadOnlyList<TicketPlanDto> PlanesPromocionales)
{
    /// <summary>Venta de contado (pagada en efectivo al momento): el ticket no muestra plan de crédito.</summary>
    public bool EsContado { get; init; }
}

public sealed record ClienteTicketDto(
    string NombreCompleto,
    string? Telefono,
    string? Direccion,
    string? Ruta,
    string? Zona);

public sealed record TicketDetalleDto(
    string Descripcion,
    int Cantidad,
    decimal Precio,
    decimal Importe);

public sealed record TicketPlanDto(
    int NumeroMeses,
    decimal MontoLiquidacion);