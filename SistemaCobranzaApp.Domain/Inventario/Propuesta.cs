namespace SistemaCobranzaApp.Domain.Inventario;

/// <summary>
/// Estatus de una propuesta de envío, portados de
/// InventarioSchema.EstatusPropuesta (app WPF).
/// </summary>
public static class EstatusPropuesta
{
    public const string Pendiente = "PENDIENTE";
    public const string Confirmada = "CONFIRMADA";
    public const string Rechazada = "RECHAZADA";
}

/// <summary>
/// Reglas puras de propuestas de envío central→zona (sin base de datos).
/// </summary>
public static class CalculoPropuesta
{
    public readonly record struct LineaPropuesta(long IdProducto, int Cantidad);

    public enum ResultadoValidacion
    {
        Ok,
        ZonaInvalida,
        SinLineas,
        CantidadInvalida,
        ProductoDuplicado
    }

    /// <summary>
    /// Valida los datos de una propuesta: zona válida, al menos una línea,
    /// cantidades positivas y sin productos repetidos.
    /// </summary>
    public static ResultadoValidacion Validar(long idZonaDestino, IReadOnlyList<LineaPropuesta> lineas)
    {
        if (idZonaDestino <= 0)
            return ResultadoValidacion.ZonaInvalida;

        if (lineas is null || lineas.Count == 0)
            return ResultadoValidacion.SinLineas;

        if (lineas.Any(l => l.Cantidad <= 0 || l.IdProducto <= 0))
            return ResultadoValidacion.CantidadInvalida;

        if (lineas.Select(l => l.IdProducto).Distinct().Count() != lineas.Count)
            return ResultadoValidacion.ProductoDuplicado;

        return ResultadoValidacion.Ok;
    }

    /// <summary>
    /// ¿Solo puede resolverse (confirmar/rechazar) si está PENDIENTE?
    /// Devuelve true si el estatus actual permite la resolución.
    /// </summary>
    public static bool PuedeResolver(string estatusActual)
        => string.Equals(estatusActual, EstatusPropuesta.Pendiente, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifica si hay stock central suficiente para una cantidad solicitada.
    /// </summary>
    public static bool StockSuficiente(int existenciaCentral, int cantidadSolicitada)
        => existenciaCentral >= cantidadSolicitada;
}
