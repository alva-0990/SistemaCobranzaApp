#if ANDROID

using Android.Webkit;

namespace SistemaCobranzaApp.Servicios;

/// <summary>
/// WebChromeClient que concede las solicitudes de permiso del contenido web (getUserMedia).
/// Por defecto Android deniega estas solicitudes dentro del WebView, lo que impide usar la
/// cámara desde JavaScript (escaneo de QR). Aquí aprobamos la captura de video/audio.
///
/// Nota: el permiso de sistema CAMERA debe estar ya concedido en runtime; esto solo autoriza
/// que la página dentro del WebView pueda usar ese permiso ya otorgado.
/// </summary>
public sealed class PermisivoWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request is null)
            return;

        // Concedemos únicamente los recursos que la app realmente usa (cámara/micrófono del WebView).
        request.Grant(request.GetResources());
    }
}

#endif
