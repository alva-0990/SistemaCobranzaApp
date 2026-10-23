namespace SistemaCobranzaApp.Domain.Entidades;

public class GastoPago
{
    public long IdPagoGasto { get; set; }

    public long IdGasto { get; set; }
    public long IdCuentaGasto { get; set; }

    public DateTimeOffset FechaPago { get; set; }

    public decimal Importe { get; set; }

    // EFECTIVO, TRANSFERENCIA, TARJETA, CHEQUE, OTRO
    public string FormaPago { get; set; } = "EFECTIVO";

    public string? Referencia { get; set; }
    public string? Observaciones { get; set; }

    public long IdUsuarioRegistro { get; set; }

    public DateTimeOffset FechaAlta { get; set; }

    // Navegación
    public Gasto? Gasto { get; set; }
    public CuentaGasto? Cuenta { get; set; }

    public MovimientoCuentaGasto? MovimientoCuenta { get; set; }
}