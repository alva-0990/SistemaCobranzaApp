namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Sobre estándar de respuesta de la API.
/// Todas las respuestas (éxito o error) comparten esta forma.
/// </summary>
/// <typeparam name="T">Tipo del payload devuelto en <see cref="Resultado"/>.</typeparam>
public record ApiRespuesta<T>
{
    /// <summary>Código de respuesta (p. ej. "200", "400", "500").</summary>
    public required string Codigo { get; init; }

    /// <summary>Mensaje legible para el usuario.</summary>
    public required string Mensaje { get; init; }

    /// <summary>Folio único de la operación (para trazabilidad/logs).</summary>
    public string Folio { get; init; } = "";

    /// <summary>Información adicional opcional sobre el contexto de la operación.</summary>
    public string? Info { get; init; }

    /// <summary>Payload de la operación (nulo en errores).</summary>
    public T? Resultado { get; init; }

    /// <summary>Detalles adicionales, típicamente errores de validación o de negocio.</summary>
    public IReadOnlyList<string>? Detalles { get; init; }
}

/// <summary>
/// Versión sin payload, para operaciones que no devuelven datos
/// (por ejemplo, errores o confirmaciones sin cuerpo).
/// </summary>
public record ApiRespuesta : ApiRespuesta<object>;
