namespace SistemaCobranzaApp.Domain.Inventario;

/// <summary>
/// Validación pura de los datos de un producto (sin base de datos).
/// </summary>
public static class ValidacionProducto
{
    public const int MaxCodigo = 50;
    public const int MaxDescripcion = 200;

    public static string? Validar(
        string? codigoClave,
        string? descripcion,
        decimal costo,
        decimal precioSugerido,
        decimal precioMinimo,
        int stockMini)
    {
        if (string.IsNullOrWhiteSpace(codigoClave))
            return "El código/clave del producto es obligatorio.";

        if (codigoClave.Trim().Length > MaxCodigo)
            return $"El código no puede exceder {MaxCodigo} caracteres.";

        if (string.IsNullOrWhiteSpace(descripcion))
            return "La descripción del producto es obligatoria.";

        if (descripcion.Trim().Length > MaxDescripcion)
            return $"La descripción no puede exceder {MaxDescripcion} caracteres.";

        if (costo < 0) return "El costo no puede ser negativo.";
        if (precioSugerido < 0) return "El precio sugerido no puede ser negativo.";
        if (precioMinimo < 0) return "El precio mínimo no puede ser negativo.";
        if (stockMini < 0) return "El stock mínimo no puede ser negativo.";

        if (precioSugerido > 0 && precioMinimo > precioSugerido)
            return "El precio mínimo no puede ser mayor al precio sugerido.";

        return null;
    }

    public static bool EsValido(string? codigo, string? desc, decimal costo, decimal sug, decimal min, int stock)
        => Validar(codigo, desc, costo, sug, min, stock) is null;
}
