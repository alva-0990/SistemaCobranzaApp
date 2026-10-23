namespace SistemaCobranzaApp.Domain.Entidades;

public class CatalogoCategoriaGasto
{
    public long IdCategoriaGasto { get; set; }

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public bool RequiereComprobante { get; set; }

    // 0 = Inactivo, 1 = Activo
    public int Estatus { get; set; } = 1;

    public DateTimeOffset FechaAlta { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    // Navegación
    public ICollection<Gasto> Gastos { get; set; } = new List<Gasto>();
}