using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

/// <summary>
/// Implementación Web de <see cref="IServicioUbicacion"/>. La app web (servidor) no captura GPS
/// del dispositivo, por lo que devuelve null: la venta/pago se registra sin coordenadas.
/// (La geolocalización por navegador requeriría JSInterop y HTTPS; no se usa por ahora.)
/// </summary>
public sealed class ServicioUbicacionWeb : IServicioUbicacion
{
    public Task<Ubicacion?> ObtenerActualAsync(CancellationToken ct = default) => Task.FromResult<Ubicacion?>(null);
}
