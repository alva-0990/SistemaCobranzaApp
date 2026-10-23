namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Parámetro configurable del sistema para lectura/listados.</summary>
public record ParametroSistemaDto(
    string Clave,
    string Descripcion,
    decimal? ValorNumerico,
    string? ValorTexto,
    DateTimeOffset FechaModificacion);

/// <summary>Alta de un parámetro del sistema. La clave se normaliza en mayúsculas.</summary>
public record CrearParametroSistemaRequest(
    string Clave,
    string Descripcion,
    decimal? ValorNumerico,
    string? ValorTexto);

/// <summary>
/// Actualización de un parámetro existente. La clave no se cambia (viaja por la ruta);
/// solo se actualizan la descripción y los valores.
/// </summary>
public record ActualizarParametroSistemaRequest(
    string Descripcion,
    decimal? ValorNumerico,
    string? ValorTexto);
