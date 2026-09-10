namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Usuarios ----

/// <summary>Alta de usuario. La contraseña se envía en claro y el servidor la hashea (BCrypt).</summary>
public record CrearUsuarioRequest(
    string Usuario,
    string Password,
    string NombreCompleto,
    long? IdRol);

/// <summary>
/// Edición de usuario. Password es opcional: si viene no vacía, se cambia;
/// si es null/vacía, se conserva la actual.
/// </summary>
public record ActualizarUsuarioRequest(
    string Usuario,
    string NombreCompleto,
    long? IdRol,
    string? Password);

/// <summary>Usuario expuesto al cliente. NUNCA incluye PasswordHash.</summary>
public record UsuarioDto(
    long IdUsuario,
    string Usuario,
    string NombreCompleto,
    long? IdRol,
    string? Rol,
    bool Activo,
    DateTimeOffset FechaAlta);

// ---- Roles ----

public record CrearRolRequest(string Descripcion);
public record ActualizarRolRequest(string Descripcion);
public record RolDto(long IdRol, string Descripcion, bool Activo);
