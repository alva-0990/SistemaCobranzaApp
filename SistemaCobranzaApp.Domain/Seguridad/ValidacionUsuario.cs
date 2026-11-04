namespace SistemaCobranzaApp.Domain.Seguridad;

/// <summary>
/// Validación pura de datos de usuario y rol (sin base de datos).
/// </summary>
public static class ValidacionUsuario
{
    public const int MaxUsuario = 50;
    public const int MaxNombre = 100;
    public const int MinPassword = 8;

    /// <summary>Texto de ayuda con los requisitos de una contraseña segura (para mostrar en pantalla).</summary>
    public const string RequisitosPassword =
        "Mínimo 8 caracteres, con mayúscula, minúscula, número y símbolo, sin espacios.";

    /// <summary>
    /// Valida que la contraseña sea segura. Devuelve el primer requisito que no cumple
    /// o null si es válida. Se usa igual en la API y en los formularios.
    /// </summary>
    public static string? ValidarPassword(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return "La contraseña es obligatoria.";
        if (password.Length < MinPassword)
            return $"La contraseña debe tener al menos {MinPassword} caracteres.";
        if (password.Any(char.IsWhiteSpace))
            return "La contraseña no debe contener espacios.";
        if (!password.Any(char.IsUpper))
            return "La contraseña debe incluir al menos una letra mayúscula.";
        if (!password.Any(char.IsLower))
            return "La contraseña debe incluir al menos una letra minúscula.";
        if (!password.Any(char.IsDigit))
            return "La contraseña debe incluir al menos un número.";
        if (password.All(char.IsLetterOrDigit))
            return "La contraseña debe incluir al menos un símbolo (por ejemplo ! @ # $ % *).";
        return null;
    }

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

        if (tienePassword)
            return ValidarPassword(password);

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
