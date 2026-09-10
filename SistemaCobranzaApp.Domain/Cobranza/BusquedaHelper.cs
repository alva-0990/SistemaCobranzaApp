using System.Text.RegularExpressions;

namespace SistemaCobranzaApp.Domain.Cobranza;

/// <summary>
/// Utilidades puras de búsqueda del módulo de cobro, portadas de
/// VistaPagoAgregar (app WPF). Sin dependencias de base de datos.
/// </summary>
public static class BusquedaHelper
{
    // Un texto de venta válido es: solo dígitos (IdVenta) o V####### (CodigoVenta).
    private static readonly Regex SoloDigitos = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex CodigoVenta = new(@"^V\d{7}$", RegexOptions.Compiled);

    public enum TipoBusquedaVenta
    {
        Invalida,
        PorId,
        PorCodigo
    }

    /// <summary>
    /// Determina cómo interpretar el texto de búsqueda de venta.
    /// Replica la validación de FiltroFolioSolo_TextChanged/EjecutarBusquedaVenta.
    /// </summary>
    public static TipoBusquedaVenta ClasificarBusquedaVenta(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return TipoBusquedaVenta.Invalida;

        var t = texto.Trim().ToUpperInvariant();

        if (CodigoVenta.IsMatch(t))
            return TipoBusquedaVenta.PorCodigo;

        if (SoloDigitos.IsMatch(t))
            return TipoBusquedaVenta.PorId;

        return TipoBusquedaVenta.Invalida;
    }

    /// <summary>
    /// Intenta extraer el IdVenta cuando el texto son solo dígitos.
    /// </summary>
    public static bool TryParseIdVenta(string? texto, out long idVenta)
    {
        idVenta = 0;
        if (string.IsNullOrWhiteSpace(texto))
            return false;

        return long.TryParse(texto.Trim(), out idVenta);
    }

    /// <summary>
    /// Normaliza el código de venta a mayúsculas (como se guarda/consulta).
    /// </summary>
    public static string NormalizarCodigoVenta(string texto) =>
        (texto ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Arma "Nombre ApPaterno ApMaterno" sin espacios sobrantes.
    /// </summary>
    public static string NombreCompleto(string? nombre, string? apPaterno, string? apMaterno)
    {
        var partes = new[] { nombre, apPaterno, apMaterno }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim());
        return string.Join(" ", partes);
    }

    /// <summary>
    /// Envuelve un término para búsqueda LIKE: %term%. Vacío -> "%".
    /// </summary>
    public static string ParaLike(string? termino)
    {
        var t = (termino ?? string.Empty).Trim();
        return "%" + t + "%";
    }
}
