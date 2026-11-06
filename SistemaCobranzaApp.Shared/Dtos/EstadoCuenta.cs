using SistemaCobranzaApp.Domain.Constantes;

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
    DateTimeOffset? FechaUltimoPago)
{
    /// <summary>Estatus de la venta (ver EstatusVentaIds); 0 si no se informó.</summary>
    public long IdEstatusVenta { get; init; }

    /// <summary>Venta cancelada: solo se permite consultar su detalle.</summary>
    public bool EsCancelada => IdEstatusVenta == EstatusVentaIds.Cancelada;

    /// <summary>Importe de los productos antes del descuento global.</summary>
    public decimal Subtotal { get; init; }
    /// <summary>Descuento global aplicado a la venta (Subtotal − DescuentoGlobal = TotalFinal).</summary>
    public decimal DescuentoGlobal { get; init; }

    // ---- Movimientos que reducen el saldo sin ser dinero cobrado (no suman a TotalAbonos) ----
    /// <summary>Mov. 6 BONIFICACION PROMOCION: descuento por racha de pagos puntuales.</summary>
    public decimal BonificacionRacha { get; init; }
    /// <summary>Mov. 4 LIQUIDACION PRONTO PAGO: descuento por liquidación con plan a meses.</summary>
    public decimal LiquidacionPlan { get; init; }
    /// <summary>Mov. 5 AJUSTE: condonación (liquidación por acuerdo y ajustes manuales).</summary>
    public decimal Condonacion { get; init; }
    /// <summary>Mov. 7 LIQUIDADA POR RENOVACION: saldo traspasado a la nueva venta.</summary>
    public decimal TraspasoRenovacion { get; init; }

    /// <summary>Descuentos y ajustes aplicados después de la venta (racha + plan + condonación).</summary>
    public decimal TotalBonificaciones => BonificacionRacha + LiquidacionPlan + Condonacion;
}

/// <summary>
/// Cuadre del estado de cuenta: Saldo = Total − Enganche − Abonos − Bonificaciones − Traspaso por renovación.
/// Toma los pagos de la venta y asigna cada movimiento no monetario a su concepto.
/// </summary>
public static class CuadreEstadoCuenta
{
    public static EstadoCuentaVentaDto ConMovimientos(EstadoCuentaVentaDto venta,
        IEnumerable<(long? IdMovimiento, decimal Monto)> pagos)
    {
        var lista = pagos.ToList();
        decimal Suma(long mov) => lista.Where(p => p.IdMovimiento == mov).Sum(p => p.Monto);
        return venta with
        {
            BonificacionRacha = Suma(MovimientoIds.BonificacionPromocion),
            LiquidacionPlan = Suma(MovimientoIds.BonificacionProntoPago),
            Condonacion = Suma(MovimientoIds.Ajuste),
            TraspasoRenovacion = Suma(MovimientoIds.LiquidacionRenovacion)
        };
    }

    /// <summary>Totales de bonificaciones y traspasos para la cabecera del estado de cuenta.</summary>
    public static EstadoCuentaResponse ConTotales(EstadoCuentaResponse ec) => ec with
    {
        TotalBonificaciones = ec.Ventas.Sum(v => v.TotalBonificaciones),
        TotalTraspasos = ec.Ventas.Sum(v => v.TraspasoRenovacion)
    };
}

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
    IReadOnlyList<EstadoCuentaPagoDto> Pagos)
{
    /// <summary>Suma de descuentos globales otorgados.</summary>
    public decimal TotalDescuentos { get; init; }
    /// <summary>Suma de enganches recibidos (TotalPagado solo contempla abonos).</summary>
    public decimal TotalEnganches { get; init; }
    /// <summary>Bonificaciones por racha, liquidación por plan y condonaciones por acuerdo.</summary>
    public decimal TotalBonificaciones { get; init; }
    /// <summary>Saldo traspasado a ventas nuevas por renovación.</summary>
    public decimal TotalTraspasos { get; init; }
}
