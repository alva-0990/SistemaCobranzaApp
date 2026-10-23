namespace SistemaCobranzaApp.Domain.Comun;

/// <summary>
/// Zona de negocio compartida por API, web y móvil. La persistencia usa UTC;
/// los calendarios, filtros por día y pantallas usan esta zona explícitamente.
/// </summary>
public static class FechaProyecto
{
    public const string IdZona = "America/Mexico_City";

    private static readonly Lazy<TimeZoneInfo> Zona = new(() => TimeZoneInfo.FindSystemTimeZoneById(IdZona));

    public static DateTimeOffset Ahora => DateTimeOffset.UtcNow.EnZonaProyecto();
    public static DateTime Hoy => Ahora.Date;

    public static DateTimeOffset EnZonaProyecto(this DateTimeOffset instante)
        => TimeZoneInfo.ConvertTime(instante, Zona.Value);

    /// <summary>Interpreta una fecha de calendario en la zona del proyecto, independientemente del equipo.</summary>
    public static DateTimeOffset DesdeFechaLocal(DateTime fecha)
    {
        var local = DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zona.Value.GetUtcOffset(local));
    }

    public static DateTimeOffset InicioDiaUtc(DateTime fecha)
        => DesdeFechaLocal(fecha.Date).ToUniversalTime();

    public static DateTimeOffset FinDiaUtc(DateTime fecha)
        => DesdeFechaLocal(fecha.Date.AddDays(1)).ToUniversalTime().AddTicks(-1);
}
