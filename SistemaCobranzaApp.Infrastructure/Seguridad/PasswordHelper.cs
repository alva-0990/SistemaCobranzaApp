namespace SistemaCobranzaApp.Infrastructure.Seguridad;

/// <summary>
/// Verificación y creación de hashes BCrypt.
/// Portado de Utilerias/PasswordHelper.cs (app WPF), workFactor 12.
/// </summary>
public static class PasswordHelper
{
    private const int WorkFactor = 12;

    public static string CrearHash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return string.Empty;

        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public static bool VerificarPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
