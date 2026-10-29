namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Reporte de cartera ----
public record ReporteCarteraFiltro(
    long? IdZona = null, 
    long? IdRuta = null, 
    long? IdEstatusVenta = null);

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
    DateTimeOffset? FechaUltimoPago)
{
    public string Productos { get; init; } = "";
}

public record ReporteCarteraResponse(
    IReadOnlyList<ReporteCarteraFila> Filas, 
    int Total, 
    decimal SaldoTotal
    );

// ---- Reporte de pagos ----
public record ReportePagosFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdCobrador = null,
    long? IdZona = null,
    long? IdRuta = null,
    long? IdMovimiento = null);

public record ReportePagoFila(
    long IdPago,
    string? FolioPago,
    long IdVenta,
    string Cliente,
    string? Cobrador,
    decimal MontoAbona,
    decimal SaldoActual,
    DateTimeOffset FechaPago)
{
    public string Productos { get; init; } = "";
    public decimal TotalVenta { get; init; }
    public long? IdMovimiento { get; init; }
    public string? Movimiento { get; init; }
}

public record ReportePagosResponse(
    IReadOnlyList<ReportePagoFila> Filas, 
    int Total, 
    decimal MontoTotal
    );

// ---- Reporte de ventas ----
public record ReporteVentasFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdVendedor = null,
    long? IdZona = null,
    long? IdRuta = null);

public record ReporteVentaFila(
    long IdVenta,
    string? CodigoVenta,
    string Cliente,
    string? Vendedor,
    decimal TotalFinal,
    decimal Enganche,
    decimal Saldo,
    string? Estado,
    DateTimeOffset FechaCompra)
{
    public string Productos { get; init; } = "";
}

public record ReporteVentasResponse(
    IReadOnlyList<ReporteVentaFila> Filas, 
    int Total, 
    decimal MontoTotal
    );

// ---- Reporte de recuperación (resumen por cobrador) ----
public record ReporteRecuperacionFiltro(
    DateTimeOffset? Desde = null, 
    DateTimeOffset? Hasta = null,
    long? IdZona = null,
    long? IdRuta = null
    );

public record ReporteRecuperacionFila(
    long? IdCobrador,
    string Cobrador,
    int NumeroPagos,
    decimal MontoCobrado);

public record ReporteRecuperacionResponse(
    IReadOnlyList<ReporteRecuperacionFila> Filas, 
    decimal MontoTotal
    );

// ---- Reporte de clientes ----
public record ReporteClientesFiltro(
    long? IdRuta = null,
    long? IdClasificacion = null,
    bool incluirInactivos = false,
    long? IdZona = null);

public record ReporteClienteFila(
    long IdCliente,
    string Cliente,
    string? Ruta,
    string? Zona,
    string? Clasificacion,
    bool Activo,
    DateTimeOffset FechaAlta);

public record ReporteClientesResponse(
    IReadOnlyList<ReporteClienteFila> Filas, 
    int Total
    );

// ---- Reporte de seguimiento (control de días desde el último pago) ----
public record ReporteSeguimientoFiltro(
    long? IdZona = null,
    DateTimeOffset? AltaDesde = null,
    DateTimeOffset? AltaHasta = null);

public record ReporteSeguimientoFila(
    long IdCliente,
    string Cliente,
    long IdVenta,
    string? FolioVenta,
    string Productos,
    decimal TotalVenta,
    decimal TotalCobrado,
    decimal TotalPendiente,
    DateTimeOffset FechaVenta,
    DateTimeOffset? FechaUltimoPago,
    int DiasTranscurridos,
    long? IdClasificacion,
    string? Clasificacion,
    DateTimeOffset FechaAlta,
    string? Zona,
    string? Observaciones = null,
    int DiasPeriodo = 0,
    string? FrecuenciaPago = null);

public record ReporteSeguimientoResponse(
    IReadOnlyList<ReporteSeguimientoFila> Filas,
    int Total,
    decimal TotalPendienteGlobal);

// Petición ligera para actualizar la clasificación (y, opcionalmente, las observaciones)
// de un cliente desde el reporte de seguimiento.
public record ActualizarClasificacionClienteRequest(long IdClasificacion, string? Observaciones = null);

// Resultado capturado por el diálogo de clasificación del reporte de seguimiento.
public record ResultadoClasificacionCliente(long IdClasificacion, string? Observaciones);

// ---- Dashboard administrativo (Home) ----
public record DashboardFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdZona = null);

public record DashboardKpis(
    decimal VentasTotales,
    decimal TotalCobrado,
    decimal SaldoPorCobrar,
    decimal CobradoHoy,
    int CuentasConSaldo,
    int PagosHoy);

public record DashboardSerieZona(string Zona, decimal Monto);

public record DashboardSerieDia(DateTimeOffset Dia, decimal Monto);

public record DashboardResumenFila(
    string Zona,
    string? Vendedor,
    string? Cobrador,
    decimal TotalVendido,
    decimal TotalCobrado,
    decimal SaldoPendiente,
    decimal PorcentajeRecuperacion); // 0–100

public record DashboardResponse(
    DashboardKpis Kpis,
    IReadOnlyList<DashboardSerieZona> VentasPorZona,
    IReadOnlyList<DashboardSerieZona> CobranzaPorZona,
    IReadOnlyList<DashboardSerieDia> CobranzaPorDia,
    IReadOnlyList<DashboardResumenFila> Resumen);
