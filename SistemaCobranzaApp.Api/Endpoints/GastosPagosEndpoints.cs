
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints administrativos para consultar y registrar pagos de gastos.
/// Todas las operaciones requieren autenticación.
/// </summary>
public class GastosPagosEndpoints : IEndpointModule
{
    private static GastoPagoDto MapearDto(GastoPago p, string nombreCuenta) =>
        new(
            p.IdPagoGasto,
            p.IdGasto,
            p.IdCuentaGasto,
            nombreCuenta,
            p.FechaPago,
            p.Importe,
            p.FormaPago,
            p.Referencia,
            p.Observaciones,
            p.IdUsuarioRegistro,
            p.FechaAlta);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        static long? ObtenerIdUsuario(ClaimsPrincipal usuario)
        {
            var valor = usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? usuario.FindFirstValue("sub");

            return long.TryParse(valor, out var id) && id > 0 ? id : null;
        }

        var admin = app
            .MapGroup("/api/admin/gastos/{idGasto:long}/pagos")
            .RequireAuthorization();

        // ============================================================
        // LISTAR PAGOS DE UN GASTO
        // GET /api/admin/gastos/{idGasto}/pagos
        // ============================================================
        admin.MapGet("/", async (
            long idGasto,
            ServicioGastosPagosAdmin s,
            CobranzaDbContext db,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Listar Pagos de Gasto";

            log.Info(proceso, $"Consultando pagos del gasto #{idGasto}.", datos: new { idGasto });

            if (idGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudieron consultar los pagos.",
                    "El identificador del gasto debe ser mayor que cero.");

            var lista = await s.ListarPorGastoAsync(idGasto, ct);

            // Nombres de cuenta en una sola consulta (evita N+1).
            var idsCuenta = lista.Select(x => x.IdCuentaGasto).Distinct().ToList();
            var nombres = await db.CuentasGasto
                .AsNoTracking()
                .Where(c => idsCuenta.Contains(c.IdCuentaGasto))
                .ToDictionaryAsync(c => c.IdCuentaGasto, c => c.Nombre, ct);

            var dto = lista.Select(p =>
                MapearDto(p, nombres.TryGetValue(p.IdCuentaGasto, out var n) ? n : string.Empty))
                .ToList();

            return Respuestas.Ok(dto, "Pagos del gasto obtenidos correctamente.");
        })
        .WithName("ListarPagosGasto");

        // ============================================================
        // REGISTRAR PAGO DE GASTO
        // POST /api/admin/gastos/{idGasto}/pagos
        // ============================================================
        admin.MapPost("/", async (
            long idGasto,
            RegistrarGastoPagoRequest req,
            ClaimsPrincipal usuario,
            ServicioGastosPagosAdmin s,
            CobranzaDbContext db,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Registrar Pago de Gasto";

            log.Info(proceso, $"Registrando pago para el gasto #{idGasto}.",
                datos: new { idGasto, req.IdCuentaGasto, req.Importe, req.FormaPago, req.FechaPago, req.Referencia });

            if (idGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el pago.",
                    "El identificador del gasto debe ser mayor que cero.");

            var idUsuarioRegistro = ObtenerIdUsuario(usuario);

            if (idUsuarioRegistro is null)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el pago.",
                    "El token no contiene un identificador de usuario válido.");

            var r = await s.RegistrarAsync(
                idGasto,
                req.IdCuentaGasto,
                req.Importe,
                req.FormaPago,
                idUsuarioRegistro.Value,
                req.FechaPago,
                req.Referencia,
                req.Observaciones,
                ct);

            if (r.Exito)
            {
                var nombreCuenta = await db.CuentasGasto
                    .AsNoTracking()
                    .Where(c => c.IdCuentaGasto == r.Datos!.IdCuentaGasto)
                    .Select(c => c.Nombre)
                    .FirstOrDefaultAsync(ct) ?? string.Empty;

                return Respuestas.Creado(
                    $"/api/admin/gastos/{idGasto}/pagos/{r.Datos!.IdPagoGasto}",
                    MapearDto(r.Datos, nombreCuenta),
                    "Pago registrado correctamente.");
            }

            if (r.NoEncontrado)
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el gasto #{idGasto}.");

            return Respuestas.SolicitudInvalida(
                "No se pudo registrar el pago.",
                r.Error ?? "Ocurrió un error al registrar el pago.");
        })
        .WithName("RegistrarPagoGasto");
    }
}
