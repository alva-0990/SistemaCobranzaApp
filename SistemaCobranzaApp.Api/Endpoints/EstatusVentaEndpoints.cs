using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class EstatusVentaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ---- Operativas (solo activas) ----
        var operativas = app.MapGroup("/api/estatus/venta").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Estatus Venta Operativos";
            log.Info(proceso, "Consultando estatus de venta activos.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Estatus de venta activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Estatus de venta activos obtenidos.");
        });

        // ---- Panel de Administración ----
        var admin = app.MapGroup("/api/admin/estatus/venta").RequireAuthorization();

        admin.MapGet("/", async (ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Estatus Venta (Admin)";
            log.Info(proceso, "Consultando todos los estatus de venta.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Estatus de venta obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Estatus de venta obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Estatus Venta";
            log.Info(proceso, $"Buscando estatus de venta #{id}.", datos: new { id });
            var e = await s.ObtenerAsync(id, ct);
            if (e is null)
            {
                log.Warn(proceso, $"Estatus de venta #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el estatus de venta #{id}.");
            }
            log.Info(proceso, $"Estatus de venta #{id} obtenido.", datos: e);
            return Respuestas.Ok(e, "Estatus de venta obtenido.");
        });

        admin.MapPost("/", async (CrearEstatusVentaRequest req, ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Estatus Venta";
            log.Info(proceso, $"Creando estatus de venta: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Estatus de venta creado #{r.EstatusVenta!.Id} ('{r.EstatusVenta.Descripcion}').", datos: r.EstatusVenta);
                return Respuestas.Creado($"/api/admin/estatus/venta/{r.EstatusVenta.Id}", r.EstatusVenta, "Estatus de venta creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el estatus de venta: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el estatus de venta.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarEstatusVentaRequest req, ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Estatus Venta";
            log.Info(proceso, $"Actualizando estatus de venta #{id} a '{req.Descripcion}'.", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Estatus de venta #{id} actualizado.", datos: r.EstatusVenta);
                return Respuestas.Ok(r.EstatusVenta!, "Estatus de venta actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Estatus de venta #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el estatus de venta #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el estatus de venta #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el estatus de venta.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioEstatusVentaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Estatus Venta";
            log.Info(proceso, $"Dando de baja estatus de venta #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Estatus de venta #{id} dado de baja.", datos: r.EstatusVenta);
                return Respuestas.Ok(r.EstatusVenta!, "Estatus de venta dado de baja correctamente.");
            }
            log.Warn(proceso, $"Estatus de venta #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el estatus de venta #{id}.");
        });
    }
}
