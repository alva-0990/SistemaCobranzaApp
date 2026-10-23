using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Services;

public sealed class DescargaArchivoMaui : IDescargaArchivo
{
    public async Task GuardarAsync(string nombre, byte[] contenido)
    {
        var directorio = Path.Combine(FileSystem.CacheDirectory, "reportes-excel");
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, Path.GetFileName(nombre));
        await File.WriteAllBytesAsync(ruta, contenido);
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Guardar o compartir reporte Excel",
            File = new ShareFile(ruta, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        }));
    }
}
