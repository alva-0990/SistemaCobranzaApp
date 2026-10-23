namespace SistemaCobranzaApp.Domain.Entidades;

public class ConfiguracionComision
{
    public long IdConfiguracion { get; set; }
    public long IdProducto { get; set; }
    public long IdCatalogoComision { get; set; }
    public DateTimeOffset FechaAsignacion { get; set; }
    public long? IdUsuarioRegistro { get; set; }

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public Producto? Producto { get; set; }
    public CatalogoComision? CatalogoComision { get; set; }
    public Usuario? UsuarioRegistro { get; set; }
}