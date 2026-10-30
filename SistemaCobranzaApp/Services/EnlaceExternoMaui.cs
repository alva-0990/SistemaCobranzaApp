using Microsoft.Maui.ApplicationModel;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

public sealed class EnlaceExternoMaui : IEnlaceExterno
{
    public async Task AbrirAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("La dirección URL no es válida.", nameof(url));

        if (uri.Scheme != Uri.UriSchemeHttps &&
            uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException(
                "Solo se permiten enlaces HTTP o HTTPS.",
                nameof(url));
        }

        await Launcher.Default.OpenAsync(uri);
    }
}