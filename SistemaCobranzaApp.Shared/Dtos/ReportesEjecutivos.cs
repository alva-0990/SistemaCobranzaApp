namespace SistemaCobranzaApp.Shared.Dtos;

// Reportes ejecutivos. Todos se alimentan de la misma consulta base de seguimiento
// (una fila por venta con saldo pendiente) y se agrupan por cliente en el servidor.

// ---- Filtro común (zona / ruta / clasificación) ----
public record ReporteDeudaFiltro(
    long? IdZona = null,
    long? IdRuta = null,
    long? IdClasificacion = null);

// ---- Dashboard ejecutivo ----
public record ReporteDashboardEjecutivoFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdZona = null,
    int DiasGraciaAlerta = 3);

public record ReporteDashboardEjecutivoResponse(
    decimal CarteraPendiente,
    decimal TotalCobrado,
    decimal VentasPeriodo,
    int ClientesActivos,
    int ClientesConAtraso,
    int ClientesCriticos,
    string? MayorDeudor,
    decimal MayorDeudorSaldo,
    string? ZonaMayorRiesgo,
    decimal ZonaMayorRiesgoSaldo);

// ---- Antigüedad de saldos ----
public record ReporteAntiguedadFila(
    long IdCliente,
    string Cliente,
    string? Zona,
    string? Ruta,
    string? Clasificacion,
    DateTimeOffset? FechaUltimoPago,
    int DiasTranscurridos,
    decimal SaldoPendiente,
    string Rango)
{
    public string? Direccion { get; init; }
    public string? TelefonoPrincipal { get; init; }
}

public record ReporteAntiguedadRangoFila(
    string Rango,
    int Clientes,
    decimal SaldoPendiente,
    decimal Porcentaje);

public record ReporteAntiguedadResponse(
    IReadOnlyList<ReporteAntiguedadFila> Filas,
    IReadOnlyList<ReporteAntiguedadRangoFila> Rangos,
    decimal SaldoTotal,
    int ClientesVencidos,
    decimal SaldoCritico,
    int MaximoAtraso);

// ---- Clientes sin pago reciente ----
public record ReporteSinPagoFiltro(
    long? IdZona = null,
    long? IdRuta = null,
    long? IdClasificacion = null,
    int DiasSinPago = 15);

public record ReporteSinPagoFila(
    long IdCliente,
    string Cliente,
    string? Zona,
    string? Ruta,
    string? Clasificacion,
    DateTimeOffset? FechaUltimoPago,
    int DiasSinPago,
    decimal SaldoPendiente)
{
    public string? Direccion { get; init; }
    public string? TelefonoPrincipal { get; init; }
}

public record ReporteSinPagoResponse(
    IReadOnlyList<ReporteSinPagoFila> Filas,
    int Total,
    decimal SaldoEnRiesgo,
    decimal PromedioDias,
    int MaximoAtraso);

// ---- Ranking de deudores ----
public record ReporteRankingDeudoresFila(
    int Posicion,
    long IdCliente,
    string Cliente,
    string? Zona,
    string? Ruta,
    string? Clasificacion,
    DateTimeOffset? FechaUltimoPago,
    int DiasTranscurridos,
    decimal SaldoPendiente)
{
    public string? Direccion { get; init; }
    public string? TelefonoPrincipal { get; init; }
}

public record ReporteRankingDeudoresResponse(
    IReadOnlyList<ReporteRankingDeudoresFila> Filas,
    decimal SaldoTotal,
    string? MayorDeudor,
    decimal MayorDeudorSaldo,
    decimal Top10Acumulado,
    decimal ParticipacionTop10);

// ---- Cartera por clasificación ----
public record ReporteCarteraClasificacionFila(
    string Clasificacion,
    int Clientes,
    decimal SaldoPendiente,
    decimal Porcentaje);

public record ReporteCarteraClasificacionResponse(
    IReadOnlyList<ReporteCarteraClasificacionFila> Filas,
    int TotalClientes,
    decimal SaldoTotal,
    string? MayorClasificacion,
    string? ClasificacionRiesgo);

// ---- Concentración de cartera ----
public record ReporteConcentracionFila(
    int Posicion,
    long IdCliente,
    string Cliente,
    decimal SaldoPendiente,
    decimal Participacion,
    string? Clasificacion,
    string? Zona);

public record ReporteConcentracionResponse(
    IReadOnlyList<ReporteConcentracionFila> Filas,
    decimal SaldoTotal,
    decimal Top5,
    decimal Top10,
    decimal Top20,
    decimal PorcentajeConcentracion);

// ---- Cobertura de cobranza (por crédito/venta) ----
// Compara, para cada crédito vigente con saldo, cuánto DEBERÍA haberse cobrado en el rango
// (cuota fija × periodos de su frecuencia, topado al saldo) contra lo realmente cobrado,
// y clasifica el crédito como PAGO / PARCIAL / NO_PAGO / SIN_FRECUENCIA.
public record ReporteCoberturaFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdZona = null,
    long? IdRuta = null);

// Estados posibles de cobertura de un crédito en el periodo.
public static class EstadoCobertura
{
    public const string Pago = "PAGO";
    public const string Parcial = "PARCIAL";
    public const string NoPago = "NO_PAGO";
    public const string SinFrecuencia = "SIN_FRECUENCIA";
}

public record ReporteCoberturaFila(
    long IdVenta,
    string? CodigoVenta,
    long IdCliente,
    string Cliente,
    string? Direccion,
    string? TelefonoPrincipal,
    string? Zona,
    string? Ruta,
    string Productos,
    decimal MontoCuota,
    string? Frecuencia,
    int DiasPeriodo,
    decimal MontoEsperado,
    decimal MontoCobrado,
    decimal Diferencia,
    string Estado,
    DateTimeOffset? FechaUltimoPago,
    int DiasSinPago,
    decimal SaldoPendiente);

// Resumen agregado por zona.
public record ReporteCoberturaResumenFila(
    string Zona,
    int TotalCreditos,
    int Pagaron,
    int Parciales,
    int NoPagaron,
    int SinFrecuencia,
    decimal EsperadoTotal,
    decimal CobradoTotal,
    decimal PorcentajeCobertura);

public record ReporteCoberturaResponse(
    IReadOnlyList<ReporteCoberturaResumenFila> Resumen,
    IReadOnlyList<ReporteCoberturaFila> Detalle,
    int TotalCreditos,
    int Pagaron,
    int Parciales,
    int NoPagaron,
    int SinFrecuencia,
    decimal EsperadoGlobal,
    decimal CobradoGlobal,
    decimal PorcentajeCoberturaGlobal);
