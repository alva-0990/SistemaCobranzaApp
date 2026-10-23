namespace SistemaCobranzaApp.Domain.Entidades;

public class ComisionPorPagar
{
    public long IdComision { get; set; }
    public long IdVenta { get; set; }
    public long IdDetalle { get; set; }
    public long IdVendedor { get; set; }
    public decimal MontoComision { get; set; }
    public string EstatusComision { get; set; } = "PENDIENTE"; // 'PENDIENTE', 'DISPONIBLE', 'PAGADA', 'CANCELADA'
    public DateTimeOffset FechaCalculo { get; set; }
    public DateTimeOffset? FechaPago { get; set; }
    public long? IdUsuarioPago { get; set; }

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Venta? Venta { get; set; }
    public DetalleVenta? DetalleVenta { get; set; }
    public Vendedor? Vendedor { get; set; }
    public Usuario? UsuarioPago { get; set; }
}