using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Domain.Entidades;

public class Venta
{
    public long IdVenta { get; set; }
    public Guid? VentaUuid { get; set; }
    public long IdCliente { get; set; }
    public long? IdVendedor { get; set; }
    public string? CodigoVenta { get; set; }
    public DateTimeOffset FechaCompra { get; set; }
    public long IdCondicion { get; set; }

    // --- SECCIÓN FINANCIERA ---
    public decimal Subtotal { get; set; }
    public decimal DescuentoGlobal { get; set; }
    public decimal TotalFinal { get; set; }
    public decimal Enganche { get; set; }
    public decimal MontoFinanciado { get; private set; } // Propiedad calculada en BD (STORED)
    
    public decimal TotalComisionVenta { get; set; }

    // --- SECCIÓN DE CRÉDITO ACTUALIZADA ---
    public long? IdFrecuenciaPago { get; set; }
    public int NumeroCuotas { get; set; }
    public decimal MontoCuotaFija { get; set; } // Tu cuota exigida/pactada
    public decimal TasaInteres { get; set; }
    public DateTimeOffset? FechaPrimerVence { get; set; }

    // --- CONTROL DE SALDOS EN TIEMPO REAL (Actualizados por Trigger de BD) ---
    public decimal TotalAbonado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public DateTimeOffset? FechaUltimoPago { get; set; }
    
    public int PagosConsecutivosPuntuales { get; set; }
    
    public string? VentasUnificadas { get; set; }
    
    // --- ESTADOS Y AUDITORÍA ---
    public long IdEstatusVenta { get; set; }
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Cliente? Cliente { get; set; }
    public Vendedor? Vendedor { get; set; }
    public CatalogoCondicion? Condicion { get; set; }
    public CatalogoFrecuenciaPago? FrecuenciaPago { get; set; } // Nueva relación al catálogo
    public CatalogoEstatusVenta? EstatusVenta { get; set; }
    
    public List<DetalleVenta> Detalles { get; set; } = new();
    public List<VentaCuota> Cuotas { get; set; } = new(); // Nueva relación a la tabla de amortización
    public List<VentaPlan> PlanesPromocionales { get; set; } = new(); // Relación a planes por meses
    public List<Pago> Pagos { get; set; } = new();
}
