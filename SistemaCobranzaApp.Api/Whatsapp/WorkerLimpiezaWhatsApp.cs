using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Whatsapp;

namespace SistemaCobranzaApp.Api.Whatsapp;

/// <summary>
/// BackgroundService que purga periódicamente la tabla de paso cola_mensajes_whatsapp.
/// Elimina las filas en estado 'Enviado' cuya fecha_envio supera el periodo de retención.
/// Las filas 'Fallido' se conservan (auditoría hasta revisión manual).
///
/// Parametrización en caliente (tabla parametrossistema):
///   * WHATSAPP_PURGA_ACTIVA ('SI'/'NO')  activa o desactiva el borrado.
///   * WHATSAPP_PURGA_DIAS   (numérico)    días de retención de los 'Enviado'.
/// El intervalo entre ciclos es una constante técnica; el "si purga" y los "días" se
/// leen de la tabla.
/// </summary>
public sealed class WorkerLimpiezaWhatsApp : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LectorParametrosWhatsApp _parametros;
    private readonly ILogger<WorkerLimpiezaWhatsApp> _log;

    public WorkerLimpiezaWhatsApp(
        IServiceScopeFactory scopeFactory,
        LectorParametrosWhatsApp parametros,
        ILogger<WorkerLimpiezaWhatsApp> log)
    {
        _scopeFactory = scopeFactory;
        _parametros = parametros;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromHours(EvolutionApiOptions.Defecto.HorasIntervaloLimpieza);
        _log.LogInformation("WorkerLimpiezaWhatsApp iniciado. Intervalo={Horas}h (retención/activación desde parametrossistema).",
            EvolutionApiOptions.Defecto.HorasIntervaloLimpieza);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WorkerLimpiezaWhatsApp: error durante la purga. Se reintentará en el próximo ciclo.");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("WorkerLimpiezaWhatsApp detenido.");
    }

    private async Task PurgarAsync(CancellationToken ct)
    {
        var config = await _parametros.ObtenerAsync(ct);
        if (!config.PurgaActiva)
        {
            _log.LogDebug("WorkerLimpiezaWhatsApp: purga desactivada por parámetro WHATSAPP_PURGA_ACTIVA.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CobranzaDbContext>();

        var corte = DateTimeOffset.UtcNow.AddDays(-config.PurgaDias);

        var eliminados = await db.ColaMensajesWhatsApp
            .Where(m => m.Estado == EstadoMensajeWhatsApp.Enviado
                        && m.FechaEnvio != null
                        && m.FechaEnvio < corte)
            .ExecuteDeleteAsync(ct);

        if (eliminados > 0)
            _log.LogInformation("WorkerLimpiezaWhatsApp: purgados {Cantidad} mensaje(s) 'Enviado' anteriores a {Corte:o} (retención {Dias}d).",
                eliminados, corte, config.PurgaDias);
    }
}
