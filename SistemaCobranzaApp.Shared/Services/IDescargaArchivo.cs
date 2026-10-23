using Microsoft.JSInterop;

namespace SistemaCobranzaApp.Shared.Services;

public interface IDescargaArchivo
{
    Task GuardarAsync(string nombre, byte[] contenido);
}

public sealed class DescargaArchivoWeb(IJSRuntime js) : IDescargaArchivo
{
    public async Task GuardarAsync(string nombre, byte[] contenido)
    {
        await using var modulo = await js.InvokeAsync<IJSObjectReference>("import", "./_content/SistemaCobranzaApp.Shared/descarga-archivo.js");
        using var stream = new MemoryStream(contenido);
        using var referencia = new DotNetStreamReference(stream);
        await modulo.InvokeVoidAsync("guardarExcel", nombre, referencia);
    }
}
