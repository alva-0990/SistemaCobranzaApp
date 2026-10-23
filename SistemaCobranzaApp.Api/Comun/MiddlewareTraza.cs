using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Comun;

/// <summary>
/// Middleware que asigna un <c>id_traza</c> por petición al <see cref="ContextoTraza"/>
/// scoped, de modo que todos los logs de la petición compartan la misma traza.
///
/// Si el cliente envía la cabecera <c>X-Id-Traza</c>, se respeta (útil para
/// correlacionar trazas entre servicios). En caso contrario, se genera una nueva.
/// El id resultante se devuelve en la cabecera de respuesta <c>X-Id-Traza</c>.
/// </summary>
public class MiddlewareTraza
{
    public const string HeaderTraza = "X-Id-Traza";

    private readonly RequestDelegate _siguiente;

    public MiddlewareTraza(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto, ContextoTraza traza)
    {
        var entrante = contexto.Request.Headers[HeaderTraza].ToString();
        traza.IdTraza = string.IsNullOrWhiteSpace(entrante)
            ? ContextoTraza.GenerarId()
            : entrante;

        // Exponer la traza en la respuesta para correlación cliente/servidor.
        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[HeaderTraza] = traza.IdTraza;
            return Task.CompletedTask;
        });

        await _siguiente(contexto);
    }
}
