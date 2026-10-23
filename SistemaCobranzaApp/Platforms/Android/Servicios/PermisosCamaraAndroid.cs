#if ANDROID

using Microsoft.Maui.ApplicationModel;

namespace SistemaCobranzaApp.Servicios;

/// <summary>
/// Solicitud del permiso de cámara en runtime (Android 6+), necesario para el escaneo de QR.
/// </summary>
public static class PermisosCamaraAndroid
{
    public static async Task<bool> SolicitarAsync()
    {
        var estado = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (estado != PermissionStatus.Granted)
            estado = await Permissions.RequestAsync<Permissions.Camera>();

        return estado == PermissionStatus.Granted;
    }
}

#endif
