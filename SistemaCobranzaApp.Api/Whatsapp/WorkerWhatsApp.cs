using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Whatsapp;

namespace SistemaCobranzaApp.Api.Whatsapp;

/// <summary>
/// BackgroundService que drena la cola cola_mensajes_whatsapp y envía cada mensaje a
/// Evolution API. Vive dentro de la API, por lo que atiende ventas/pagos originados en
/// web, Android en línea y la sincronización offline (todos terminan insertando en central).
///
/// Robustez:
///   * Despierta con la señal en memoria (<see cref="SenalColaWhatsApp"/>) O por polling
///     de respaldo, de modo que ningún pendiente queda atascado.
///   * Marca cada fila 'Procesando' antes de enviar; al éxito 'Enviado' + fecha_envio;
///     al fallo incrementa intentos y, superado el máximo, la marca 'Fallido' con error_log.
///   * Usa un scope de DI por ciclo (CobranzaDbContext es scoped).
/// </summary>
public sealed class WorkerWhatsApp : BackgroundService
{
    public const string HttpClientNombre = "EvolutionApi";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SenalColaWhatsApp _senal;
    private readonly IHttpClientFactory _httpFactory;
    private readonly EvolutionApiOptions _opciones;
    private readonly WhatsAppOficialOptions _opcionesOficial;
    private readonly LectorParametrosWhatsApp _parametros;
    private readonly ILogger<WorkerWhatsApp> _log;

    public WorkerWhatsApp(
        IServiceScopeFactory scopeFactory,
        SenalColaWhatsApp senal,
        IHttpClientFactory httpFactory,
        IOptions<EvolutionApiOptions> opciones,
        IOptions<WhatsAppOficialOptions> opcionesOficial,
        LectorParametrosWhatsApp parametros,
        ILogger<WorkerWhatsApp> log)
    {
        _scopeFactory = scopeFactory;
        _senal = senal;
        _httpFactory = httpFactory;
        _opciones = opciones.Value;
        _opcionesOficial = opcionesOficial.Value;
        _parametros = parametros;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "WorkerWhatsApp iniciado. Evolution configurada={Evolution}, API oficial configurada={Oficial}, polling={Segundos}s.",
            _opciones.EstaConfigurado(), _opcionesOficial.EstaConfigurado(), EvolutionApiOptions.Defecto.SegundosPollingRespaldo);

        var respaldo = TimeSpan.FromSeconds(EvolutionApiOptions.Defecto.SegundosPollingRespaldo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Vacía la cola por lotes hasta que no queden pendientes en este ciclo.
                while (!stoppingToken.IsCancellationRequested)
                {
                    var procesados = await ProcesarLoteAsync(stoppingToken);
                    if (procesados == 0) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WorkerWhatsApp: error inesperado en el ciclo. Se reintentará tras el respaldo.");
            }

            // Espera la próxima señal o el polling de respaldo.
            try
            {
                await _senal.EsperarAsync(respaldo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("WorkerWhatsApp detenido.");
    }

    /// <summary>
    /// Procesa un lote de pendientes. Devuelve cuántos se intentaron (0 = nada pendiente).
    /// </summary>
    private async Task<int> ProcesarLoteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CobranzaDbContext>();

        var lote = await db.ColaMensajesWhatsApp
            .Where(m => m.Estado == EstadoMensajeWhatsApp.Pendiente && m.Intentos < EvolutionApiOptions.Defecto.MaxIntentos)
            .OrderBy(m => m.FechaCreacion)
            .Take(EvolutionApiOptions.Defecto.TamanoLote)
            .ToListAsync(ct);

        if (lote.Count == 0) return 0;

        // El flujo (Evolution u oficial) se lee de parametrossistema en cada lote: el cambio aplica en caliente.
        var config = await _parametros.ObtenerAsync(ct);
        var oficial = config.Proveedor == ProveedorWhatsApp.Oficial;

        // Si no hay datos de conexión del flujo elegido, no quemamos intentos: dejamos todo en Pendiente.
        // (El interruptor de negocio WHATSAPP_ENVIO_ACTIVO ya evitó encolar aguas arriba.)
        if (oficial ? !_opcionesOficial.EstaConfigurado() : !_opciones.EstaConfigurado())
        {
            _log.LogWarning(
                "WorkerWhatsApp: hay {Cantidad} mensaje(s) pendientes pero el flujo {Proveedor} no está configurado. Se dejan en 'Pendiente'.",
                lote.Count, config.Proveedor);
            return 0;
        }

        var cliente = _httpFactory.CreateClient(oficial ? WhatsAppOficialOptions.HttpClientNombre : HttpClientNombre);
        // La API oficial no necesita el freno anti-baneo de la sesión no oficial.
        var pausa = TimeSpan.FromSeconds(oficial ? 1 : EvolutionApiOptions.Defecto.SegundosEntreMensajes);

        foreach (var msg in lote)
        {
            // Marca 'Procesando' para evitar que otra ejecución lo retome (defensa básica).
            msg.Estado = EstadoMensajeWhatsApp.Procesando;
            await db.SaveChangesAsync(ct);

            try
            {
                if (oficial)
                    await EnviarOficialAsync(cliente, msg, config, ct);
                else
                    await EnviarAsync(cliente, msg.Telefono, msg.Mensaje, ct);

                msg.Estado = EstadoMensajeWhatsApp.Enviado;
                msg.FechaEnvio = DateTimeOffset.UtcNow;
                msg.ErrorLog = null;
            }
            catch (Exception ex)
            {
                msg.Intentos += 1;
                msg.ErrorLog = Recortar(ex.Message, 2000);

                if (msg.Intentos >= EvolutionApiOptions.Defecto.MaxIntentos)
                {
                    msg.Estado = EstadoMensajeWhatsApp.Fallido;
                    _log.LogError(ex,
                        "WorkerWhatsApp: mensaje #{Id} a {Telefono} FALLIDO tras {Intentos} intento(s).",
                        msg.Id, msg.Telefono, msg.Intentos);
                }
                else
                {
                    // Vuelve a 'Pendiente' para reintentar en el próximo ciclo.
                    msg.Estado = EstadoMensajeWhatsApp.Pendiente;
                    _log.LogWarning(ex,
                        "WorkerWhatsApp: mensaje #{Id} a {Telefono} falló (intento {Intentos}/{Max}). Se reintentará.",
                        msg.Id, msg.Telefono, msg.Intentos, EvolutionApiOptions.Defecto.MaxIntentos);
                }
            }

            await db.SaveChangesAsync(ct);

            await Task.Delay(pausa, ct);
        }

        return lote.Count;
    }

    /// <summary>
    /// Envía el mensaje por la API oficial (WhatsApp Cloud API): POST {BaseUrl}/{version}/{phoneNumberId}/messages
    /// con Bearer token y una plantilla aprobada (type=template) cuyas variables {{1}}..{{n}} son las
    /// guardadas en la cola. Las plantillas aprobadas permiten escribir al cliente fuera de la ventana de 24 h.
    /// </summary>
    private async Task EnviarOficialAsync(HttpClient cliente, ColaMensajeWhatsApp msg, ConfiguracionWhatsApp config, CancellationToken ct)
    {
        var nombrePlantilla = config.NombrePlantillaOficial(msg.Plantilla);
        if (string.IsNullOrWhiteSpace(nombrePlantilla) || string.IsNullOrWhiteSpace(msg.Variables))
            throw new InvalidOperationException(
                "El mensaje no tiene plantilla/variables (se encoló antes de usar la API oficial); no se puede enviar por ese flujo.");

        var variables = System.Text.Json.JsonSerializer.Deserialize<List<string>>(msg.Variables) ?? new List<string>();
        var cuerpo = new
        {
            messaging_product = "whatsapp",
            to = msg.Telefono,
            type = "template",
            template = new
            {
                name = nombrePlantilla,
                language = new { code = config.OficialIdioma },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = variables.Select(v => new { type = "text", text = LimpiarVariable(v) }).ToArray()
                    }
                }
            }
        };

        var ruta = $"{_opcionesOficial.VersionApi.Trim('/')}/{Uri.EscapeDataString(_opcionesOficial.PhoneNumberId)}/messages";
        using var req = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opcionesOficial.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await cliente.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detalle = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"API oficial de WhatsApp respondió {(int)resp.StatusCode} {resp.StatusCode}: {Recortar(detalle, 500)}");
        }
    }

    // Meta rechaza variables con saltos de línea, tabuladores o más de 4 espacios seguidos.
    private static string LimpiarVariable(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "-";
        var limpio = System.Text.RegularExpressions.Regex.Replace(valor, @"\s+", " ").Trim();
        return limpio.Length == 0 ? "-" : limpio;
    }

    /// <summary>
    /// Envía un texto a Evolution API: POST {BaseUrl}/message/sendText/{instancia}
    /// con header 'apikey' y cuerpo { number, text }.
    /// </summary>
    private async Task EnviarAsync(HttpClient cliente, string telefono, string texto, CancellationToken ct)
    {
        var ruta = $"message/sendText/{Uri.EscapeDataString(_opciones.Instancia)}";
        var cuerpo = new { number = telefono, text = texto, delay = EvolutionApiOptions.Defecto.MsPresenciaEscribiendo };

        using var req = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = JsonContent.Create(cuerpo)
        };
        // La API key va por mensaje para tolerar rotación de credenciales sin reiniciar.
        req.Headers.TryAddWithoutValidation("apikey", _opciones.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await cliente.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detalle = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Evolution API respondió {(int)resp.StatusCode} {resp.StatusCode}: {Recortar(detalle, 500)}");
        }
    }

    private static string Recortar(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max];
    }
}
