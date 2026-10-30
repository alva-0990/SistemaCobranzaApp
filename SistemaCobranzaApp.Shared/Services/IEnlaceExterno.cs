namespace SistemaCobranzaApp.Shared.Services;

public interface IEnlaceExterno
{
    Task AbrirAsync(string url);
}