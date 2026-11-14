namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Abre la aplicación de mapas predeterminada del sistema operativo con la ruta hacia unas coordenadas.
/// Android: app de mapas por defecto (Google Maps u otra). iOS: Apple Maps. Web: enlace de navegación en el navegador.
/// </summary>
public interface INavegadorMapa
{
    Task LlevarmeAsync(decimal latitud, decimal longitud, string? nombre = null);
}
