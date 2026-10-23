using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Seguridad;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class RolesEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/roles").RequireAuthorization();

        admin.MapGet("/", async (bool? incluirInactivos, ServicioRolesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Roles (Admin)";
            log.Info(proceso, "Consultando roles.");
            var lista = await s.ListarAsync(incluirInactivos, ct);
            log.Info(proceso, $"Roles obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Roles obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioRolesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Rol";
            log.Info(proceso, $"Buscando rol #{id}.");
            var r = await s.ObtenerAsync(id, ct);
            if (r is null)
            {
                log.Warn(proceso, $"Rol #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el rol #{id}.");
            }
            log.Info(proceso, $"Rol #{id} obtenido.", datos: r);
            return Respuestas.Ok(r, "Rol obtenido.");
        });

        admin.MapPost("/", async (CrearRolRequest req, ServicioRolesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Rol";
            log.Info(proceso, $"Creando rol: '{req.Descripcion}'.");
            var r = await s.CrearAsync(req.Descripcion, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Rol creado #{r.Rol!.IdRol} ('{r.Rol.Descripcion}').", datos: r.Rol);
                return Respuestas.Creado($"/api/admin/roles/{r.Rol.IdRol}", r.Rol, "Rol creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el rol: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear el rol.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarRolRequest req, ServicioRolesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Rol";
            log.Info(proceso, $"Actualizando rol #{id} a '{req.Descripcion}'.");
            var r = await s.ActualizarAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Rol #{id} actualizado.", datos: r.Rol);
                return Respuestas.Ok(r.Rol!, "Rol actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Rol #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el rol #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el rol #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar el rol.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioRolesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Rol";
            log.Info(proceso, $"Dando de baja rol #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Rol #{id} dado de baja.", datos: r.Rol);
                return Respuestas.Ok(r.Rol!, "Rol dado de baja correctamente.");
            }
            log.Warn(proceso, $"Rol #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el rol #{id}.");
        });
    }
}
