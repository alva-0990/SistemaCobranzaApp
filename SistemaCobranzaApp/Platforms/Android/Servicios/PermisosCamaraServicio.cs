#if ANDROID

using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Servicios;

/// <summary>
/// Implementación Android de <see cref="IPermisosCamara"/>: solicita el permiso CAMERA en runtime.
/// </summary>
public sealed class PermisosCamaraServicio : IPermisosCamara
{
    public Task<bool> SolicitarAsync() => PermisosCamaraAndroid.SolicitarAsync();
}

#endif
