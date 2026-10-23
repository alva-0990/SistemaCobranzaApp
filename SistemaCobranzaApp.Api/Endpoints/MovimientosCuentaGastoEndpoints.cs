
using System.Security.Claims;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints administrativos para consultar y registrar movimientos
/// de las cuentas de gastos. Todas las operaciones requieren autenticación.
/// </summary>
public class MovimientosCuentaGastoEndpoints : IEndpointModule
{
    private static MovimientoCuentaGastoDto MapearDto(MovimientoCuentaGasto m) =>
        new(
            m.IdMovimientoCuenta,
            m.IdCuentaGasto,
            m.IdPagoGasto,
            m.TipoMovimiento,
            m.Importe,
            m.FechaMovimiento,
            m.Concepto,
            m.Referencia,
            m.Observaciones,
            m.IdUsuarioRegistro);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        static long? ObtenerIdUsuario(ClaimsPrincipal usuario)
        {
            var valorIdUsuario =
                usuario.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? usuario.FindFirstValue("sub");

            return long.TryParse(valorIdUsuario, out var idUsuario) && idUsuario > 0
                ? idUsuario
                : null;
        }

        var admin = app
            .MapGroup("/api/admin/cuentas-gasto/{idCuentaGasto:long}/movimientos")
            .RequireAuthorization();

        // ============================================================
        // LISTAR MOVIMIENTOS DE UNA CUENTA
        // GET /api/admin/cuentas-gasto/{idCuentaGasto}/movimientos
        // ============================================================
        admin.MapGet("/", async (
            long idCuentaGasto,
            DateTimeOffset? desde,
            DateTimeOffset? hasta,
            ServicioMovimientosCuentaGasto s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Listar Movimientos de Cuenta de Gasto";

            log.Info(proceso, $"Consultando movimientos de la cuenta #{idCuentaGasto}.",
                datos: new { idCuentaGasto, desde, hasta });

            if (idCuentaGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudieron consultar los movimientos.",
                    "El identificador de la cuenta debe ser mayor que cero.");

            if (desde.HasValue && hasta.HasValue && desde.Value > hasta.Value)
                return Respuestas.SolicitudInvalida(
                    "El rango de fechas no es válido.",
                    "La fecha inicial no puede ser posterior a la fecha final.");

            var lista = await s.ListarAsync(idCuentaGasto, desde, hasta, ct);
            var dto = lista.Select(MapearDto).ToList();

            log.Info(proceso, $"Movimientos obtenidos: {dto.Count}.",
                datos: new { idCuentaGasto, cantidad = dto.Count });

            return Respuestas.Ok(dto, "Movimientos de la cuenta obtenidos correctamente.");
        })
        .WithName("ListarMovimientosCuentaGasto");

        // ============================================================
        // REGISTRAR MOVIMIENTO MANUAL
        // POST /api/admin/cuentas-gasto/{idCuentaGasto}/movimientos
        // ============================================================
        admin.MapPost("/", async (
            long idCuentaGasto,
            CrearMovimientoCuentaGastoRequest req,
            ClaimsPrincipal usuario,
            ServicioMovimientosCuentaGasto s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Registrar Movimiento de Cuenta de Gasto";

            log.Info(proceso, $"Registrando movimiento para la cuenta #{idCuentaGasto}.",
                datos: new { idCuentaGasto, req.TipoMovimiento, req.Importe, req.Concepto, req.Referencia });

            if (idCuentaGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el movimiento.",
                    "El identificador de la cuenta debe ser mayor que cero.");

            var idUsuarioRegistro = ObtenerIdUsuario(usuario);

            if (idUsuarioRegistro is null)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el movimiento.",
                    "El token no contiene un identificador de usuario válido.");

            var r = await s.RegistrarManualAsync(
                idCuentaGasto,
                req.TipoMovimiento,
                req.Importe,
                req.Concepto,
                idUsuarioRegistro.Value,
                req.Referencia,
                req.Observaciones,
                ct);

            if (r.Exito)
            {
                log.Info(proceso,
                    $"Movimiento #{r.Datos!.IdMovimientoCuenta} registrado para la cuenta #{idCuentaGasto}.");

                return Respuestas.Creado(
                    $"/api/admin/cuentas-gasto/{idCuentaGasto}/movimientos/{r.Datos.IdMovimientoCuenta}",
                    MapearDto(r.Datos),
                    "Movimiento registrado correctamente.");
            }

            if (r.NoEncontrado)
                return Respuestas.NoEncontrado(
                    r.Error ?? $"No se encontró la cuenta de gasto #{idCuentaGasto}.");

            return Respuestas.SolicitudInvalida(
                "No se pudo registrar el movimiento.",
                r.Error ?? "Ocurrió un error al registrar el movimiento.");
        })
        .WithName("RegistrarMovimientoCuentaGasto");
    }
}
