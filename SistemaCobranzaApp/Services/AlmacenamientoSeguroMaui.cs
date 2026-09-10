using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

public class AlmacenamientoSeguroMaui : IServicioAlmacenamiento
{
    public async Task GuardarCadenaAsync(string clave, string valor) 
        => await SecureStorage.Default.SetAsync(clave, valor);

    public async Task<string?> ObtenerCadenaAsync(string clave) 
        => await SecureStorage.Default.GetAsync(clave);

    public async Task EliminarCadenaAsync(string clave) 
        => SecureStorage.Default.Remove(clave);
}