using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class CobradoresEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var operativas = app.MapGroup("/api/cobradores").RequireAuthorization();

        // Solo cobradores activos (para selectores/combos comunes).
        operativas.MapGet("/", async (long? idZona, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Cobradores Operativos";
            log.Info(proceso, "Consultando cobradores activos.");
            var lista = await s.ListarActivasAsync(idZona, ct);
            log.Info(proceso, $"Cobradores activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Cobradores activos obtenidos.");
        });

        var admin = app.MapGroup("/api/admin/cobradores").RequireAuthorization();

        admin.MapGet("/", async (bool? incluirInactivos, long? idZona, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Cobradores (Admin)";
            log.Info(proceso, "Consultando cobradores.");
            var lista = await s.ListarAsync(incluirInactivos, idZona, ct);
            log.Info(proceso, $"Cobradores obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Cobradores obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Cobrador";
            log.Info(proceso, $"Buscando cobrador #{id}.");
            var x = await s.ObtenerAsync(id, ct);
            if (x is null)
            {
                log.Warn(proceso, $"Cobrador #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el cobrador #{id}.");
            }
            log.Info(proceso, $"Cobrador #{id} obtenido.", datos: x);
            return Respuestas.Ok(x, "Cobrador obtenido.");
        });

        admin.MapPost("/", async (CrearCobradorRequest req, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Cobrador";
            log.Info(proceso, $"Creando cobrador: '{req.Nombre}'.");
            var r = await s.CrearAsync(req.Nombre, req.IdZona, ct, req.IdUsuario);
            if (r.Exito)
            {
                log.Info(proceso, $"Cobrador creado #{r.Cobrador!.IdCobrador} ('{r.Cobrador.Nombre}').", datos: r.Cobrador);
                return Respuestas.Creado($"/api/admin/cobradores/{r.Cobrador.IdCobrador}", r.Cobrador, "Cobrador creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el cobrador: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear el cobrador.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarCobradorRequest req, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Cobrador";
            log.Info(proceso, $"Actualizando cobrador #{id} a '{req.Nombre}'.");
            var r = await s.ActualizarAsync(id, req.Nombre, req.IdZona, req.Activo, ct, req.IdUsuario);
            if (r.Exito)
            {
                log.Info(proceso, $"Cobrador #{id} actualizado.", datos: r.Cobrador);
                return Respuestas.Ok(r.Cobrador!, "Cobrador actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Cobrador #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el cobrador #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el cobrador #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar el cobrador.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioCobradoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Cobrador";
            log.Info(proceso, $"Dando de baja cobrador #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Cobrador #{id} dado de baja.", datos: r.Cobrador);
                return Respuestas.Ok(r.Cobrador!, "Cobrador dado de baja correctamente.");
            }
            log.Warn(proceso, $"Cobrador #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el cobrador #{id}.");
        });
    }
}
