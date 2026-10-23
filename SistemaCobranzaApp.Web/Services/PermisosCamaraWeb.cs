using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

/// <summary>
/// Implementación Web de <see cref="IPermisosCamara"/>. En el navegador el permiso de cámara
/// lo solicita el propio navegador al invocar getUserMedia, por lo que aquí solo devolvemos true.
/// </summary>
public sealed class PermisosCamaraWeb : IPermisosCamara
{
    public Task<bool> SolicitarAsync() => Task.FromResult(true);
}
