using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class ClasificaionClienteEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Cambiamos el nombre de la variable de 'publicas' a 'operativas' para evitar confusiones.
        // Sigue requiriendo token JWT gracias a .RequireAuthorization()
        var operativas = app.MapGroup("/api/clasificacion/cliente").RequireAuthorization();
        
        // 👈 Cambiado aquí: Inyecta ServicioClasificacionClienteAdmin en lugar del público
        operativas.MapGet("/", async (ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Clasificacion de Clientes Operativas";
            log.Info(proceso, "Consultando clasificacion de Clientes activas.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Clasificacion de Clientes activas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Clasificacion de Clientes activas obtenidas.");
        });

        var admin = app.MapGroup("/api/admin/clasificacion/cliente").RequireAuthorization();

        admin.MapGet("/", async (ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Clasificacion de Clientes (Admin)";
            log.Info(proceso, "Consultando todas las clasificacion de Clientes.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Clasificacion de Clientes obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Clasificacion de Clientes obtenidas.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Clasificaion cliente";
            log.Info(proceso, $"Buscando zona #{id}.", datos: new { id });
            var z = await s.ObtenerAsync(id, ct);
            if (z is null)
            {
                log.Warn(proceso, $"Clasificaion cliente #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la zona #{id}.");
            }
            log.Info(proceso, $"Clasificaion cliente #{id} obtenida.", datos: z);
            return Respuestas.Ok(z, "Clasificaion cliente obtenida.");
        });

        admin.MapPost("/", async (CrearClasificaionClienteRequest req, ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Clasificacion Cliente";
            log.Info(proceso, $"Creando Clasificacion CLiente: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Clasificacion Cliente creada #{r.ClasificacionCliente!.IdClasificaionCliente} ('{r.ClasificacionCliente.Descripcion}').", datos: r.ClasificacionCliente);
                return Respuestas.Creado($"/api/admin/clasificacion/cliente/{r.ClasificacionCliente.IdClasificaionCliente}", r.ClasificacionCliente, "Clasificacion Cliente creada correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear la Clasificacion Cliente: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear la Clasificacion del Cliente.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarClasificaionClienteRequest req, ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Clasificacion Cliente";
            log.Info(proceso, $"Actualizando Clasificacion Cliente #{id} a '{req.Descripcion}'.", datos: new { id, req.Descripcion });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Clasificacion Cliente #{id} actualizada.", datos: r.ClasificacionCliente);
                return Respuestas.Ok(r.ClasificacionCliente!, "Clasificacion Cliente actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Clasificacion Cliente #{id} no encontrada.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la zona #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar la Clasificacion CLiente #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar la Clasificacion CLiente.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioClasificacionClienteAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Clasificacion Cliente";
            log.Info(proceso, $"Dando de baja Clasificacion CLiente #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Clasificacion Cliente #{id} dada de baja.", datos: r.ClasificacionCliente);
                return Respuestas.Ok(r.ClasificacionCliente!, "Clasifiacion Cliente dada de baja correctamente.");
            }
            log.Warn(proceso, $"Clasifiacion Cliente #{id} no encontrada.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la Clasificacion CLiente #{id}.");
        });
    }
}
