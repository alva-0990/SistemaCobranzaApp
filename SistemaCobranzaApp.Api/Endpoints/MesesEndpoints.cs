using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class MesesEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ---- Operativas (solo activas) ----
        var operativas = app.MapGroup("/api/meses").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Meses Operativos";
            log.Info(proceso, "Consultando meses activos.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Meses activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Meses activos obtenidos.");
        });

        // ---- Panel de Administración ----
        var admin = app.MapGroup("/api/admin/meses").RequireAuthorization();

        admin.MapGet("/", async (ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Meses (Admin)";
            log.Info(proceso, "Consultando todos los meses.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Meses obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Meses obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Mes";
            log.Info(proceso, $"Buscando mes #{id}.", datos: new { id });
            var m = await s.ObtenerAsync(id, ct);
            if (m is null)
            {
                log.Warn(proceso, $"Mes #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el mes #{id}.");
            }
            log.Info(proceso, $"Mes #{id} obtenido.", datos: m);
            return Respuestas.Ok(m, "Mes obtenido.");
        });

        admin.MapPost("/", async (CrearMesRequest req, ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Mes";
            log.Info(proceso, $"Creando mes: '{req.Descripcion}' ({req.NumMeses} meses).", datos: req);
            var r = await s.CrearAsync(req.NumMeses, req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Mes creado #{r.Mes!.Id} ('{r.Mes.Descripcion}').", datos: r.Mes);
                return Respuestas.Creado($"/api/admin/meses/{r.Mes.Id}", r.Mes, "Mes creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el mes: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el mes.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarMesRequest req, ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Mes";
            log.Info(proceso, $"Actualizando mes #{id} a '{req.Descripcion}' ({req.NumMeses} meses).", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.NumMeses, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Mes #{id} actualizado.", datos: r.Mes);
                return Respuestas.Ok(r.Mes!, "Mes actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Mes #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el mes #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el mes #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el mes.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioMesesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Mes";
            log.Info(proceso, $"Dando de baja mes #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Mes #{id} dado de baja.", datos: r.Mes);
                return Respuestas.Ok(r.Mes!, "Mes dado de baja correctamente.");
            }
            log.Warn(proceso, $"Mes #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el mes #{id}.");
        });
    }
}
