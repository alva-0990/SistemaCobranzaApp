using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Domain.Entidades;

public class Producto
{
    public long IdProducto { get; set; }
    public string CodigoClave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal PrecioSugerido { get; set; }
    public decimal PrecioMinimo { get; set; }
    public int StockMini { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class Venta
{
    public long IdVenta { get; set; }
    public long IdCliente { get; set; }
    public long? IdVendedor { get; set; }
    public DateTimeOffset FechaCompra { get; set; }
    public DateTimeOffset? FechaUltimoPago { get; set; }
    public decimal Subtotal { get; set; }
    public decimal? PagoRequerido { get; set; }
    public decimal Enganche { get; set; }
    public decimal TotalAbonos { get; set; }
    public decimal SaldoPagado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public decimal SaldoPagadoDesc { get; set; }
    public decimal TotalFinal { get; set; }
    public long IdCondicion { get; set; }
    public long IdEstatusVenta { get; set; }
    public string? CodigoVenta { get; set; }
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    public Cliente? Cliente { get; set; }
    public Vendedor? Vendedor { get; set; }
    public CatalogoCondicion? Condicion { get; set; }
    public CatalogoEstatusVenta? EstatusVenta { get; set; }
    public List<DetalleVenta> Detalles { get; set; } = new();
    public List<Pago> Pagos { get; set; } = new();
}

public class DetalleVenta
{
    public long IdDetalle { get; set; }
    public long IdVenta { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioCobrado { get; set; }
    public decimal Importe { get; set; }

    public Venta? Venta { get; set; }
    public Producto? Producto { get; set; }
}

public class Pago
{
    public long IdPago { get; set; }
    public Guid PagoUuid { get; set; }
    public long IdVenta { get; set; }
    public long IdCliente { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoActual { get; set; }
    public decimal MontoAbona { get; set; }
    public long? IdCobrador { get; set; }
    public DateTimeOffset FechaPago { get; set; }
    public long? IdMovimiento { get; set; }
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public string? FolioPago { get; set; }
    public int? ConsecutivoPago { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    public Venta? Venta { get; set; }
    public Cliente? Cliente { get; set; }
    public Cobrador? Cobrador { get; set; }
    public CatalogoMovimientos? Movimiento { get; set; }
}

public class VentaPlan
{
    public long IdPlan { get; set; }
    public long IdVenta { get; set; }
    public int NumeroMeses { get; set; }
    public decimal MontoLiquidacion { get; set; }
    public bool Activo { get; set; } = true;

    public Venta? Venta { get; set; }
}
