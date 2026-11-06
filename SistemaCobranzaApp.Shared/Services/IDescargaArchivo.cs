using Microsoft.JSInterop;

namespace SistemaCobranzaApp.Shared.Services;

public interface IDescargaArchivo
{
    /// <param name="tipoMime">Tipo del archivo; por omisión, Excel (.xlsx).</param>
    Task GuardarAsync(string nombre, byte[] contenido, string? tipoMime = null);
}

public sealed class DescargaArchivoWeb(IJSRuntime js) : IDescargaArchivo
{
    public async Task GuardarAsync(string nombre, byte[] contenido, string? tipoMime = null)
    {
        await using var modulo = await js.InvokeAsync<IJSObjectReference>("import", "./_content/SistemaCobranzaApp.Shared/descarga-archivo.js");
        using var stream = new MemoryStream(contenido);
        using var referencia = new DotNetStreamReference(stream);
        await modulo.InvokeVoidAsync("guardarExcel", nombre, referencia, tipoMime);
    }
}
