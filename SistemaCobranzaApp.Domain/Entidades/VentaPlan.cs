namespace SistemaCobranzaApp.Domain.Entidades;

public class VentaPlan
{
    public long IdPlan { get; set; }
    public long IdVenta { get; set; }
    public int NumeroMeses { get; set; }
    public decimal MontoLiquidacion { get; set; }
    public bool Activo { get; set; } = true;

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Venta? Venta { get; set; }
}