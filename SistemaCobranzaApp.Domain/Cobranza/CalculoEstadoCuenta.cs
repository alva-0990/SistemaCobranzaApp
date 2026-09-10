namespace SistemaCobranzaApp.Domain.Cobranza;

/// <summary>
/// Agregación pura de totales del estado de cuenta de un cliente
/// (sin base de datos).
/// </summary>
public static class CalculoEstadoCuenta
{
    public readonly record struct VentaResumen(decimal TotalFinal, decimal Enganche, decimal TotalAbonos)
    {
        /// <summary>Monto ya pagado de la venta = enganche + abonos.</summary>
        public decimal Pagado => Enganche + TotalAbonos;

        /// <summary>Saldo pendiente = total - pagado, con piso en 0.</summary>
        public decimal Saldo
        {
            get
            {
                var s = TotalFinal - Pagado;
                return s < 0 ? 0m : s;
            }
        }
    }

    public record Totales(decimal TotalVendido, decimal TotalPagado, decimal SaldoTotal);

    /// <summary>
    /// Calcula los totales consolidados a partir de las ventas del cliente.
    /// </summary>
    public static Totales CalcularTotales(IEnumerable<VentaResumen> ventas)
    {
        decimal vendido = 0, pagado = 0, saldo = 0;
        foreach (var v in ventas)
        {
            vendido += v.TotalFinal;
            pagado += v.Pagado;
            saldo += v.Saldo;
        }
        return new Totales(vendido, pagado, saldo);
    }
}
