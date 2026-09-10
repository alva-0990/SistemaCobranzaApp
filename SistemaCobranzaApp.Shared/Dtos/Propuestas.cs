namespace SistemaCobranzaApp.Shared.Dtos;

public record CrearPropuestaDetalleRequest(long IdProducto, int Cantidad);

public record CrearPropuestaRequest(
    long IdZonaDestino,
    string? Observaciones,
    long? IdUsuarioRegistro,
    IReadOnlyList<CrearPropuestaDetalleRequest> Detalles);

public record PropuestaDetalleDto(
    long IdPropuestaDetalle,
    long IdProducto,
    string? Producto,
    int Cantidad);

public record PropuestaDto(
    long IdPropuesta,
    long IdZonaDestino,
    string? ZonaDestino,
    string Estatus,
    string? Observaciones,
    DateTimeOffset FechaCreacion,
    DateTimeOffset? FechaResolucion,
    IReadOnlyList<PropuestaDetalleDto> Detalles);

/// <summary>Resolución de una propuesta (confirmar/rechazar).</summary>
public record ResolverPropuestaRequest(long? IdUsuarioConfirma);
