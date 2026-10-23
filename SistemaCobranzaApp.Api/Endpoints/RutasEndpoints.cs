using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class RutasEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var operativas = app.MapGroup("/api/rutas").RequireAuthorization();

        // Solo rutas activas (para selectores/combos comunes).
        operativas.MapGet("/", async (long? idZona, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Rutas Operativas";
            log.Info(proceso, "Consultando rutas activas.");
            var lista = await s.ListarActivasAsync(idZona, ct);
            log.Info(proceso, $"Rutas activas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Rutas activas obtenidas.");
        });

        var admin = app.MapGroup("/api/admin/rutas").RequireAuthorization();

        admin.MapGet("/", async (bool? incluirInactivas, long? idZona, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Rutas (Admin)";
            log.Info(proceso, "Consultando rutas.");
            var lista = await s.ListarAsync(incluirInactivas, idZona, ct);
            log.Info(proceso, $"Rutas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Rutas obtenidas.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Ruta";
            log.Info(proceso, $"Buscando ruta #{id}.");
            var x = await s.ObtenerAsync(id, ct);
            if (x is null)
            {
                log.Warn(proceso, $"Ruta #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la ruta #{id}.");
            }
            log.Info(proceso, $"Ruta #{id} obtenida.", datos: x);
            return Respuestas.Ok(x, "Ruta obtenida.");
        });

        admin.MapPost("/", async (CrearRutaRequest req, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Ruta";
            log.Info(proceso, $"Creando ruta: '{req.Descripcion}'.");
            var r = await s.CrearAsync(req.Descripcion, req.IdZona, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Ruta creada #{r.Ruta!.IdRuta} ('{r.Ruta.Descripcion}').", datos: r.Ruta);
                return Respuestas.Creado($"/api/admin/rutas/{r.Ruta.IdRuta}", r.Ruta, "Ruta creada correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear la ruta: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear la ruta.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarRutaRequest req, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Ruta";
            log.Info(proceso, $"Actualizando ruta #{id} a '{req.Descripcion}'.");
            var r = await s.ActualizarAsync(id, req.Descripcion, req.IdZona, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Ruta #{id} actualizada.", datos: r.Ruta);
                return Respuestas.Ok(r.Ruta!, "Ruta actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Ruta #{id} no encontrada.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la ruta #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar la ruta #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar la ruta.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioRutasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Ruta";
            log.Info(proceso, $"Dando de baja ruta #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Ruta #{id} dada de baja.", datos: r.Ruta);
                return Respuestas.Ok(r.Ruta!, "Ruta dada de baja correctamente.");
            }
            log.Warn(proceso, $"Ruta #{id} no encontrada.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la ruta #{id}.");
        });
    }
}
