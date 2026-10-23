namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Contenedor de filtros avanzados para la consulta de comisiones por pagar.
/// </summary>
public record FiltroComisionesRequest(
    long? IdVendedor = null,
    long? IdZona = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

public record ComisionPendientePagoDto(
    long IdComision,
    long IdVenta,
    string CodigoVenta,
    string DescripcionProducto,
    long IdVendedor,
    string NombreVendedor,
    string NombreZona, // 👈 NUEVO: Para saber a qué zona pertenece el vendedor/cliente
    decimal MontoComision,
    string EstatusComision,
    DateTimeOffset FechaCalculo
);

public record ProcesarPagoComisionesRequest(List<long> IdsComisiones);