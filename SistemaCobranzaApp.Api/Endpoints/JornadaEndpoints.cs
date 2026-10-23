using System.Security.Claims;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Sincronizacion;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Api.Endpoints;

public class JornadaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/jornada").RequireAuthorization();
        g.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (UnauthorizedAccessException ex)
            {
                return Respuestas.Error(403, CodigosApi.Prohibido, ex.Message);
            }
        });
        g.MapGet("/zona", async (ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.ObtenerZonaAsync(Id(user), ct), "Zona del usuario."));
        g.MapGet("/rutas", async (ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.RutasAsync(Id(user), ct), "Rutas autorizadas."));
        g.MapGet("/rutas/{ruta:long}/descarga", async (long ruta, ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.DescargarAsync(Id(user), ruta, ct), "Jornada descargada."));
        g.MapPost("/rutas/{ruta:long}/ventas", async (long ruta, VentaJornadaRequest req, ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.VenderAsync(Id(user), ruta, req, ct), "Resultado de sincronización."));
        g.MapPost("/clientes", async (ClienteJornadaRequest req, ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.CrearClienteAsync(Id(user), req, ct), "Resultado de sincronización de cliente."));
        g.MapPost("/pagos", async (PagoJornadaRequest req, ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.CobrarAsync(Id(user), req, ct), "Resultado de sincronización."));

        g.MapGet("/descarga", async (ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.DescargarAsync(Id(user), 0, ct), "Jornada de zona descargada."));
        g.MapPost("/ventas", async (VentaJornadaRequest req, ClaimsPrincipal user, ServicioJornada s, CancellationToken ct)
            => Respuestas.Ok(await s.VenderAsync(Id(user), 0, req, ct), "Resultado de sincronización."));

    }

    private static long Id(ClaimsPrincipal user)
        => long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
}
