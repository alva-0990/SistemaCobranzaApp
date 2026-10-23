namespace SistemaCobranzaApp.Domain.Entidades;

public class Gasto
{
    public long IdGasto { get; set; }

    public long IdCategoriaGasto { get; set; }

    public string FolioGasto { get; set; } = string.Empty;

    public DateTimeOffset FechaGasto { get; set; }

    public string Concepto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Datos del proveedor
    public string? Proveedor { get; set; }
    public string? RFCProveedor { get; set; }
    public string? Referencia { get; set; }

    public decimal Importe { get; set; }

    // PENDIENTE, AUTORIZADO, PARCIAL, PAGADO, CANCELADO
    public string Estatus { get; set; } = "PENDIENTE";

    // Clasificación operativa opcional
    public long? IdZona { get; set; }
    public long? IdCobrador { get; set; }
    public long? IdVendedor { get; set; }

    // Cuenta prevista/sugerida de la que saldrá el dinero (informativa).
    // El pago real puede hacerse desde esta u otra cuenta en GastosPagos.
    public long? IdCuentaPrevista { get; set; }

    public string? Observaciones { get; set; }

    // Auditoría del registro
    public long IdUsuarioRegistro { get; set; }

    public long? IdUsuarioAutoriza { get; set; }
    public DateTimeOffset? FechaAutorizacion { get; set; }

    public long? IdUsuarioCancelacion { get; set; }
    public DateTimeOffset? FechaCancelacion { get; set; }

    public string? MotivoCancelacion { get; set; }

    public DateTimeOffset FechaAlta { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    // Navegación
    public CatalogoCategoriaGasto? Categoria { get; set; }

    public Zona? Zona { get; set; }
    public Cobrador? Cobrador { get; set; }
    public Vendedor? Vendedor { get; set; }

    public CuentaGasto? CuentaPrevista { get; set; }

    public ICollection<GastoPago> Pagos { get; set; } = new List<GastoPago>();

    public ICollection<GastoComprobante> Comprobantes { get; set; }
        = new List<GastoComprobante>();
}