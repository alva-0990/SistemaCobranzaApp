namespace SistemaCobranzaApp.Domain.Entidades;

public class DetalleVenta
{
    public long IdDetalle { get; set; }
    public long IdVenta { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioCobrado { get; set; }
    public decimal Importe { get; set; }
    public decimal PorcentajeComisionAplicado { get; set; }
    public decimal MontoComisionGenerado { get; set; }

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Venta? Venta { get; set; }
    public Producto? Producto { get; set; }
}