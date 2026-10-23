
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class GastosEndpoints : IEndpointModule
{
    private static GastoDto MapearDto(Gasto g, decimal totalPagado) =>
        new(
            g.IdGasto,
            g.IdCategoriaGasto,
            g.Categoria?.Nombre ?? string.Empty,
            g.FolioGasto,
            g.FechaGasto,
            g.Concepto,
            g.Descripcion,
            g.Proveedor,
            g.RFCProveedor,
            g.Referencia,
            g.Importe,
            totalPagado,
            g.Importe - totalPagado,
            g.Estatus,
            g.IdZona,
            g.Zona?.Descripcion,
            g.IdCobrador,
            g.Cobrador?.Nombre,
            g.IdVendedor,
            g.Vendedor?.Nombre,
            g.IdCuentaPrevista,
            g.CuentaPrevista?.Nombre,
            g.Observaciones,
            g.IdUsuarioRegistro,
            g.IdUsuarioAutoriza,
            g.FechaAutorizacion,
            g.IdUsuarioCancelacion,
            g.FechaCancelacion,
            g.MotivoCancelacion,
            g.FechaAlta);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        static long? ObtenerIdUsuario(ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

            return long.TryParse(raw, out var id) ? id : null;
        }

        var api = app.MapGroup("/api/admin/gastos")
            .RequireAuthorization();

        // 1. Listar gastos con filtros opcionales.
        api.MapGet("/", async (
            DateTimeOffset? desde,
            DateTimeOffset? hasta,
            string? estatus,
            long? idCategoria,
            long? idZona,
            long? idCobrador,
            long? idVendedor,
            string? folio,
            bool? soloCentrales,
            ServicioGastosAdmin s,
            CobranzaDbContext db,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Consultar Gastos (Admin)";

            try
            {
                log.Info(proceso, "Consultando gastos administrativos.");

                var lista = await s.ListarAsync(
                    desde, hasta, estatus, idCategoria,
                    idZona, idCobrador, idVendedor, folio,
                    soloCentrales ?? false, ct);

                // Total pagado por gasto en una sola consulta (evita N+1 y
                // no duplica importes al relacionar con pagos).
                var ids = lista.Select(x => x.IdGasto).ToList();

                var pagosPorGasto = await db.GastosPagos
                    .AsNoTracking()
                    .Where(p => ids.Contains(p.IdGasto))
                    .GroupBy(p => p.IdGasto)
                    .Select(g => new { g.Key, Total = g.Sum(x => x.Importe) })
                    .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

                var dto = lista.Select(g =>
                    MapearDto(g, pagosPorGasto.TryGetValue(g.IdGasto, out var t) ? t : 0m))
                    .ToList();

                return Respuestas.Ok(dto, "Gastos consultados.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex);
                return Respuestas.ErrorInterno("No fue posible consultar los gastos.");
            }
        }).WithName("ListarGastosAdmin");

        // 2. Obtener el detalle de un gasto.
        api.MapGet("/{id:long}", async (
            long id,
            ServicioGastosAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Consultar Detalle de Gasto (Admin)";

            try
            {
                var gasto = await s.ObtenerAsync(id, ct);

                if (gasto is null)
                    return Respuestas.NoEncontrado($"No se encontró el gasto #{id}.");

                var totalPagado = gasto.Pagos?.Sum(p => p.Importe) ?? 0m;

                return Respuestas.Ok(MapearDto(gasto, totalPagado), "Gasto obtenido.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex, datos: new { id });
                return Respuestas.ErrorInterno("No fue posible consultar el gasto.");
            }
        }).WithName("ObtenerGastoAdmin");

        // 3. Crear un gasto. El folio se genera en el servicio.
        api.MapPost("/", async (
            CrearGastoRequest req,
            ClaimsPrincipal user,
            ServicioGastosAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Alta de Gasto (Admin)";

            var idUsuario = ObtenerIdUsuario(user);

            log.Info(proceso,
                $"Registrando gasto (usuario #{idUsuario?.ToString() ?? "desconocido"}).",
                datos: req);

            var r = await s.CrearAsync(
                req.IdCategoriaGasto,
                req.FechaGasto,
                req.Concepto,
                req.Importe,
                idUsuario ?? 0,
                req.Descripcion,
                req.Proveedor,
                req.RFCProveedor,
                req.Referencia,
                req.IdZona,
                req.IdCobrador,
                req.IdVendedor,
                req.IdCuentaPrevista,
                req.Observaciones,
                ct);

            if (r.Exito)
                return Respuestas.Creado(
                    $"/api/admin/gastos/{r.Datos!.IdGasto}",
                    MapearDto(r.Datos, 0m),
                    "Gasto registrado administrativamente.");

            return Respuestas.SolicitudInvalida(
                "No se pudo registrar el gasto.", r.Error ?? "");
        }).WithName("CrearGastoAdmin");

        // 4. Editar un gasto (solo sin pagos y no cancelado).
        api.MapPut("/{id:long}", async (
            long id,
            ActualizarGastoRequest req,
            ClaimsPrincipal user,
            ServicioGastosAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Editar Gasto (Admin)";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo editar el gasto.",
                    "El identificador debe ser mayor que cero.");

            log.Info(proceso, $"Editando gasto #{id}.", datos: req);

            var r = await s.ActualizarAsync(
                id,
                req.IdCategoriaGasto,
                req.FechaGasto,
                req.Concepto,
                req.Importe,
                req.Descripcion,
                req.Proveedor,
                req.RFCProveedor,
                req.Referencia,
                req.IdZona,
                req.IdCobrador,
                req.IdVendedor,
                req.IdCuentaPrevista,
                req.Observaciones,
                ct);

            if (r.Exito)
            {
                var totalPagado = await s.TotalPagadoAsync(id, ct);
                return Respuestas.Ok(MapearDto(r.Datos!, totalPagado), "Gasto actualizado.");
            }

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró el gasto #{id}.")
                : Respuestas.SolicitudInvalida("No se pudo editar el gasto.", r.Error ?? "");
        }).WithName("EditarGastoAdmin");

        // 5. Autorizar un gasto pendiente.
        api.MapPost("/{id:long}/autorizar", async (
            long id,
            ClaimsPrincipal user,
            ServicioGastosAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Autorizar Gasto (Admin)";

            var idUsuario = ObtenerIdUsuario(user);

            log.Info(proceso,
                $"Autorizando gasto #{id} (usuario #{idUsuario?.ToString() ?? "desconocido"}).");

            var r = await s.AutorizarAsync(id, idUsuario ?? 0, ct);

            if (r.Exito)
            {
                var totalPagado = await s.TotalPagadoAsync(id, ct);
                return Respuestas.Ok(MapearDto(r.Datos!, totalPagado), "Gasto autorizado.");
            }

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? "")
                : Respuestas.SolicitudInvalida("No se pudo autorizar el gasto.", r.Error ?? "");
        }).WithName("AutorizarGastoAdmin");

        // 6. Cancelar un gasto sin pagos registrados.
        api.MapPost("/{id:long}/cancelar", async (
            long id,
            CancelarGastoRequest req,
            ClaimsPrincipal user,
            ServicioGastosAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Cancelar Gasto (Admin)";

            var idUsuario = ObtenerIdUsuario(user);

            log.Info(proceso,
                $"Cancelando gasto #{id} (usuario #{idUsuario?.ToString() ?? "desconocido"}).",
                datos: new { req.Motivo });

            var r = await s.CancelarAsync(id, idUsuario ?? 0, req.Motivo, ct);

            if (r.Exito)
            {
                var totalPagado = await s.TotalPagadoAsync(id, ct);
                return Respuestas.Ok(MapearDto(r.Datos!, totalPagado), "Gasto cancelado.");
            }

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? "")
                : Respuestas.SolicitudInvalida("No se pudo cancelar el gasto.", r.Error ?? "");
        }).WithName("CancelarGastoAdmin");
    }
}
