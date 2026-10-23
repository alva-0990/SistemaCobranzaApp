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

    /// <summary>
    /// Devolución del inventario de zona ocasionada
    /// por la cancelación de una venta.
    /// </summary>
    public const string DevolucionVenta = "DEVOLUCION_VENTA";
}

/// <summary>
/// Cálculo puro de existencias tras un movimiento (sin base de datos).
/// </summary>
public static class CalculoInventario
{
    public static int CalcularNuevaExistencia(
        int existenciaAnterior,
        int cantidad,
        bool esAjuste)
        => esAjuste
            ? cantidad
            : existenciaAnterior + cantidad;

    /// <summary>
    /// Existencia tras una salida (venta/envío): anterior - cantidad,
    /// con piso en 0.
    /// </summary>
    public static int CalcularSalida(
        int existenciaAnterior,
        int cantidad)
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

    public static ResultadoValidacion ValidarEntradaOAjuste(
        int cantidad,
        bool esAjuste)
    {
        if (esAjuste)
        {
            return cantidad < 0
                ? ResultadoValidacion.AjusteNegativo
                : ResultadoValidacion.Ok;
        }

        return cantidad <= 0
            ? ResultadoValidacion.CantidadInvalida
            : ResultadoValidacion.Ok;
    }

    public static string TipoCentral(bool esAjuste)
        => esAjuste
            ? TipoMovimientoInventario.AjusteCentral
            : TipoMovimientoInventario.EntradaCentral;

    public static string TipoZona(bool esAjuste)
        => esAjuste
            ? TipoMovimientoInventario.AjusteZona
            : TipoMovimientoInventario.EntradaZona;
}