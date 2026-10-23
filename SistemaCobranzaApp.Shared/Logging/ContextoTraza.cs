using SistemaCobranzaApp.Domain.Comun;
namespace SistemaCobranzaApp.Shared.Logging;

/// <summary>
/// Contexto de traza con ámbito por petición (scoped).
/// Guarda el <c>id_traza</c> generado al inicio del request para que todos
/// los logs de esa misma petición —en cualquier capa— compartan el mismo
/// identificador.
///
/// Vive en <c>Shared</c> para que todas las capas (Application, Infrastructure,
/// Api) puedan recibirlo por inyección de dependencias.
/// </summary>
public class ContextoTraza
{
    /// <summary>
    /// Identificador de traza de la petición actual.
    /// Se asigna una vez (en el middleware) y se lee a lo largo del proceso.
    /// </summary>
    public string IdTraza { get; set; } = GenerarId();

    /// <summary>
    /// Genera un identificador de traza único, ordenable cronológicamente y legible:
    /// marca de tiempo + sufijo aleatorio corto. Ej.: "20260929233625123-A7F3".
    /// </summary>
    public static string GenerarId()
    {
        var marca = FechaProyecto.Ahora.DateTime.ToString("yyyyMMddHHmmssfff");
        var sufijo = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"{marca}-{sufijo}";
    }
}
