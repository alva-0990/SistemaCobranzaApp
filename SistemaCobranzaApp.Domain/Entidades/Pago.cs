namespace SistemaCobranzaApp.Domain.Entidades;

public class Pago
{
    public long IdPago { get; set; }
    public Guid PagoUuid { get; set; } = Guid.NewGuid();
    public long IdVenta { get; set; }
    public long IdCliente { get; set; }
    public decimal SaldoAnterior { get; set; } // Seteado automáticamente por el Trigger
    public decimal SaldoActual { get; set; }   // Seteado automáticamente por el Trigger
    public decimal MontoAbona { get; set; }
    public long? IdCobrador { get; set; }
    public DateTimeOffset FechaPago { get; set; } = DateTimeOffset.UtcNow;
    public long? IdMovimiento { get; set; }
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public string? FolioPago { get; set; }
    public int? ConsecutivoPago { get; set; }
    public DateTimeOffset FechaModificacion { get; set; } = DateTimeOffset.UtcNow;

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Venta? Venta { get; set; }
    public Cliente? Cliente { get; set; }
    public Cobrador? Cobrador { get; set; }
    public CatalogoMovimientos? Movimiento { get; set; }
}