using Microsoft.JSInterop;

namespace SistemaCobranzaApp.Shared.Services;

public interface IDescargaArchivo
{
    /// <param name="tipoMime">Tipo del archivo; por omisión, Excel (.xlsx).</param>
    Task GuardarAsync(string nombre, byte[] contenido, string? tipoMime = null);

    /// <summary>Abre la hoja de compartir del sistema (WhatsApp, etc.). Por omisión equivale a guardar.</summary>
    Task CompartirAsync(string nombre, byte[] contenido, string? tipoMime = null, string? texto = null)
        => GuardarAsync(nombre, contenido, tipoMime);
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

    public async Task CompartirAsync(string nombre, byte[] contenido, string? tipoMime = null, string? texto = null)
    {
        await using var modulo = await js.InvokeAsync<IJSObjectReference>("import", "./_content/SistemaCobranzaApp.Shared/visor-pdf.js");
        using var stream = new MemoryStream(contenido);
        using var referencia = new DotNetStreamReference(stream);
        var compartido = await modulo.InvokeAsync<bool>("compartirArchivo", nombre, referencia, tipoMime, texto);
        // Plan B (escritorio sin Web Share): se descarga para adjuntarlo manualmente en WhatsApp Web.
        if (!compartido) await GuardarAsync(nombre, contenido, tipoMime);
    }
}
