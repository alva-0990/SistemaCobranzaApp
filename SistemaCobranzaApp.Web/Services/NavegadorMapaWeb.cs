using System.Globalization;
using Microsoft.JSInterop;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

/// <summary>
/// En navegador abre el enlace universal de rutas de Google Maps (en celulares abre la app de mapas instalada).
/// </summary>
public sealed class NavegadorMapaWeb : INavegadorMapa
{
    private readonly IJSRuntime _js;

    public NavegadorMapaWeb(IJSRuntime js) => _js = js;

    public async Task LlevarmeAsync(decimal latitud, decimal longitud, string? nombre = null)
    {
        var destino = string.Create(CultureInfo.InvariantCulture, $"{latitud},{longitud}");
        var url = $"https://www.google.com/maps/dir/?api=1&destination={destino}&travelmode=driving";
        await _js.InvokeVoidAsync("open", url, "_blank");
    }
}
