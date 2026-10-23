namespace SistemaCobranzaApp.Shared.Services;

public static class ImagenEmpresa
{
    public const int MaxBytes = 256 * 1024;

    public static string? Validar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (!valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return valor.Length <= 500 ? null : "La URL del logo no puede exceder 500 caracteres.";
        if (valor.Length > MaxBytes * 4 / 3 + 100) return "El logo no puede exceder 256 KB.";
        var coma = valor.IndexOf(',');
        if (coma < 0) return "El logo no contiene una imagen válida.";
        var tipo = valor[..coma];
        try
        {
            var bytes = Convert.FromBase64String(valor[(coma + 1)..]);
            if (bytes.Length > MaxBytes) return "El logo no puede exceder 256 KB.";
            var valido = tipo == "data:image/png;base64" && bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                || tipo == "data:image/jpeg;base64" && bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })
                || tipo == "data:image/webp;base64" && bytes.Length >= 12
                    && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
            return valido ? null : "Selecciona una imagen PNG, JPEG o WebP válida.";
        }
        catch (FormatException) { return "El logo no contiene una imagen válida."; }
    }
}
