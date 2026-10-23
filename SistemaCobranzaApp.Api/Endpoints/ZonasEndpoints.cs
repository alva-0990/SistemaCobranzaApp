using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class ZonasEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Cambiamos el nombre de la variable de 'publicas' a 'operativas' para evitar confusiones.
        // Sigue requiriendo token JWT gracias a .RequireAuthorization()
        var operativas = app.MapGroup("/api/zonas").RequireAuthorization();
        
        // 👈 Cambiado aquí: Inyecta ServicioZonasAdmin en lugar del público
        operativas.MapGet("/", async (ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Zonas Operativas";
            log.Info(proceso, "Consultando zonas activas.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Zonas activas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Zonas activas obtenidas.");
        });

        var admin = app.MapGroup("/api/admin/zonas").RequireAuthorization();

        admin.MapGet("/", async (ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Zonas (Admin)";
            log.Info(proceso, "Consultando todas las zonas.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Zonas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Zonas obtenidas.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Zona";
            log.Info(proceso, $"Buscando zona #{id}.", datos: new { id });
            var z = await s.ObtenerAsync(id, ct);
            if (z is null)
            {
                log.Warn(proceso, $"Zona #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la zona #{id}.");
            }
            log.Info(proceso, $"Zona #{id} obtenida.", datos: z);
            return Respuestas.Ok(z, "Zona obtenida.");
        });

        admin.MapPost("/", async (CrearZonaRequest req, ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Zona";
            log.Info(proceso, $"Creando zona: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Zona creada #{r.Zona!.IdZona} ('{r.Zona.Descripcion}').", datos: r.Zona);
                return Respuestas.Creado($"/api/admin/zonas/{r.Zona.IdZona}", r.Zona, "Zona creada correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear la zona: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear la zona.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarZonaRequest req, ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Zona";
            log.Info(proceso, $"Actualizando zona #{id} a '{req.Descripcion}'.", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Zona #{id} actualizada.", datos: r.Zona);
                return Respuestas.Ok(r.Zona!, "Zona actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Zona #{id} no encontrada.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la zona #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar la zona #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar la zona.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioZonasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Zona";
            log.Info(proceso, $"Dando de baja zona #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Zona #{id} dada de baja.", datos: r.Zona);
                return Respuestas.Ok(r.Zona!, "Zona dada de baja correctamente.");
            }
            log.Warn(proceso, $"Zona #{id} no encontrada.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la zona #{id}.");
        });
    }
}
