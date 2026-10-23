namespace SistemaCobranzaApp.Domain.Entidades;

public class MovimientoCuentaGasto
{
    public long IdMovimientoCuenta { get; set; }

    public long IdCuentaGasto { get; set; }
    public long? IdPagoGasto { get; set; }

    // ENTRADA o SALIDA
    public string TipoMovimiento { get; set; } = "SALIDA";

    public decimal Importe { get; set; }

    public DateTimeOffset FechaMovimiento { get; set; }

    public string Concepto { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }

    public long IdUsuarioRegistro { get; set; }

    // Navegación
    public CuentaGasto? Cuenta { get; set; }
    public GastoPago? Pago { get; set; }
}