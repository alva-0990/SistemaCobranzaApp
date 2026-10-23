using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Seguridad;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class UsuariosEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/usuarios").RequireAuthorization();

        admin.MapGet("/", async (bool? incluirInactivos, ServicioUsuariosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Usuarios (Admin)";
            log.Info(proceso, "Consultando usuarios.");
            var lista = await s.ListarAsync(incluirInactivos, ct);
            // UsuarioDto NUNCA incluye contraseña/hash, es seguro loguearlo.
            log.Info(proceso, $"Usuarios obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Usuarios obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioUsuariosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Usuario";
            log.Info(proceso, $"Buscando usuario #{id}.");
            var u = await s.ObtenerAsync(id, ct);
            if (u is null)
            {
                log.Warn(proceso, $"Usuario #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el usuario #{id}.");
            }
            log.Info(proceso, $"Usuario #{id} obtenido.", datos: u);
            return Respuestas.Ok(u, "Usuario obtenido.");
        });

        admin.MapPost("/", async (CrearUsuarioRequest req, ServicioUsuariosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Usuario";
            // No se registra la contraseña: solo campos no sensibles.
            log.Info(proceso, $"Creando usuario: '{req.Usuario}' (rol={req.IdRol}).");
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Usuario creado #{r.Usuario!.IdUsuario} ('{r.Usuario.Usuario}').", datos: r.Usuario);
                return Respuestas.Creado($"/api/admin/usuarios/{r.Usuario.IdUsuario}", r.Usuario, "Usuario creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el usuario: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear el usuario.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarUsuarioRequest req, ServicioUsuariosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Usuario";
            // No se registra la contraseña: solo campos no sensibles.
            log.Info(proceso, $"Actualizando usuario #{id} ('{req.Usuario}', rol={req.IdRol}).");
            var r = await s.ActualizarAsync(id, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Usuario #{id} actualizado.", datos: r.Usuario);
                return Respuestas.Ok(r.Usuario!, "Usuario actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Usuario #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el usuario #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el usuario #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar el usuario.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioUsuariosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Usuario";
            log.Info(proceso, $"Dando de baja usuario #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Usuario #{id} dado de baja.", datos: r.Usuario);
                return Respuestas.Ok(r.Usuario!, "Usuario dado de baja correctamente.");
            }
            log.Warn(proceso, $"Usuario #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el usuario #{id}.");
        });
    }
}
