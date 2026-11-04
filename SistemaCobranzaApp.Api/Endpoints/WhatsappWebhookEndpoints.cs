using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SistemaCobranzaApp.Api.Whatsapp;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Webhook de entrada de Evolution API. Cuando un cliente responde "BAJA" se apaga su
/// bandera NotificarWhatsApp y deja de recibir mensajes. Se protege con el secreto
/// compartido Whatsapp:WebhookSecret (header X-Webhook-Secret); sin secreto configurado
/// el endpoint responde 401.
/// </summary>
public class WhatsappWebhookEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/whatsapp/webhook", async (
                [FromHeader(Name = "X-Webhook-Secret")] string? recibida,
                [FromBody] JsonElement payload,
                CobranzaDbContext db,
                IOptions<EvolutionApiOptions> opciones,
                IServicioLog log,
                CancellationToken ct) =>
            {
                const string proceso = "Webhook WhatsApp";
                var esperada = opciones.Value.WebhookSecret;

                if (string.IsNullOrWhiteSpace(esperada))
                {
                    log.Warn(proceso, "Webhook rechazado: Whatsapp:WebhookSecret no está configurado en la API.");
                    return Results.Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(recibida)
                    || !CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(recibida), Encoding.UTF8.GetBytes(esperada)))
                {
                    log.Warn(proceso, "Webhook rechazado: header X-Webhook-Secret ausente o incorrecto.",
                        datos: new { traeHeader = !string.IsNullOrWhiteSpace(recibida) });
                    return Results.Unauthorized();
                }

                try
                {
                    var evento = payload.TryGetProperty("event", out var ev) ? ev.GetString() : null;
                    if (!string.Equals(evento, "messages.upsert", StringComparison.OrdinalIgnoreCase))
                    {
                        log.Debug(proceso, $"Evento ignorado: '{evento}'.");
                        return Results.Ok();
                    }

                    var data = payload.GetProperty("data");
                    var key = data.GetProperty("key");

                    // Ignora los mensajes que enviamos nosotros mismos.
                    if (key.TryGetProperty("fromMe", out var fromMe) && fromMe.ValueKind == JsonValueKind.True)
                    {
                        log.Debug(proceso, "Mensaje propio (fromMe) ignorado.");
                        return Results.Ok();
                    }

                    var texto = "";
                    if (data.TryGetProperty("message", out var msg)
                        && msg.TryGetProperty("conversation", out var conv))
                        texto = conv.GetString()?.Trim().ToUpperInvariant() ?? "";

                    // WhatsApp puede enviar un identificador interno (@lid) en remoteJid; en ese caso
                    // el número real viene en remoteJidAlt (o senderPn en algunas versiones).
                    var jid = ObtenerJidTelefono(key);
                    var digitos = new string(jid.Split('@')[0].Where(char.IsAsciiDigit).ToArray());
                    var telefono = digitos.Length >= 10 ? digitos[^10..] : null; // últimos 10 dígitos (quita el 52/521)
                    var telOculto = telefono is null ? null : "…" + telefono[^4..];

                    log.Info(proceso, "Mensaje entrante recibido.",
                        datos: new { telefono = telOculto, esBaja = texto == "BAJA" });

                    if (texto != "BAJA") return Results.Ok();

                    if (telefono is null)
                    {
                        log.Warn(proceso, "BAJA recibida sin número de teléfono identificable.",
                            datos: new { remoteJid = key.TryGetProperty("remoteJid", out var rj) ? rj.GetString() : null });
                        return Results.Ok();
                    }

                    var clientes = await db.Clientes
                        .Where(c => c.TelefonoPrincipal.EndsWith(telefono)
                                    || (c.TelefonoSecundario != null && c.TelefonoSecundario.EndsWith(telefono)))
                        .ToListAsync(ct);

                    if (clientes.Count == 0)
                    {
                        log.Warn(proceso, "BAJA recibida pero ningún cliente tiene ese teléfono.",
                            datos: new { telefono = telOculto });
                        return Results.Ok();
                    }

                    foreach (var c in clientes) c.NotificarWhatsApp = Estatus.Inactivo;
                    await db.SaveChangesAsync(ct);

                    log.Info(proceso, $"BAJA aplicada: {clientes.Count} cliente(s) ya no recibirán WhatsApp.",
                        datos: new { telefono = telOculto, idsClientes = clientes.Select(c => c.IdCliente).ToArray() });
                }
                catch (Exception ex)
                {
                    // Siempre 200 para que Evolution no reintente en bucle.
                    log.Error(proceso, ex);
                }

                return Results.Ok();
            })
            .AllowAnonymous()
            .WithName("WhatsappWebhook");
    }

    /// <summary>
    /// Devuelve el JID que contiene el número de teléfono: el primero de remoteJid,
    /// remoteJidAlt o senderPn que termine en @s.whatsapp.net. Vacío si ninguno lo trae.
    /// </summary>
    private static string ObtenerJidTelefono(JsonElement key)
    {
        foreach (var campo in new[] { "remoteJid", "remoteJidAlt", "senderPn" })
        {
            if (key.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString() ?? "";
                if (s.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase))
                    return s;
            }
        }
        return string.Empty;
    }
}
