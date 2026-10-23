using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class MovimientosEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ---- Operativas (solo activas) ----
        var operativas = app.MapGroup("/api/movimientos").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Movimientos Operativos";
            log.Info(proceso, "Consultando movimientos activos.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Movimientos activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Movimientos activos obtenidos.");
        });

        // ---- Panel de Administración ----
        var admin = app.MapGroup("/api/admin/movimientos").RequireAuthorization();

        admin.MapGet("/", async (ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Movimientos (Admin)";
            log.Info(proceso, "Consultando todos los movimientos.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Movimientos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Movimientos obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Movimiento";
            log.Info(proceso, $"Buscando movimiento #{id}.", datos: new { id });
            var m = await s.ObtenerAsync(id, ct);
            if (m is null)
            {
                log.Warn(proceso, $"Movimiento #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el movimiento #{id}.");
            }
            log.Info(proceso, $"Movimiento #{id} obtenido.", datos: m);
            return Respuestas.Ok(m, "Movimiento obtenido.");
        });

        admin.MapPost("/", async (CrearMovimientoRequest req, ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Movimiento";
            log.Info(proceso, $"Creando movimiento: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Movimiento creado #{r.Movimiento!.Id} ('{r.Movimiento.Descripcion}').", datos: r.Movimiento);
                return Respuestas.Creado($"/api/admin/movimientos/{r.Movimiento.Id}", r.Movimiento, "Movimiento creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el movimiento: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el movimiento.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarMovimientoRequest req, ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Movimiento";
            log.Info(proceso, $"Actualizando movimiento #{id} a '{req.Descripcion}'.", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Movimiento #{id} actualizado.", datos: r.Movimiento);
                return Respuestas.Ok(r.Movimiento!, "Movimiento actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Movimiento #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el movimiento #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el movimiento #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el movimiento.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioMovimientosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Movimiento";
            log.Info(proceso, $"Dando de baja movimiento #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Movimiento #{id} dado de baja.", datos: r.Movimiento);
                return Respuestas.Ok(r.Movimiento!, "Movimiento dado de baja correctamente.");
            }
            log.Warn(proceso, $"Movimiento #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el movimiento #{id}.");
        });
    }
}
