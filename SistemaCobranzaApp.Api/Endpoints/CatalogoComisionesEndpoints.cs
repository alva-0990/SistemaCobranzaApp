using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.CatalogoComisiones;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class CatalogoComisionesEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // 1. Grupo Operativo (Para selectores o combobox comunes en el registro de productos)
        var operativas = app.MapGroup("/api/comisiones").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Comisiones Operativas";
            log.Info(proceso, "Consultando esquemas de comisión activos.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Esquemas activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Esquemas de comisión activos obtenidos.");
        });

        // 2. Grupo Administrativo (CRUD completo para el panel de control)
        var admin = app.MapGroup("/api/admin/comisiones").RequireAuthorization();

        admin.MapGet("/", async (ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Comisiones (Admin)";
            log.Info(proceso, "Consultando todos los esquemas de comisión.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Todos los esquemas obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Todos los esquemas de comisión obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Esquema Comisión";
            log.Info(proceso, $"Buscando esquema de comisión #{id}.", datos: new { id });
            var c = await s.ObtenerAsync(id, ct);
            if (c is null)
            {
                log.Warn(proceso, $"Esquema #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el esquema de comisión #{id}.");
            }
            log.Info(proceso, $"Esquema #{id} obtenido.", datos: c);
            return Respuestas.Ok(c, "Esquema de comisión obtenido.");
        });

        admin.MapPost("/", async (CrearCatalogoComisionRequest req, ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Esquema Comisión";
            log.Info(proceso, $"Creando esquema de comisión: '{req.NombreEsquema}'.", datos: req);
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Esquema creado #{r.Comision!.Id} ('{r.Comision.NombreEsquema}').", datos: r.Comision);
                return Respuestas.Creado($"/api/admin/comisiones/{r.Comision.Id}", r.Comision, "Esquema de comisión creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el esquema de comisión: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el esquema de comisión.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarCatalogoComisionRequest req, ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Esquema Comisión";
            log.Info(proceso, $"Actualizando esquema #{id} a '{req.NombreEsquema}'.", datos: new { id, Request = req });
            var r = await s.ActualizarAsync(id, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Esquema de comisión #{id} actualizado.", datos: r.Comision);
                return Respuestas.Ok(r.Comision!, "Esquema de comisión actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Esquema de comisión #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el esquema de comisión #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el esquema #{id}: {r.Error}", datos: new { id, Request = req });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el esquema de comisión.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioCatalogoComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Esquema Comisión";
            log.Info(proceso, $"Dando de baja esquema de comisión #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Esquema de comisión #{id} dado de baja.", datos: r.Comision);
                return Respuestas.Ok(r.Comision!, "Esquema de comisión dado de baja correctamente.");
            }
            log.Warn(proceso, $"Esquema de comisión #{id} no encontrado para baja.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el esquema de comisión #{id}.");
        });
    }
}
