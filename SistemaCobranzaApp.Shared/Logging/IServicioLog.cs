using System.Runtime.CompilerServices;

namespace SistemaCobranzaApp.Shared.Logging;

/// <summary>
/// Abstracción de logging estructurado disponible para todas las capas
/// (Application, Infrastructure, Api). La implementación concreta —que decide
/// dónde escribir (consola, archivo, etc.)— vive en la capa que la provee
/// (por ejemplo la API), y se inyecta por DI.
///
/// El <c>id_traza</c> se toma del <see cref="ContextoTraza"/> scoped, de modo
/// que todos los logs de una misma petición (en cualquier capa) comparten traza.
///
/// <para><b>metodo</b> y <b>linea</b> se autocompletan con
/// <see cref="CallerMemberNameAttribute"/> / <see cref="CallerLineNumberAttribute"/>;
/// no los pases manualmente salvo que quieras sobreescribirlos.</para>
/// </summary>
public interface IServicioLog
{
    void Debug(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0);

    void Info(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0);

    void Warn(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0);

    void Error(string proceso, string mensaje = "", object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0);

    /// <summary>Registra un ERROR con el detalle de una excepción.</summary>
    void Error(string proceso, Exception ex, object? datos = null,
        [CallerMemberName] string metodo = "", [CallerLineNumber] int linea = 0);
}
