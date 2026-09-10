namespace SistemaCobranzaApp.Domain.Seguridad;

/// <summary>
/// Validación pura de datos de usuario y rol (sin base de datos).
/// </summary>
public static class ValidacionUsuario
{
    public const int MaxUsuario = 50;
    public const int MaxNombre = 100;
    public const int MinPassword = 6;

    /// <summary>
    /// Valida los campos comunes de usuario.
    /// <paramref name="password"/>:
    ///  - en alta (requerirPassword=true) es obligatoria y con longitud mínima;
    ///  - en edición (false) es opcional, pero si viene debe cumplir la longitud.
    /// </summary>
    public static string? Validar(
        string? usuario,
        string? nombreCompleto,
        string? password,
        bool requerirPassword)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            return "El nombre de usuario es obligatorio.";
        if (usuario.Trim().Length > MaxUsuario)
            return $"El usuario no puede exceder {MaxUsuario} caracteres.";

        if (string.IsNullOrWhiteSpace(nombreCompleto))
            return "El nombre completo es obligatorio.";
        if (nombreCompleto.Trim().Length > MaxNombre)
            return $"El nombre no puede exceder {MaxNombre} caracteres.";

        var tienePassword = !string.IsNullOrWhiteSpace(password);
        if (requerirPassword && !tienePassword)
            return "La contraseña es obligatoria.";

        if (tienePassword && password!.Length < MinPassword)
            return $"La contraseña debe tener al menos {MinPassword} caracteres.";

        return null;
    }
}

/// <summary>Validación pura de rol.</summary>
public static class ValidacionRol
{
    public const int MaxDescripcion = 50;

    public static string? Validar(string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
            return "La descripción del rol es obligatoria.";
        if (descripcion.Trim().Length > MaxDescripcion)
            return $"La descripción no puede exceder {MaxDescripcion} caracteres.";
        return null;
    }
}
