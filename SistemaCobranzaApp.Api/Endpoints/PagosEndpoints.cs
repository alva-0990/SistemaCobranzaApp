using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Pagos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class PagosEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Colócalo dentro del MapEndpoints junto a tus otros grupos de administración
        var admin = app.MapGroup("/api/admin/pagos").RequireAuthorization();

        admin.MapDelete("/{id:long}", async (long id, ServicioPagosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Reversión de Pago";
            log.Info(proceso, $"Solicitud para eliminar el pago físico #{id}.", datos: new { id });

            var (exito, error) = await s.RevertirPagoAsync(id, ct);

            if (exito)
            {
                log.Info(proceso, $"Pago #{id} y su bloque transaccional revertidos con éxito en la base de datos.");
                // Retornamos true indicando que la operación de borrado se completó
                return Respuestas.Ok(true, "El pago y todos sus movimientos vinculados fueron revertidos correctamente.");
            }

            log.Warn(proceso, $"No se pudo revertir el pago #{id}: {error}", datos: new { id });
            return Respuestas.SolicitudInvalida("No se pudo procesar la reversión del pago.", error ?? "");
        });

    }
    
}