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
    string Rango);

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
    decimal SaldoPendiente);

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
    decimal SaldoPendiente);

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
