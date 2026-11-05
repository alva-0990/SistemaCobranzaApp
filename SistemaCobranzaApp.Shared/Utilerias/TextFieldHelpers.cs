using System.Text.RegularExpressions;

namespace SistemaCobranzaApp.Shared.Utilerias;

// Modos de filtrado de un campo de texto. El nombre (en minúscula) debe
// coincidir con las claves del objeto PATRONES en wwwroot/solo-alfanumerico.js.
public enum ModoCampo
{
    Texto,        // letras y espacios
    TextoGuionBajo, // letras y _  (sin espacios ni dígitos)
    Numeros,      // solo dígitos
    Alfanumerico, // letras, dígitos y espacios
    AlfanumericoGuion, // letras, dígitos, espacios y -
    AlfanumericoGuiones, // letras, dígitos, espacios y - _ [ ]
    Direccion,    // letras, dígitos, espacios y , . - _ # ( )
    Correo,       // letras, dígitos y @ . - _
    Codigo,       // letras, dígitos y -  (p. ej. códigos de venta)
    FrecuenciaTexto, // letras, dígitos, espacios y % . : ; , - _
    Observaciones // letras, dígitos, espacios y , . - ; : _ [ ] ( )
}

// Utilerías genéricas para campos de texto filtrados.
public static class TextFieldHelpers
{
    // Patrones de caracteres PERMITIDOS, equivalentes a los de solo-alfanumerico.js.
    // Se usan para saneo/validación del lado C# (respaldo del filtro de teclado).
    private static readonly Dictionary<ModoCampo, Regex> Patrones = new()
    {
        [ModoCampo.Texto]        = new(@"^[\p{L}\s]*$", RegexOptions.Compiled),
        [ModoCampo.TextoGuionBajo] = new(@"^[\p{L}_]*$", RegexOptions.Compiled),
        [ModoCampo.Numeros]      = new(@"^\d*$", RegexOptions.Compiled),
        [ModoCampo.Alfanumerico] = new(@"^[\p{L}\d\s]*$", RegexOptions.Compiled),
        [ModoCampo.AlfanumericoGuion] = new(@"^[\p{L}\d\s-]*$", RegexOptions.Compiled),
        [ModoCampo.AlfanumericoGuiones] = new(@"^[\p{L}\d\s_\[\]-]*$", RegexOptions.Compiled),
        [ModoCampo.Direccion]    = new(@"^[\p{L}\d\s,._#()-]*$", RegexOptions.Compiled),
        [ModoCampo.Correo]       = new(@"^[\p{L}\d@._-]*$", RegexOptions.Compiled),
        [ModoCampo.Codigo]       = new(@"^[\p{L}\d-]*$", RegexOptions.Compiled),
        [ModoCampo.FrecuenciaTexto] = new(@"^[\p{L}\d\s%.:;,_-]*$", RegexOptions.Compiled),
        [ModoCampo.Observaciones] = new(@"^[\p{L}\d\s,.;:_()\[\]-]*$", RegexOptions.Compiled),
    };

    private static readonly Dictionary<ModoCampo, Regex> NoPermitidos = new()
    {
        [ModoCampo.Texto]        = new(@"[^\p{L}\s]", RegexOptions.Compiled),
        [ModoCampo.TextoGuionBajo] = new(@"[^\p{L}_]", RegexOptions.Compiled),
        [ModoCampo.Numeros]      = new(@"[^\d]", RegexOptions.Compiled),
        [ModoCampo.Alfanumerico] = new(@"[^\p{L}\d\s]", RegexOptions.Compiled),
        [ModoCampo.AlfanumericoGuion] = new(@"[^\p{L}\d\s-]", RegexOptions.Compiled),
        [ModoCampo.AlfanumericoGuiones] = new(@"[^\p{L}\d\s_\[\]-]", RegexOptions.Compiled),
        [ModoCampo.Direccion]    = new(@"[^\p{L}\d\s,._#()-]", RegexOptions.Compiled),
        [ModoCampo.Correo]       = new(@"[^\p{L}\d@._-]", RegexOptions.Compiled),
        [ModoCampo.Codigo]       = new(@"[^\p{L}\d-]", RegexOptions.Compiled),
        [ModoCampo.FrecuenciaTexto] = new(@"[^\p{L}\d\s%.:;,_-]", RegexOptions.Compiled),
        [ModoCampo.Observaciones] = new(@"[^\p{L}\d\s,.;:_()\[\]-]", RegexOptions.Compiled),
    };

    // Nombre del modo tal como lo espera el JS (minúsculas).
    public static string NombreJs(ModoCampo modo) => modo.ToString().ToLowerInvariant();

    // Elimina del texto los caracteres no permitidos por el modo.
    public static string Sanear(ModoCampo modo, string? valor)
        => string.IsNullOrEmpty(valor) ? valor ?? string.Empty : NoPermitidos[modo].Replace(valor, string.Empty);

    // true si el texto respeta por completo el modo.
    public static bool EsValido(ModoCampo modo, string? valor)
        => string.IsNullOrEmpty(valor) || Patrones[modo].IsMatch(valor);
}
