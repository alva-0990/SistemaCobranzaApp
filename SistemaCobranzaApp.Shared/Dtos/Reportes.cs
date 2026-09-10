namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Reporte de cartera ----
public record ReporteCarteraFiltro(long? IdZona = null, long? IdRuta = null, long? IdEstatusVenta = null);

public record ReporteCarteraFila(
    long IdVenta,
    string? CodigoVenta,
    long IdCliente,
    string Cliente,
    string? Ruta,
    string? Zona,
    decimal TotalFinal,
    decimal Enganche,
    decimal TotalAbonos,
    decimal Saldo,
    string? Estado,
    DateTimeOffset FechaCompra,
    DateTimeOffset? FechaUltimoPago);

public record ReporteCarteraResponse(IReadOnlyList<ReporteCarteraFila> Filas, int Total, decimal SaldoTotal);

// ---- Reporte de pagos ----
public record ReportePagosFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdCobrador = null);

public record ReportePagoFila(
    long IdPago,
    string? FolioPago,
    long IdVenta,
    string Cliente,
    string? Cobrador,
    decimal MontoAbona,
    decimal SaldoActual,
    DateTimeOffset FechaPago);

public record ReportePagosResponse(IReadOnlyList<ReportePagoFila> Filas, int Total, decimal MontoTotal);

// ---- Reporte de ventas ----
public record ReporteVentasFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdVendedor = null);

public record ReporteVentaFila(
    long IdVenta,
    string? CodigoVenta,
    string Cliente,
    string? Vendedor,
    decimal TotalFinal,
    decimal Enganche,
    decimal Saldo,
    string? Estado,
    DateTimeOffset FechaCompra);

public record ReporteVentasResponse(IReadOnlyList<ReporteVentaFila> Filas, int Total, decimal MontoTotal);

// ---- Reporte de recuperación (resumen por cobrador) ----
public record ReporteRecuperacionFiltro(DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null);

public record ReporteRecuperacionFila(
    long? IdCobrador,
    string Cobrador,
    int NumeroPagos,
    decimal MontoCobrado);

public record ReporteRecuperacionResponse(IReadOnlyList<ReporteRecuperacionFila> Filas, decimal MontoTotal);

// ---- Reporte de clientes ----
public record ReporteClientesFiltro(
    long? IdRuta = null,
    long? IdClasificacion = null,
    bool incluirInactivos = false);

public record ReporteClienteFila(
    long IdCliente,
    string Cliente,
    string? Ruta,
    string? Zona,
    string? Clasificacion,
    bool Activo,
    DateTimeOffset FechaAlta);

public record ReporteClientesResponse(IReadOnlyList<ReporteClienteFila> Filas, int Total);
