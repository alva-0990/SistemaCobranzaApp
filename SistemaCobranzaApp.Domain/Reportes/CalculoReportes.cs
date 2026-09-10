namespace SistemaCobranzaApp.Domain.Reportes;

/// <summary>
/// Utilidades puras para reportes (sin base de datos).
/// </summary>
public static class CalculoReportes
{
    /// <summary>
    /// Normaliza un rango de fechas para filtros:
    ///  - Si no hay 'desde', usa DateTimeOffset.MinValue.
    ///  - Si no hay 'hasta', usa DateTimeOffset.MaxValue.
    ///  - 'hasta' se lleva al FIN del día (23:59:59.999...) para que sea inclusivo.
    ///  - Si desde > hasta, se intercambian.
    /// </summary>
    public static (DateTimeOffset desde, DateTimeOffset hasta) NormalizarRango(
        DateTimeOffset? desde, DateTimeOffset? hasta)
    {
        var d = desde ?? DateTimeOffset.MinValue;

        DateTimeOffset h;
        if (hasta is { } hv)
        {
            // Fin del día para incluir toda la jornada.
            var fin = hv.Date.AddDays(1).AddTicks(-1);
            h = new DateTimeOffset(fin, hv.Offset);
        }
        else
        {
            h = DateTimeOffset.MaxValue;
        }

        return d <= h ? (d, h) : (h, d);
    }

    /// <summary>
    /// Porcentaje de recuperación = cobrado / esperado * 100, con piso/tope
    /// razonables y protección contra división por cero.
    /// </summary>
    public static decimal PorcentajeRecuperacion(decimal cobrado, decimal esperado)
    {
        if (esperado <= 0) return 0m;
        var pct = cobrado / esperado * 100m;
        return pct < 0 ? 0m : pct;
    }
}
