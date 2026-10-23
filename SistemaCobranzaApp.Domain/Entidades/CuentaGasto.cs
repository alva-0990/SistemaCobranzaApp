namespace SistemaCobranzaApp.Domain.Entidades;

public class CuentaGasto
{
    public long IdCuentaGasto { get; set; }

    public string Nombre { get; set; } = string.Empty;

    // EFECTIVO, CAJA_CHICA, BANCO, OTRO
    public string TipoCuenta { get; set; } = "EFECTIVO";

    public string Moneda { get; set; } = "MXN";

    public decimal SaldoInicial { get; set; }

    // 0 = Inactiva, 1 = Activa
    public int Estatus { get; set; } = 1;

    public string? Observaciones { get; set; }

    public DateTimeOffset FechaAlta { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    // Navegación
    public ICollection<GastoPago> Pagos { get; set; } = new List<GastoPago>();

    public ICollection<MovimientoCuentaGasto> Movimientos { get; set; }
        = new List<MovimientoCuentaGasto>();
}