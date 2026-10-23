namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Ítem genérico de catálogo (id + descripción) para llenar combos.
/// </summary>
public record CatalogoItemDto(
    long Id,
    string Descripcion
);

/// <summary>
/// Cobrador o vendedor asociado a una zona.
/// Se utiliza para consultas y selección en combos.
/// </summary>
public record PersonaZonaDto(
    long Id,
    string Nombre,
    long? IdZona
);

/// <summary>
/// Cobrador o vendedor para administración.
/// Incluye su estado activo/inactivo.
/// </summary>
public record PersonaZonaAdminDto(
    long Id,
    string Nombre,
    long? IdZona,
    bool Activo
);

/// <summary>
/// Ruta con su zona.
/// </summary>
public record RutaDto(
    long IdRuta,
    string Descripcion,
    long? IdZona
);

/// <summary>
/// Datos para crear un cobrador o vendedor.
/// </summary>
public record CrearPersonaZonaRequest(
    string Nombre,
    long? IdZona
);

/// <summary>
/// Datos para actualizar un cobrador o vendedor.
/// </summary>
public record ActualizarPersonaZonaRequest(
    string Nombre,
    long? IdZona
);

