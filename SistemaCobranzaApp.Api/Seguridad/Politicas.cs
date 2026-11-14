using SistemaCobranzaApp.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Shared.Seguridad;

namespace SistemaCobranzaApp.Api.Seguridad;

/// <summary>
/// Políticas de autorización por rol. Se basan en el claim "idrol" que emite JwtTokenGenerator,
/// no en el nombre del rol (que es editable). Superadmin pasa siempre.
/// </summary>
public static class Politicas
{
    public const string ClaimIdRol = "idrol";

    /// <summary>Solo Administrador (y Superadmin).</summary>
    public const string SoloAdmin = "SoloAdmin";

    /// <summary>Administrador o Supervisor (y Superadmin).</summary>
    public const string AdminSupervisor = "AdminSupervisor";

    /// <summary>Administrador, Supervisor o Auxiliar administrativo (y Superadmin).</summary>
    public const string AdminSupervisorAuxiliar = "AdminSupervisorAuxiliar";

    public static void Registrar(AuthorizationOptions opciones)
    {
        opciones.AddPolicy(SoloAdmin, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => TieneRol(ctx.User, RolesSistema.Administrador)));

        opciones.AddPolicy(AdminSupervisor, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => TieneRol(ctx.User, RolesSistema.Administrador, RolesSistema.Supervisor)));

        opciones.AddPolicy(AdminSupervisorAuxiliar, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => TieneRol(ctx.User, RolesSistema.Administrador, RolesSistema.Supervisor,
                RolesSistema.AuxiliarAdministrativo)));
    }

    /// <summary>
    /// Restringe TODO el grupo a los roles indicados (más Superadmin). Úsalo en grupos donde incluso
    /// la lectura debe limitarse (p. ej. WhatsApp).
    /// </summary>
    public static RouteGroupBuilder RequiereRoles(this RouteGroupBuilder grupo, params long[] roles)
    {
        grupo.AddEndpointFilter(async (ctx, next) =>
            TieneRol(ctx.HttpContext.User, roles) ? await next(ctx) : Prohibido());
        return grupo;
    }

    /// <summary>
    /// Restringe solo las operaciones que modifican datos (todo lo que no sea GET/HEAD/OPTIONS).
    /// La lectura sigue abierta a cualquier usuario autenticado porque los catálogos también
    /// alimentan listas desplegables de otras pantallas y de la jornada.
    /// </summary>
    public static RouteGroupBuilder RequiereRolesParaEscribir(this RouteGroupBuilder grupo, params long[] roles)
    {
        grupo.AddEndpointFilter(async (ctx, next) =>
        {
            var m = ctx.HttpContext.Request.Method;
            var lectura = HttpMethods.IsGet(m) || HttpMethods.IsHead(m) || HttpMethods.IsOptions(m);
            return lectura || TieneRol(ctx.HttpContext.User, roles) ? await next(ctx) : Prohibido();
        });
        return grupo;
    }

    private static IResult Prohibido() =>
        Respuestas.Error(403, CodigosApi.Prohibido, "Tu rol no tiene permiso para realizar esta operación.");

    private static bool TieneRol(System.Security.Claims.ClaimsPrincipal usuario, params long[] permitidos)
    {
        if (!long.TryParse(usuario.FindFirst(ClaimIdRol)?.Value, out var idRol)) return false;
        return idRol == RolesSistema.Superadmin || permitidos.Contains(idRol);
    }
}
