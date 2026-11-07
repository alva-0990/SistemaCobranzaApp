using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

/// <summary>
/// Implementación MAUI de <see cref="IServicioUbicacion"/>. Solicita el permiso de ubicación
/// en runtime y obtiene un fix de GPS con precisión media y un timeout corto, para no congelar
/// el registro de venta/pago en campo.
///
/// Es best-effort: ante permiso denegado, falta de señal, timeout o cualquier excepción,
/// devuelve null y el flujo continúa guardando la venta/pago sin coordenadas.
/// </summary>
public sealed class ServicioUbicacionMaui : IServicioUbicacion
{
    // Tiempo máximo que esperamos un fix de GPS antes de rendirnos (no bloquear al cobrador).
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    public async Task<Ubicacion?> ObtenerActualAsync(CancellationToken ct = default)
    {
        try
        {
            var estado = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (estado != PermissionStatus.Granted)
                estado = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (estado != PermissionStatus.Granted)
                return null; // El usuario no concedió el permiso: se registra sin ubicación.

            // Primero intenta la última ubicación conocida (instantánea); si no hay, pide una nueva.
            var loc = await Geolocation.GetLastKnownLocationAsync();
            if (loc is null)
            {
                var solicitud = new GeolocationRequest(GeolocationAccuracy.Medium, Timeout);
                loc = await Geolocation.GetLocationAsync(solicitud, ct);
            }

            if (loc is null) return null;

            // numeric(10,8) / numeric(11,8): 8 decimales de precisión (≈1 mm), suficiente y seguro.
            var lat = Math.Round((decimal)loc.Latitude, 8);
            var lng = Math.Round((decimal)loc.Longitude, 8);
            return new Ubicacion(lat, lng);
        }
        catch
        {
            // Sin señal, GPS apagado, timeout, permiso revocado en caliente, etc. No rompemos el flujo.
            return null;
        }
    }
}
