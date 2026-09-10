namespace SistemaCobranzaApp.Domain.Inventario;

/// <summary>
/// Tipos de movimiento de inventario, portados de
/// InventarioSchema.TipoMovimiento (app WPF).
/// </summary>
public static class TipoMovimientoInventario
{
    public const string EntradaCentral = "ENTRADA_CENTRAL";
    public const string AjusteCentral = "AJUSTE_CENTRAL";
    public const string SalidaZona = "SALIDA_ZONA";
    public const string EntradaZona = "ENTRADA_ZONA";
    public const string AjusteZona = "AJUSTE_ZONA";
    public const string Venta = "VENTA";
}

/// <summary>
/// Cálculo puro de existencias tras un movimiento (sin base de datos).
/// </summary>
public static class CalculoInventario
{
    /// <summary>
    /// Existencia resultante:
    ///  - Ajuste: la existencia queda fijada a <paramref name="cantidad"/> (absoluto).
    ///  - Entrada: se suma <paramref name="cantidad"/> a la existencia anterior.
    /// </summary>
    public static int CalcularNuevaExistencia(int existenciaAnterior, int cantidad, bool esAjuste)
        => esAjuste ? cantidad : existenciaAnterior + cantidad;

    /// <summary>
    /// Existencia tras una salida (venta/envío): anterior - cantidad, con piso en 0.
    /// </summary>
    public static int CalcularSalida(int existenciaAnterior, int cantidad)
    {
        var nueva = existenciaAnterior - cantidad;
        return nueva < 0 ? 0 : nueva;
    }

    public enum ResultadoValidacion
    {
        Ok,
        CantidadInvalida,
        AjusteNegativo
    }

    /// <summary>
    /// Valida una entrada/ajuste:
    ///  - Entrada: la cantidad debe ser &gt; 0.
    ///  - Ajuste: la cantidad (existencia final) no puede ser negativa.
    /// </summary>
    public static ResultadoValidacion ValidarEntradaOAjuste(int cantidad, bool esAjuste)
    {
        if (esAjuste)
            return cantidad < 0 ? ResultadoValidacion.AjusteNegativo : ResultadoValidacion.Ok;

        return cantidad <= 0 ? ResultadoValidacion.CantidadInvalida : ResultadoValidacion.Ok;
    }

    /// <summary>Tipo de movimiento para central según sea ajuste o entrada.</summary>
    public static string TipoCentral(bool esAjuste)
        => esAjuste ? TipoMovimientoInventario.AjusteCentral : TipoMovimientoInventario.EntradaCentral;

    /// <summary>Tipo de movimiento para zona según sea ajuste o entrada.</summary>
    public static string TipoZona(bool esAjuste)
        => esAjuste ? TipoMovimientoInventario.AjusteZona : TipoMovimientoInventario.EntradaZona;
}
