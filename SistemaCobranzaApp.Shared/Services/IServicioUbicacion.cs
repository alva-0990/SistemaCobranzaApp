namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Coordenadas GPS capturadas del dispositivo. Nulo cuando no se pudo obtener
/// (permiso denegado, sin señal, o plataforma sin GPS como la Web).
/// </summary>
public readonly record struct Ubicacion(decimal Latitud, decimal Longitud);

/// <summary>
/// Abstracción para obtener la ubicación actual del dispositivo, implementada por cada plataforma.
///
/// Es "best-effort": NUNCA debe bloquear ni hacer fallar el registro de una venta o un pago.
/// Si el usuario niega el permiso, no hay señal o la plataforma no tiene GPS (Web), devuelve null.
/// </summary>
public interface IServicioUbicacion
{
    /// <summary>
    /// Intenta obtener la ubicación actual. Devuelve null si no se pudo (sin permiso, sin señal,
    /// timeout o plataforma sin GPS). No lanza excepciones.
    /// </summary>
    Task<Ubicacion?> ObtenerActualAsync(CancellationToken ct = default);
}
