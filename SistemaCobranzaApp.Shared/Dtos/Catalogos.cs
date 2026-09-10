namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Ítem genérico de catálogo (id + descripción) para llenar combos.</summary>
public record CatalogoItemDto(long Id, string Descripcion);

/// <summary>Cobrador o vendedor asociado a una zona.</summary>
public record PersonaZonaDto(long Id, string Nombre, long? IdZona);

/// <summary>Ruta con su zona.</summary>
public record RutaDto(long IdRuta, string Descripcion, long? IdZona);
