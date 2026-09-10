namespace SistemaCobranzaApp.Shared.Services; // Ajustado a tu espacio de nombres real

public interface IServicioAlmacenamiento
{
    Task GuardarCadenaAsync(string clave, string valor);
    Task<string?> ObtenerCadenaAsync(string clave);
    Task EliminarCadenaAsync(string clave);
}