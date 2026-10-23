using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    public Task<(List<RutaJornadaDto>? datos, string? error)> ListarRutasJornadaAsync(CancellationToken ct = default)
        => GetSobreListaAsync<RutaJornadaDto>("/api/jornada/rutas", ct);

    public Task<(JornadaDescargaDto? datos, string? error)> DescargarJornadaAsync(long ruta, CancellationToken ct = default)
        => GetJornadaRequeridaAsync<JornadaDescargaDto>(ruta == 0 ? "/api/jornada/descarga" : $"/api/jornada/rutas/{ruta}/descarga", ct);

    public Task<(ResultadoOperacionJornada? datos, string? error)> SincronizarVentaJornadaAsync(long ruta, VentaJornadaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ResultadoOperacionJornada>(HttpMethod.Post, ruta == 0 ? "/api/jornada/ventas" : $"/api/jornada/rutas/{ruta}/ventas", req, ct);

    public Task<(ResultadoOperacionJornada? datos, string? error)> SincronizarPagoJornadaAsync(PagoJornadaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ResultadoOperacionJornada>(HttpMethod.Post, "/api/jornada/pagos", req, ct);

    public Task<(ResultadoOperacionJornada? datos, string? error)> SincronizarClienteJornadaAsync(ClienteJornadaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ResultadoOperacionJornada>(HttpMethod.Post, "/api/jornada/clientes", req, ct);

    public async Task<string?> ActualizarZonaJornadaAsync(CancellationToken ct = default)
    {
        var sesion = Sesion;
        if (sesion is null) return "Inicia sesión para consultar tu zona.";
        var (zona, error) = await GetJornadaRequeridaAsync<RutaJornadaDto>("/api/jornada/zona", ct);
        if (error is not null || zona is null) return error;
        if (zona.IdUsuario != sesion.IdUsuario || zona.IdZona <= 0 || string.IsNullOrWhiteSpace(zona.Ruta))
            return "La API devolvió una zona inválida para este usuario.";
        if (Sesion != sesion) return "La sesión cambió durante la consulta de zona.";
        var actualizada = sesion with { IdZona = zona.IdZona, Zona = zona.Ruta };
        await _almacenamiento.GuardarCadenaAsync(TokenKey, JsonSerializer.Serialize(actualizada));
        Sesion = actualizada;
        NotificarSesionCambiada();
        return null;
    }

    // Una jornada es obligatoria: un 404 o un sobre sin resultado nunca son éxito.
    private async Task<(T? datos, string? error)> GetJornadaRequeridaAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);
            var destino = request.RequestUri?.ToString() ?? url;
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return (default, $"La API no tiene disponible {destino} (HTTP 404). Comprueba que Android apunte a la API actualizada con los endpoints de jornada.");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return (default, "Tu sesión expiró. Inicia sesión de nuevo.");
            ApiRespuesta<T>? sobre = null;
            try { sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<T>>(OpcionesWeb, ct); }
            catch (JsonException)
            {
                return (default, $"La API devolvió una respuesta inválida en {destino} (HTTP {(int)resp.StatusCode}).");
            }
            if (!resp.IsSuccessStatusCode || sobre?.Codigo != CodigosApi.Exito)
                return (default, MensajeDeSobre(sobre, $"Error al consultar {destino} (HTTP {(int)resp.StatusCode})."));
            if (sobre.Resultado is null)
                return (default, $"La API respondió sin resultado en {destino} (HTTP {(int)resp.StatusCode}).");
            return (sobre.Resultado, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La descarga fue cancelada o excedió el tiempo de espera."); }
    }
}
