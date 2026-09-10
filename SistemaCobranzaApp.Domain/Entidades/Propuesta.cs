namespace SistemaCobranzaApp.Domain.Entidades;

public class PropuestaEnvio
{
    public long IdPropuesta { get; set; }
    public long IdZonaDestino { get; set; }
    public string Estatus { get; set; } = "PENDIENTE";
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public long? IdUsuarioConfirma { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? FechaResolucion { get; set; }

    public Zona? ZonaDestino { get; set; }
    public List<PropuestaEnvioDetalle> Detalles { get; set; } = new();
}

public class PropuestaEnvioDetalle
{
    public long IdPropuestaDetalle { get; set; }
    public long IdPropuesta { get; set; }
    public long IdProducto { get; set; }
    public int Cantidad { get; set; }

    public PropuestaEnvio? Propuesta { get; set; }
    public Producto? Producto { get; set; }
}
