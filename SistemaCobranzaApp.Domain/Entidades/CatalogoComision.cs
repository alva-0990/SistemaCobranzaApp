namespace SistemaCobranzaApp.Domain.Entidades;

public class CatalogoComision
{
    public long IdCatalogoComision { get; set; }
    public string NombreEsquema { get; set; } = string.Empty;
    public string TipoCalculo { get; set; } = string.Empty; // 'PORCENTAJE' o 'FIJO'
    public decimal ComisionContado { get; set; }
    public decimal ComisionCredito { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset FechaRegistro { get; set; }

    // --- PROPIEDADES DE NAVEGACIÓN ---
    public ICollection<ConfiguracionComision> ConfiguracionesComisiones { get; set; } = new List<ConfiguracionComision>();
}