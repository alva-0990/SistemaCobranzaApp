namespace SistemaCobranzaApp.Domain.Catalogos;

/// <summary>
/// Validación pura de entradas de catálogos administrables (sin base de datos).
/// </summary>
public static class ValidacionCatalogo
{
    public const int MaxTexto = 100;

    /// <summary>
    /// Valida un texto obligatorio (descripción o nombre). Devuelve null si es
    /// válido, o el mensaje de error. <paramref name="campo"/> se usa en el mensaje.
    /// </summary>
    public static string? ValidarTextoRequerido(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return $"{campo} es obligatorio.";

        if (valor.Trim().Length > MaxTexto)
            return $"{campo} no puede exceder {MaxTexto} caracteres.";

        return null;
    }

    /// <summary>
    /// Valida un IdZona opcional: si viene, debe ser &gt; 0.
    /// (La existencia real se valida contra la base.)
    /// </summary>
    public static string? ValidarZonaOpcional(long? idZona)
    {
        if (idZona is { } z && z <= 0)
            return "La zona seleccionada no es válida.";
        return null;
    }
}
