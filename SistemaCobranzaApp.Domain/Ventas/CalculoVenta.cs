using SistemaCobranzaApp.Domain.Constantes;

namespace SistemaCobranzaApp.Domain.Ventas;

/// <summary>
/// Reglas puras de alta de venta, portadas de VentanaNuevaVenta (app WPF).
/// Sin dependencias de base de datos.
/// </summary>
public static class CalculoVenta
{
    /// <summary>Una línea de detalle para el cálculo (cantidad y precio).</summary>
    public readonly record struct LineaCalculo(int Cantidad, decimal PrecioCobrado)
    {
        public decimal Importe => Cantidad * PrecioCobrado;
    }

    /// <summary>Subtotal = suma de importes (cantidad * precio) de las líneas.</summary>
    public static decimal CalcularSubtotal(IEnumerable<LineaCalculo> lineas)
        => lineas.Sum(l => l.Importe);

    /// <summary>
    /// Saldo pagado inicial = enganche + abonos (0 en alta).
    /// </summary>
    public static decimal SaldoPagadoInicial(decimal enganche) => enganche;

    /// <summary>
    /// Saldo pendiente inicial = totalFinal - saldoPagado, con piso en 0.
    /// </summary>
    public static decimal SaldoPendienteInicial(decimal totalFinal, decimal enganche)
    {
        var saldo = totalFinal - SaldoPagadoInicial(enganche);
        return saldo < 0 ? 0m : saldo;
    }

    /// <summary>
    /// Estatus automático inicial de la venta, portado de VentanaNuevaVenta:
    ///  - saldoPendiente &lt;= 0        -> Pagada
    ///  - saldoPagado (enganche) &gt; 0 -> Abonando
    ///  - en otro caso                 -> Pendiente
    /// </summary>
    public static long DeterminarEstatusInicial(decimal totalFinal, decimal enganche)
    {
        var saldoPagado = SaldoPagadoInicial(enganche);
        var saldoPendiente = SaldoPendienteInicial(totalFinal, enganche);

        if (saldoPendiente <= 0) return EstatusVentaIds.Pagada;
        if (saldoPagado > 0) return EstatusVentaIds.Abonando;
        return EstatusVentaIds.Pendiente;
    }

    /// <summary>Formatea el código de venta: V + id a 7 dígitos (V0000123).</summary>
    public static string FormatearCodigoVenta(long idVenta) => $"V{idVenta:D7}";

    public enum ResultadoValidacion
    {
        Ok,
        SinLineas,
        LineaInvalida,
        EngancheNegativo,
        EngancheExcedeTotal,
        PagoRequeridoNegativo
    }

    /// <summary>
    /// Valida los datos de alta de la venta (sin tocar la base).
    /// </summary>
    public static ResultadoValidacion Validar(
        IReadOnlyList<LineaCalculo> lineas,
        decimal enganche,
        decimal? pagoRequerido)
    {
        if (lineas is null || lineas.Count == 0)
            return ResultadoValidacion.SinLineas;

        foreach (var l in lineas)
        {
            if (l.Cantidad <= 0 || l.PrecioCobrado < 0)
                return ResultadoValidacion.LineaInvalida;
        }

        if (enganche < 0)
            return ResultadoValidacion.EngancheNegativo;

        var total = CalcularSubtotal(lineas);
        if (enganche > total)
            return ResultadoValidacion.EngancheExcedeTotal;

        if (pagoRequerido is { } pr && pr < 0)
            return ResultadoValidacion.PagoRequeridoNegativo;

        return ResultadoValidacion.Ok;
    }
}
