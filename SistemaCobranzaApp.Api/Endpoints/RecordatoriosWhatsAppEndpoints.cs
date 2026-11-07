using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Whatsapp;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Recordatorios de cobranza por WhatsApp (envío MANUAL confirmado por el usuario).
///   - GET  /api/admin/whatsapp/recordatorios            : lista candidatos en atraso (con preview del mensaje).
///   - POST /api/admin/whatsapp/recordatorios/enviar     : encola los recordatorios de los clientes seleccionados.
/// </summary>
public class RecordatoriosWhatsAppEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/whatsapp/recordatorios").RequireAuthorization();

        grupo.MapGet("/", async (
            long? idZona, long? idRuta, int? diasAtrasoMinimo,
            ServicioRecordatoriosWhatsApp s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Candidatos a Recordatorio";
            try
            {
                var filtro = new RecordatorioCobranzaFiltro(idZona, idRuta, diasAtrasoMinimo ?? 1);
                var r = await s.ListarCandidatosAsync(filtro, ct);
                return Respuestas.Ok(r, "Candidatos obtenidos.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex);
                return Respuestas.ErrorInterno("No fue posible obtener los candidatos a recordatorio.");
            }
        }).WithName("ListarCandidatosRecordatorio");

        grupo.MapPost("/enviar", async (
            EnviarRecordatoriosRequest req, long? idZona, long? idRuta, int? diasAtrasoMinimo,
            ServicioRecordatoriosWhatsApp s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Enviar Recordatorios de Cobranza";
            try
            {
                var filtro = new RecordatorioCobranzaFiltro(idZona, idRuta, diasAtrasoMinimo ?? 1);
                var r = await s.EnviarAsync(req, filtro, ct);
                log.Info(proceso, $"Recordatorios encolados: {r.Encolados}, omitidos: {r.Omitidos}.");
                return Respuestas.Ok(r, $"Se encolaron {r.Encolados} recordatorio(s).");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex);
                return Respuestas.ErrorInterno("No fue posible enviar los recordatorios.");
            }
        }).WithName("EnviarRecordatorios");
    }
}
