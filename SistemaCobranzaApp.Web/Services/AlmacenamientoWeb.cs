using Microsoft.JSInterop;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

public class AlmacenamientoWeb : IServicioAlmacenamiento
{
    private readonly IJSRuntime _js;

    public AlmacenamientoWeb(IJSRuntime js) => _js = js;

    public async Task GuardarCadenaAsync(string clave, string valor) 
        => await _js.InvokeVoidAsync("localStorage.setItem", clave, valor);

    public async Task<string?> ObtenerCadenaAsync(string clave) 
        => await _js.InvokeAsync<string?>("localStorage.getItem", clave);

    public async Task EliminarCadenaAsync(string clave) 
        => await _js.InvokeVoidAsync("localStorage.removeItem", clave);
}