using System.Text.Json.Serialization;

namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Niveles de log soportados. El texto emitido en el JSON coincide con el nombre.
/// </summary>
public enum NivelLog
{
    DEBUG,
    INFO,
    WARN,
    ERROR
}

/// <summary>
/// Contenido de la propiedad <c>log_data</c> del registro de log.
/// Se serializa exactamente con estos nombres de campo (JSON en una sola línea).
/// </summary>
public record LogData
{
    /// <summary>Fecha/hora del evento con formato "yyyy-MM-dd HH:mm:ss,fff".</summary>
    [JsonPropertyName("fecha")]
    public required string Fecha { get; init; }

    /// <summary>Nivel del evento (DEBUG, INFO, WARN, ERROR).</summary>
    [JsonPropertyName("Level")]
    public required string Level { get; init; }

    /// <summary>Identificador de traza propagado a lo largo de toda la petición.</summary>
    [JsonPropertyName("id_traza")]
    public required string IdTraza { get; init; }

    /// <summary>Nombre lógico del proceso/operación (definido manualmente).</summary>
    [JsonPropertyName("proceso")]
    public required string Proceso { get; init; }

    /// <summary>Mensaje descriptivo del evento.</summary>
    [JsonPropertyName("Mensaje")]
    public string Mensaje { get; init; } = "";

    /// <summary>Método desde el que se emitió el log (autocompletado).</summary>
    [JsonPropertyName("metodo")]
    public required string Metodo { get; init; }

    /// <summary>Línea de código desde la que se emitió el log (autocompletado).</summary>
    [JsonPropertyName("linea")]
    public int Linea { get; init; }

    /// <summary>
    /// Datos asociados al evento (payload consultado/creado/modificado), como
    /// JSON real anidado. Se omite del JSON cuando es null.
    /// </summary>
    [JsonPropertyName("datos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Datos { get; init; }
}

/// <summary>
/// Sobre del registro de log: <c>{"log_data":{...}}</c>.
/// </summary>
public record RegistroLog
{
    [JsonPropertyName("log_data")]
    public required LogData LogData { get; init; }
}
