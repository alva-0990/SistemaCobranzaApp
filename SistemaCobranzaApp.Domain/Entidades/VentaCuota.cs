namespace SistemaCobranzaApp.Domain.Entidades;

public class VentaCuota
{
    public long IdCuota { get; set; }
    public long IdVenta { get; set; }
    public int NumeroCuota { get; set; }
    public DateTimeOffset FechaVencimiento { get; set; }
    public decimal MontoCuota { get; set; }
    public decimal MontoInteres { get; set; }
    public decimal SaldoCuota { get; set; }
    public string EstatusCuota { get; set; } = "PENDIENTE"; // 'PENDIENTE', 'PAGADA', 'VENCIDA'

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Venta? Venta { get; set; }
}