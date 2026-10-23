namespace SistemaCobranzaApp.Domain.Entidades;

/// <summary>
/// Parámetro configurable del sistema (tabla ParametrosSistema).
/// La clave es la llave primaria (texto). Puede llevar un valor numérico y/o un valor de texto,
/// según el parámetro. Lo usan, entre otros, los triggers de promociones de cobranza
/// (p. ej. PROMO_PAGOS_CONSECUTIVOS_ACTIVA / PROMO_PAGOS_CONSECUTIVOS_META).
/// </summary>
public class ParametroSistema
{
    public string Clave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal? ValorNumerico { get; set; }
    public string? ValorTexto { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }
}
