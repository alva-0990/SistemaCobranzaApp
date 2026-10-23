using System.Security.Claims;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Seguridad;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class AuthEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var cuenta = app.MapGroup("/api/auth").RequireAuthorization();
        cuenta.MapGet("/perfil", async (ClaimsPrincipal user, ServicioUsuariosAdmin s, CancellationToken ct) =>
        {
            if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();
            var perfil = await s.ObtenerAsync(id, ct);
            return perfil is null || !perfil.Activo
                ? Respuestas.NoEncontrado("El usuario no está disponible.")
                : Respuestas.Ok(perfil, "Perfil obtenido.");
        });
        cuenta.MapPut("/perfil", async (ActualizarPerfilRequest req, ClaimsPrincipal user, ServicioUsuariosAdmin s, CancellationToken ct) =>
        {
            if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();
            var r = await s.ActualizarPerfilAsync(id, req, ct);
            return r.Exito ? Respuestas.Ok(r.Usuario!, "Perfil actualizado.")
                : Respuestas.SolicitudInvalida("No se pudo actualizar el perfil.", r.Error ?? "");
        });
        cuenta.MapPut("/password", async (ActualizarPasswordRequest req, ClaimsPrincipal user, ServicioUsuariosAdmin s, CancellationToken ct) =>
        {
            if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();
            var r = await s.ActualizarPasswordAsync(id, req, ct);
            return r.Exito ? Respuestas.Ok(r.Usuario!, "Contraseña actualizada.")
                : Respuestas.SolicitudInvalida("No se pudo actualizar la contraseña.", r.Error ?? "");
        });

        // Renueva la sesión mientras el token actual siga vigente (ValidateLifetime lo exige).
        // Reemite un LoginResponse con un token nuevo y Expira recalculado. La app lo invoca
        // automáticamente al recuperar señal para extender la jornada offline sin re-login.
        cuenta.MapPost("/refresh", async (ClaimsPrincipal user, ServicioAutenticacion auth, CancellationToken ct) =>
        {
            if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();
            var resultado = await auth.RefreshAsync(id, ct);
            return resultado.Exito
                ? Respuestas.Ok(resultado.Respuesta!, "Sesión renovada.")
                : Respuestas.Error(401, CodigosApi.NoAutorizado,
                    "No se pudo renovar la sesión.",
                    detalles: resultado.Error is null ? null : new[] { resultado.Error });
        }).WithName("RefreshToken");

        // Endpoint de login (público, sin autorización).
        // Respuesta con el sobre estándar ApiRespuesta<LoginResponse>:
        //   éxito -> 200 con Resultado = LoginResponse (incluye el token).
        //   fallo -> 401 con codigo/mensaje/detalles (sin Resultado).
        // NUNCA se registra la contraseña ni el token en los logs.
        app.MapPost("/api/auth/login", async (LoginRequest req, ServicioAutenticacion auth, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Login";
            log.Info(proceso, $"Intento de login para usuario '{req.Usuario}'.");

            var resultado = await auth.LoginAsync(req, ct);
            if (resultado.Exito)
            {
                // No se registra el token JWT: solo datos no sensibles del usuario.
                var r = resultado.Respuesta!;
                log.Info(proceso, $"Login exitoso para '{req.Usuario}' (idUsuario={r.IdUsuario}).",
                    datos: new { r.IdUsuario, r.Usuario, r.NombreCompleto, r.IdRol, r.Rol });
                return Respuestas.Ok(resultado.Respuesta!, "Inicio de sesión exitoso.");
            }

            log.Warn(proceso, $"Login fallido para '{req.Usuario}': {resultado.Error}");
            return Respuestas.Error(401, CodigosApi.NoAutorizado,
                "Usuario o contraseña incorrectos.",
                detalles: resultado.Error is null ? null : new[] { resultado.Error });
        })
        .WithName("Login");
    }
}
