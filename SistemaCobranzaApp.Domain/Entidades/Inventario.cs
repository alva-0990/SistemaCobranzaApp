namespace SistemaCobranzaApp.Domain.Entidades;

public class InventarioCentral
{
    public long IdInventarioCentral { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }

    public Producto? Producto { get; set; }
}

public class InventarioZona
{
    public long IdInventarioZona { get; set; }
    public long IdZona { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }

    public Zona? Zona { get; set; }
    public Producto? Producto { get; set; }
}

public class InventarioMovimiento
{
    public long IdMovimientoInv { get; set; }
    public long IdProducto { get; set; }
    public string TipoMovimiento { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int ExistenciaAnterior { get; set; }
    public int ExistenciaNueva { get; set; }
    public long? IdZonaOrigen { get; set; }
    public long? IdZonaDestino { get; set; }
    public long? IdPropuesta { get; set; }
    public long? IdVenta { get; set; }
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public DateTimeOffset FechaMovimiento { get; set; }

    public Producto? Producto { get; set; }
}
