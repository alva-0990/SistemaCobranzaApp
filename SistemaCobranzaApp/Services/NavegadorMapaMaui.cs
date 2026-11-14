using Microsoft.Maui.ApplicationModel;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

/// <summary>
/// Usa el selector nativo de MAUI: abre la app de mapas predeterminada del dispositivo
/// (Android: la que el usuario tenga; iOS/macOS: Apple Maps) iniciando la navegación.
/// </summary>
public sealed class NavegadorMapaMaui : INavegadorMapa
{
    public async Task LlevarmeAsync(decimal latitud, decimal longitud, string? nombre = null)
    {
        var opciones = new MapLaunchOptions
        {
            Name = string.IsNullOrWhiteSpace(nombre) ? "Cliente" : nombre,
            NavigationMode = NavigationMode.Driving
        };
        await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync((double)latitud, (double)longitud, opciones);
    }
}
