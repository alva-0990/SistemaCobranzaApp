using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Whatsapp;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Consulta de la tabla de paso colamensajeswhatsapp (solo lectura).
///   - Admin (/api/admin/whatsapp/mensajes): listado con filtros por estado y texto.
/// </summary>
public class MensajesWhatsAppEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/whatsapp").RequireAuthorization();

        // Listado de mensajes (más recientes primero).
        admin.MapGet("/mensajes", async (
            string? estado, string? texto, int? limite,
            ServicioMensajesWhatsAppAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Mensajes WhatsApp (Admin)";
            log.Info(proceso, "Consultando cola de mensajes de WhatsApp.", datos: new { estado, texto, limite });
            var lista = await s.ListarAsync(estado, texto, limite ?? 300, ct);
            log.Info(proceso, $"Mensajes obtenidos: {lista.Count}.");
            return Respuestas.Ok(lista, "Mensajes obtenidos.");
        }).WithName("ListarMensajesWhatsApp");
    }
}
