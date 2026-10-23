// ---- Detalles de la Venta (Líneas/Renglones) ----
public record CrearDetalleVentaRequest(
    long IdProducto,
    int Cantidad,
    decimal PrecioCobrado
);

public record DetalleVentaDto(
    long IdDetalle,
    long IdVenta,
    long IdProducto,
    string NombreProducto, // Desnormalizado para simplificar lecturas en la app
    int Cantidad,
    decimal PrecioCobrado,
    decimal Importe
);

// ---- Opciones de Liquidación / Planes Promocionales ----
public record CrearVentaPlanRequest(
    int NumeroMeses,
    decimal MontoLiquidacion
);

public record VentaPlanDto(
    long IdPlan,
    long IdVenta,
    int NumeroMeses,
    decimal MontoLiquidacion,
    bool Activo
);

// ---- Plan de Amortización / Cuotas Individuales ----
public record VentaCuotaDto(
    long IdCuota,
    long IdVenta,
    int NumeroCuota,
    DateTimeOffset FechaVencimiento,
    decimal MontoCuota,
    decimal MontoInteres,
    decimal SaldoCuota,
    string EstatusCuota // 'PENDIENTE', 'PAGADA', 'VENCIDA'
);

// ---- Cabecera de la Venta ----
public record CrearVentaRequest(
    long IdCliente,
    long? IdVendedor,
    string? CodigoVenta,
    long IdCondicion,
    decimal Subtotal,
    decimal DescuentoGlobal,
    decimal TotalFinal,
    decimal Enganche,
    // Propiedades exclusivas si es a Crédito (IdCondicion = Crédito)
    long? IdFrecuenciaPago, 
    int? NumeroCuotas,
    decimal? MontoCuotaFija,
    decimal? TasaInteres,
    DateTimeOffset? FechaPrimerVence,
    string? Observaciones,
    // Hijos requeridos para la atomicidad de la transacción
    List<CrearDetalleVentaRequest> Detalles,
    List<CrearVentaPlanRequest>? PlanesPromocionales // Opcional
)
{
    public decimal? SaldoRenovacionEsperado { get; init; }
    public Guid? VentaUuid { get; init; }
    public DateTimeOffset? FechaCaptura { get; init; }
}

// ---- Edición de una venta existente (sin abonos aplicados) ----
// Reemplazo integral: la venta se recalcula por completo a partir de este request,
// igual que un alta, pero conservando el IdVenta y su folio original.
public record ActualizarVentaRequest(
    long? IdVendedor,
    long IdCondicion,
    decimal Subtotal,
    decimal DescuentoGlobal,
    decimal TotalFinal,
    decimal Enganche,
    long? IdFrecuenciaPago,
    int? NumeroCuotas,
    decimal? MontoCuotaFija,
    decimal? TasaInteres,
    DateTimeOffset? FechaPrimerVence,
    string? Observaciones,
    List<CrearDetalleVentaRequest> Detalles,
    List<CrearVentaPlanRequest>? PlanesPromocionales
);
public record VentaDto(
    long IdVenta,
    long IdCliente,
    string NombreCliente,
    long? IdVendedor,
    string? NombreVendedor,
    string? CodigoVenta,
    DateTimeOffset FechaCompra,
    long IdCondicion,
    string DescripcionCondicion,
    decimal Subtotal,
    decimal DescuentoGlobal,
    decimal TotalFinal,
    decimal Enganche,
    decimal MontoFinanciado,
    // Sección Crédito
    long? IdFrecuenciaPago,
    string? DescripcionFrecuencia,
    int NumeroCuotas,
    decimal MontoCuotaFija,
    decimal TasaInteres,
    DateTimeOffset? FechaPrimerVence,
    // Saldos controlados
    decimal TotalAbonado,
    decimal SaldoPendiente,
    DateTimeOffset? FechaUltimoPago,
    long IdEstatusVenta,
    string DescripcionEstatus,
    string? Observaciones,
    // 🎯 ADICIONES: Listas hijas para el Estado de Cuenta Completo en .NET MAUI
    List<DetalleVentaDto>? Detalles = null,
    List<VentaCuotaDto>? Cuotas = null,
    List<VentaPlanDto>? PlanesPromocionales = null
);
