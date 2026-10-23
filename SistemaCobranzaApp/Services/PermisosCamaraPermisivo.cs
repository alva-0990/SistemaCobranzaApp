using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

/// <summary>
/// Implementación por defecto de <see cref="IPermisosCamara"/> para plataformas MAUI
/// distintas de Android (iOS/MacCatalyst/Windows), donde el escaneo de QR por WebView
/// no se usa activamente. Concede sin pedir permiso explícito.
/// </summary>
public sealed class PermisosCamaraPermisivo : IPermisosCamara
{
    public Task<bool> SolicitarAsync() => Task.FromResult(true);
}
