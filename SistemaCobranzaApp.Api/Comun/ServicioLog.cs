using SistemaCobranzaApp.Domain.Comun;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Comun;

/// <summary>
/// Implementación de <see cref="IServicioLog"/> que emite registros con el
/// formato <c>{"log_data":{...}}</c> (una línea por evento) a consola y a un
/// archivo diario con rotación. Portable en Mac y Windows.
///
/// Vive en la capa API porque escribe a disco (solo tiene sentido en el
/// servidor). Las capas internas dependen de la abstracción
/// <see cref="IServicioLog"/> (en Shared), no de esta clase.
///
/// El <c>id_traza</c> se toma del <see cref="ContextoTraza"/> scoped, de modo
/// que todos los logs de un mismo request (en cualquier capa) comparten traza.
///
/// <para><b>Ámbito:</b> registrar como scoped (usa el ContextoTraza del request).</para>
/// </summary>
public class ServicioLog : IServicioLog
{
    private readonly ContextoTraza _traza;

    // Serialización compacta en una sola línea (sin indentación).
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        WriteIndented = false,
        // No re-escapar caracteres no ASCII para mantener legibilidad (acentos, etc.).
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Directorio de logs: <base de la app>/logs, válido en Mac y Windows.
    private static readonly string DirectorioLogs =
        Path.Combine(AppContext.BaseDirectory, "logs");

    // Candado global para escrituras seguras entre hilos al archivo del día.
    private static readonly object CandadoArchivo = new();

    public ServicioLog(ContextoTraza traza) => _traza = traza;

    public void Debug(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0)
        => Escribir(NivelLog.DEBUG, proceso, mensaje, datos, metodo, linea);

    public void Info(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0)
        => Escribir(NivelLog.INFO, proceso, mensaje, datos, metodo, linea);

    public void Warn(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0)
        => Escribir(NivelLog.WARN, proceso, mensaje, datos, metodo, linea);

    public void Error(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0)
        => Escribir(NivelLog.ERROR, proceso, mensaje, datos, metodo, linea);

    /// <summary>Registra un ERROR con el detalle de una excepción.</summary>
    public void Error(string proceso, Exception ex, object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0)
        => Escribir(NivelLog.ERROR, proceso, $"{ex.GetType().Name}: {ex.Message}", datos, metodo, linea);

    private void Escribir(NivelLog nivel, string proceso, string mensaje, object? datos, string metodo, int linea)
    {
        var registro = new RegistroLog
        {
            LogData = new LogData
            {
                Fecha = FechaProyecto.Ahora.DateTime.ToString("yyyy-MM-dd HH:mm:ss,fff"),
                Level = nivel.ToString(),
                IdTraza = _traza.IdTraza,
                Proceso = proceso,
                Mensaje = mensaje,
                Metodo = metodo,
                Linea = linea,
                Datos = datos
            }
        };

        var linea_json = JsonSerializer.Serialize(registro, OpcionesJson);

        // 1) Consola.
        Console.WriteLine(linea_json);

        // 2) Archivo diario (best-effort: nunca debe tumbar la petición).
        try
        {
            Directory.CreateDirectory(DirectorioLogs);
            var ruta = Path.Combine(DirectorioLogs, $"app-{FechaProyecto.Ahora.DateTime:yyyyMMdd}.log");
            lock (CandadoArchivo)
            {
                File.AppendAllText(ruta, linea_json + Environment.NewLine);
            }
        }
        catch
        {
            // Si falla la escritura a disco, seguimos: el log de consola ya salió.
        }
    }
}
