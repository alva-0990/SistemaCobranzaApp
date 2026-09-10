namespace SistemaCobranzaApp.Shared.Dtos;

// ================= Catálogos de configuración (CRUD admin) =================
// Aplica a: clasificaciones de cliente, condiciones de crédito, estatus de
// venta y tipos de movimiento/pago. Todos comparten forma (Id + Descripción +
// Activo) y baja lógica.

/// <summary>Ítem administrable de catálogo de configuración.</summary>
public record CatalogoConfigDto(long Id, string Descripcion, bool Activo);

/// <summary>Alta de un catálogo de configuración.</summary>
public record CrearCatalogoConfigRequest(string Descripcion);

/// <summary>Edición de un catálogo de configuración.</summary>
public record ActualizarCatalogoConfigRequest(string Descripcion);
