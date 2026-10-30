using Microsoft.JSInterop;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

public sealed class EnlaceExternoWeb : IEnlaceExterno
{
    private readonly IJSRuntime _js;

    public EnlaceExternoWeb(IJSRuntime js)
    {
        _js = js;
    }

    public async Task AbrirAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("La dirección URL no es válida.");
        }

        await _js.InvokeVoidAsync("open", uri.AbsoluteUri, "_blank");
    }
}