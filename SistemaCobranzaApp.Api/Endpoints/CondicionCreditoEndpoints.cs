using System.Net.Http; // Para HttpMethod si fuera necesario externamente
using Microsoft.AspNetCore.Mvc; // Mantener para consistencia de APIs
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class CondicionCreditoEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Cambiamos el nombre de la variable de 'publicas' a 'operativas' para evitar confusiones.
        // Sigue requiriendo token JWT gracias a .RequireAuthorization()
        var operativas = app.MapGroup("/api/condicion/credito").RequireAuthorization();
        
        // Inyecta ServicioCondicionCreditoAdmin en lugar de zonas
        operativas.MapGet("/", async (ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Condiciones Crédito Operativas";
            log.Info(proceso, "Consultando condiciones de crédito activas.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Condiciones de crédito activas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Condiciones de crédito activas obtenidas.");
        });

        var admin = app.MapGroup("/api/admin/condicion/credito").RequireAuthorization();

        admin.MapGet("/", async (ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Condiciones Crédito (Admin)";
            log.Info(proceso, "Consultando todas las condiciones de crédito.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Condiciones de crédito obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Condiciones de crédito obtenidas.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Condición Crédito";
            log.Info(proceso, $"Buscando condición de crédito #{id}.", datos: new { id });
            var c = await s.ObtenerAsync(id, ct);
            if (c is null)
            {
                log.Warn(proceso, $"Condición de crédito #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la condición de crédito #{id}.");
            }
            log.Info(proceso, $"Condición de crédito #{id} obtenida.", datos: c);
            return Respuestas.Ok(c, "Condición de crédito obtenida.");
        });

        admin.MapPost("/", async (CrearCondicionCreditoRequest req, ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Condición Crédito";
            log.Info(proceso, $"Creando condición de crédito: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Condición de crédito creada #{r.CondicionCredito!.IdCondicion} ('{r.CondicionCredito.Descripcion}').", datos: r.CondicionCredito);
                return Respuestas.Creado($"/api/admin/condicion/credito/{r.CondicionCredito.IdCondicion}", r.CondicionCredito, "Condición de crédito creada correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear la condición de crédito: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear la condición de crédito.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarCondicionCreditoRequest req, ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Condición Crédito";
            log.Info(proceso, $"Actualizando condición de crédito #{id} a '{req.Descripcion}'.", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Condición de crédito #{id} actualizada.", datos: r.CondicionCredito);
                return Respuestas.Ok(r.CondicionCredito!, "Condición de crédito actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Condición de crédito #{id} no encontrada.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la condición de crédito #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar la condición de crédito #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar la condición de crédito.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioCondicionCreditoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Condición Crédito";
            log.Info(proceso, $"Dando de baja condición de crédito #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Condición de crédito #{id} dada de baja.", datos: r.CondicionCredito);
                return Respuestas.Ok(r.CondicionCredito!, "Condición de crédito dada de baja correctamente.");
            }
            log.Warn(proceso, $"Condición de crédito #{id} no encontrada.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la condición de crédito #{id}.");
        });
    }
}
