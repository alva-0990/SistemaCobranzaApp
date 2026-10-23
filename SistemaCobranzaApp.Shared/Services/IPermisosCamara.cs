namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Abstracción para solicitar el permiso de cámara en runtime, implementada por cada plataforma.
/// En Web el navegador gestiona el permiso al llamar getUserMedia, así que devuelve true.
/// </summary>
public interface IPermisosCamara
{
    Task<bool> SolicitarAsync();
}
