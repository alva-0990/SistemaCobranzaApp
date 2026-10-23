using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Api.Comun;

/// <summary>
/// Fábrica de resultados HTTP (<see cref="IResult"/>) que envuelven la respuesta
/// en el sobre estándar <see cref="ApiRespuesta{T}"/>.
/// </summary>
public static class Respuestas
{
    /// <summary>Genera un folio único para trazabilidad.</summary>
    public static string NuevoFolio() =>
        $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

    // ---------------------------------------------------------------------
    // Éxito
    // ---------------------------------------------------------------------

    public static IResult Ok<T>(T datos, string mensaje = "Operación exitosa.", string? info = null) =>
        Results.Ok(new ApiRespuesta<T>
        {
            Codigo = CodigosApi.Exito,
            Mensaje = mensaje,
            Folio = NuevoFolio(),
            Info = info,
            Resultado = datos
        });

    public static IResult Creado<T>(string uri, T datos, string mensaje = "Recurso creado correctamente.") =>
        Results.Created(uri, new ApiRespuesta<T>
        {
            Codigo = CodigosApi.Creado,
            Mensaje = mensaje,
            Folio = NuevoFolio(),
            Resultado = datos
        });

    // ---------------------------------------------------------------------
    // Errores
    // ---------------------------------------------------------------------

    public static IResult Error(
        int http,
        string codigo,
        string mensaje,
        string? info = null,
        IReadOnlyList<string>? detalles = null) =>
        Results.Json(new ApiRespuesta
        {
            Codigo = codigo,
            Mensaje = mensaje,
            Folio = NuevoFolio(),
            Info = info,
            Detalles = detalles
        }, statusCode: http);

    public static IResult SolicitudInvalida(string mensaje, params string[] detalles) =>
        Error(400, CodigosApi.SolicitudInvalida, mensaje,
            detalles: detalles.Length > 0 ? detalles : null);

    public static IResult NoEncontrado(string mensaje = "Recurso no encontrado.", params string[] detalles) =>
        Error(404, CodigosApi.NoEncontrado, mensaje,
            detalles: detalles.Length > 0 ? detalles : null);

    public static IResult Conflicto(string mensaje, params string[] detalles) =>
        Error(409, CodigosApi.Conflicto, mensaje,
            detalles: detalles.Length > 0 ? detalles : null);

    public static IResult ErrorInterno(
        string mensaje = "Error interno del servidor.",
        string? info = null,
        params string[] detalles) =>
        Error(500, CodigosApi.ErrorInterno, mensaje, info,
            detalles.Length > 0 ? detalles : null);
}
