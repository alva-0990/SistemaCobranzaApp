namespace SistemaCobranzaApp.Shared.Dtos;

// Estado de cuenta integral del cliente: contiene todo lo necesario para emitir el documento (PDF)
// sin consultas adicionales. Se expone por API para poder reutilizarlo (p. ej. envío por WhatsApp).

public record EstadoCuentaIntegralClienteDto(
    long IdCliente,
    string Nombre,
    string? Clasificacion,
    string? Telefonos,
    string? Correo,
    bool Activo,
    string? Direccion,
    string? Ruta,
    string? Zona,
    string? Vendedor,
    string? Referencia);

public record EstadoCuentaIntegralResumenDto(
    decimal HistoricoSubtotal,
    decimal HistoricoDescuentos,
    decimal HistoricoVendido,
    decimal HistoricoEnganches,
    decimal HistoricoAbonos,
    decimal HistoricoPagado,
    decimal HistoricoBonificaciones,
    decimal HistoricoTraspasos,
    decimal SaldoPendiente,
    int CreditosActivos,
    int Liquidados,
    int Cancelados);

public record EstadoCuentaIntegralCreditoDto(
    long IdVenta,
    string Codigo,
    DateTimeOffset FechaCompra,
    string Estado,
    string? Condicion,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    decimal Enganche,
    decimal Abonos,
    decimal Pagado,
    decimal Bonificaciones,
    decimal Traspaso,
    decimal Saldo,
    DateTimeOffset? FechaUltimoPago)
{
    /// <summary>Saldo de cuentas anteriores incluido en el Total (renovación).</summary>
    public decimal SaldoRenovado { get; init; }
    public string? RenuevaCuentas { get; init; }
    public string? RenovadaEnCuenta { get; init; }
}

public record EstadoCuentaIntegralProductoDto(
    string Codigo,
    string Descripcion,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Importe);

public record EstadoCuentaIntegralCuotaDto(
    int Numero,
    DateTimeOffset Vencimiento,
    decimal MontoCuota,
    decimal Pagado,
    string Estatus,
    decimal SaldoInsoluto);

public record EstadoCuentaIntegralPagoDto(
    string? Folio,
    DateTimeOffset Fecha,
    string? Movimiento,
    decimal Monto,
    decimal SaldoActual,
    string? Cobrador);

public record EstadoCuentaIntegralDetalleDto(
    EstadoCuentaIntegralCreditoDto Credito,
    decimal Enganche,
    decimal AvancePorcentaje,
    IReadOnlyList<EstadoCuentaIntegralProductoDto> Productos,
    IReadOnlyList<EstadoCuentaIntegralCuotaDto> Cuotas,
    IReadOnlyList<EstadoCuentaIntegralPagoDto> Pagos)
{
    // Desglose de bonificaciones (movimientos que reducen el saldo sin ser cobro).
    public decimal BonificacionRacha { get; init; }
    public decimal LiquidacionPlan { get; init; }
    public decimal Condonacion { get; init; }
}

public record EstadoCuentaIntegralCerradoDto(
    long IdVenta,
    string Codigo,
    DateTimeOffset FechaCompra,
    DateTimeOffset? FechaCierre,
    decimal Descuento,
    decimal Total,
    decimal Enganche,
    decimal Pagado,
    decimal Bonificaciones,
    decimal Traspaso)
{
    public string? RenovadaEnCuenta { get; init; }
    public decimal SaldoRenovado { get; init; }
    public string? RenuevaCuentas { get; init; }
}

public record EstadoCuentaIntegralDto(
    string Empresa,
    DateTimeOffset FechaEmision,
    EstadoCuentaIntegralClienteDto Cliente,
    string PerfilRiesgo,
    EstadoCuentaIntegralResumenDto Resumen,
    IReadOnlyList<EstadoCuentaIntegralCreditoDto> Creditos,
    IReadOnlyList<EstadoCuentaIntegralDetalleDto> Activos,
    IReadOnlyList<EstadoCuentaIntegralCerradoDto> Liquidados,
    IReadOnlyList<EstadoCuentaIntegralCerradoDto> Cancelados,
    string Observaciones,
    string Operador,
    string? ZonaOperacion,
    string SelloIntegridad);
