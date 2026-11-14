namespace SistemaCobranzaApp.Domain.Entidades;

/// <summary>
/// Rastro de una renovación de crédito: el saldo de la venta vieja se traspasa a la venta nueva.
/// Lo inserta el procedimiento pr_unificar_cuentas_ventas (la tabla solo se lee desde la aplicación).
/// </summary>
public class VentaUnificacion
{
    public long IdUnificacion { get; set; }
    public long IdVentaNueva { get; set; }
    public long IdVentaVieja { get; set; }
    public decimal MontoTraspasado { get; set; }
    public string CodigoLiquidacion { get; set; } = string.Empty;
    public DateTimeOffset FechaUnificacion { get; set; }
    public long IdUsuarioRegistro { get; set; }
}
