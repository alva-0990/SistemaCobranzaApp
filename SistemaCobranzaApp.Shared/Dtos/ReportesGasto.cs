namespace SistemaCobranzaApp.Shared.Dtos;

// =====================================================================
// Reportes del módulo de Gastos
// ---------------------------------------------------------------------
// Regla transversal: para evitar duplicar importes al relacionar gastos
// con sus pagos/movimientos, los importes de gasto (Importe, TotalPagado,
// SaldoPendiente) se calculan a nivel del gasto; el "pagado" proviene de
// GastosPagos agregados por gasto, nunca multiplicando filas.
// =====================================================================

// ---- Filtro común de gastos ----

/// <summary>
/// Filtro para los reportes de gastos. Todos los campos son opcionales.
/// Clasificación operativa: Zona / Central (<see cref="SoloCentrales"/>) /
/// Cobrador / Vendedor.
/// </summary>
public record ReporteGastosFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdCategoria = null,
    string? Estatus = null,
    long? IdZona = null,
    long? IdCobrador = null,
    long? IdVendedor = null,
    bool SoloCentrales = false);

// ---- Detalle de gastos (lista plana con importes no duplicados) ----

public record ReporteGastoFila(
    long IdGasto,
    string FolioGasto,
    DateTimeOffset FechaGasto,
    string Categoria,
    string Concepto,
    string? Proveedor,
    decimal Importe,
    decimal TotalPagado,
    decimal SaldoPendiente,
    string Estatus,
    string? Zona,
    string? Cobrador,
    string? Vendedor);

public record ReporteGastosResponse(
    IReadOnlyList<ReporteGastoFila> Filas,
    int Total,
    decimal ImporteTotal,
    decimal TotalPagado,
    decimal SaldoPendiente);

// ---- Agrupaciones (totales por dimensión) ----

/// <summary>
/// Fila de resumen agrupada por una dimensión (categoría, zona, cobrador,
/// vendedor, estatus, etc.). <see cref="Clave"/> es el identificador o
/// etiqueta del grupo; <see cref="Etiqueta"/> su descripción legible.
/// </summary>
public record ReporteGastosAgrupadoFila(
    string Clave,
    string Etiqueta,
    int NumeroGastos,
    decimal Importe,
    decimal TotalPagado,
    decimal SaldoPendiente);

public record ReporteGastosAgrupadoResponse(
    IReadOnlyList<ReporteGastosAgrupadoFila> Filas,
    int Total,
    decimal ImporteTotal,
    decimal TotalPagado,
    decimal SaldoPendiente);

// ---- Estado de cuenta de una cuenta de gastos ----

public record ReporteEstadoCuentaGastoFiltro(
    long IdCuentaGasto,
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null);

public record EstadoCuentaGastoMovimientoFila(
    long IdMovimientoCuenta,
    DateTimeOffset FechaMovimiento,
    string TipoMovimiento,
    string Concepto,
    string? Referencia,
    decimal Entrada,
    decimal Salida,
    decimal SaldoAcumulado);

public record ReporteEstadoCuentaGastoResponse(
    long IdCuentaGasto,
    string NombreCuenta,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    decimal TotalEntradas,
    decimal TotalSalidas,
    decimal SaldoFinal,
    IReadOnlyList<EstadoCuentaGastoMovimientoFila> Movimientos);
