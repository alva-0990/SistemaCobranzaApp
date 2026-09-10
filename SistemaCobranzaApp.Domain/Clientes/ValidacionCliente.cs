namespace SistemaCobranzaApp.Domain.Clientes;

/// <summary>
/// Validación pura de los datos de un cliente (sin base de datos).
/// Reglas mínimas de integridad de entrada; la existencia de FKs se
/// valida aparte contra la base.
/// </summary>
public static class ValidacionCliente
{
    public const int MaxNombre = 100;

    /// <summary>
    /// Valida los campos de entrada. Devuelve null si es válido, o el mensaje
    /// del primer error encontrado.
    /// </summary>
    public static string? Validar(
        string? nombre,
        long idRuta,
        long? idVendedor,
        long? idClasificacion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return "El nombre del cliente es obligatorio.";

        if (nombre.Trim().Length > MaxNombre)
            return $"El nombre no puede exceder {MaxNombre} caracteres.";

        if (idRuta <= 0)
            return "La ruta es obligatoria.";

        if (idVendedor is { } v && v <= 0)
            return "El vendedor seleccionado no es válido.";

        if (idClasificacion is { } c && c <= 0)
            return "La clasificación seleccionada no es válida.";

        return null;
    }

    public static bool EsValido(string? nombre, long idRuta, long? idVendedor, long? idClasificacion)
        => Validar(nombre, idRuta, idVendedor, idClasificacion) is null;
}
