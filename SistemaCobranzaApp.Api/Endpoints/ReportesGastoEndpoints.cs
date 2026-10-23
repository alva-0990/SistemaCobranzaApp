using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints de reportes del módulo de Gastos (solo lectura).
/// Todas las operaciones requieren autenticación.
///
/// Los reportes evitan duplicar importes: el total pagado se agrega por
/// gasto antes de relacionarlo, y el estado de cuenta usa los movimientos
/// ya materializados una sola vez.
/// </summary>
public class ReportesGastoEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var api = app
            .MapGroup("/api/admin/reportes/gastos")
            .RequireAuthorization();

        static ReporteGastosFiltro ConstruirFiltro(
            DateTimeOffset? desde,
            DateTimeOffset? hasta,
            long? idCategoria,
            string? estatus,
            long? idZona,
            long? idCobrador,
            long? idVendedor,
            bool? soloCentrales) =>
            new(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales ?? false);

        // ------------------------------------------------------------
        // DETALLE: gastos por rango de fechas, categoría, estado, zona,
        // cobrador, vendedor o centrales. Incluye totales.
        // GET /api/admin/reportes/gastos
        // ------------------------------------------------------------
        api.MapGet("/", async (
            DateTimeOffset? desde,
            DateTimeOffset? hasta,
            long? idCategoria,
            string? estatus,
            long? idZona,
            long? idCobrador,
            long? idVendedor,
            bool? soloCentrales,
            ServicioReportesGasto s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Reporte de Gastos (Detalle)";
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus,
                    idZona, idCobrador, idVendedor, soloCentrales);

                var r = await s.DetalleAsync(f, ct);
                return Respuestas.Ok(r, "Reporte de gastos generado.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte de gastos.");
            }
        }).WithName("ReporteGastosDetalle");

        // ------------------------------------------------------------
        // AGRUPACIONES (totales por dimensión).
        // ------------------------------------------------------------
        api.MapGet("/por-categoria", async (
            DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
            long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales,
            ServicioReportesGasto s, IServicioLog log, CancellationToken ct) =>
        {
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales);
                return Respuestas.Ok(await s.PorCategoriaAsync(f, ct), "Reporte de gastos por categoría generado.");
            }
            catch (Exception ex)
            {
                log.Error("Reporte de Gastos por Categoría", ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte por categoría.");
            }
        }).WithName("ReporteGastosPorCategoria");

        api.MapGet("/por-zona", async (
            DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
            long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales,
            ServicioReportesGasto s, IServicioLog log, CancellationToken ct) =>
        {
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales);
                return Respuestas.Ok(await s.PorZonaAsync(f, ct), "Reporte de gastos por zona generado.");
            }
            catch (Exception ex)
            {
                log.Error("Reporte de Gastos por Zona", ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte por zona.");
            }
        }).WithName("ReporteGastosPorZona");

        api.MapGet("/por-cobrador", async (
            DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
            long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales,
            ServicioReportesGasto s, IServicioLog log, CancellationToken ct) =>
        {
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales);
                return Respuestas.Ok(await s.PorCobradorAsync(f, ct), "Reporte de gastos por cobrador generado.");
            }
            catch (Exception ex)
            {
                log.Error("Reporte de Gastos por Cobrador", ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte por cobrador.");
            }
        }).WithName("ReporteGastosPorCobrador");

        api.MapGet("/por-vendedor", async (
            DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
            long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales,
            ServicioReportesGasto s, IServicioLog log, CancellationToken ct) =>
        {
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales);
                return Respuestas.Ok(await s.PorVendedorAsync(f, ct), "Reporte de gastos por vendedor generado.");
            }
            catch (Exception ex)
            {
                log.Error("Reporte de Gastos por Vendedor", ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte por vendedor.");
            }
        }).WithName("ReporteGastosPorVendedor");

        api.MapGet("/por-estatus", async (
            DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
            long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales,
            ServicioReportesGasto s, IServicioLog log, CancellationToken ct) =>
        {
            try
            {
                var f = ConstruirFiltro(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales);
                return Respuestas.Ok(await s.PorEstatusAsync(f, ct), "Reporte de gastos por estatus generado.");
            }
            catch (Exception ex)
            {
                log.Error("Reporte de Gastos por Estatus", ex);
                return Respuestas.ErrorInterno("No fue posible generar el reporte por estatus.");
            }
        }).WithName("ReporteGastosPorEstatus");

        // ------------------------------------------------------------
        // ESTADO DE CUENTA de una cuenta de gastos.
        // GET /api/admin/reportes/gastos/cuentas/{idCuentaGasto}/estado-cuenta
        // ------------------------------------------------------------
        api.MapGet("/cuentas/{idCuentaGasto:long}/estado-cuenta", async (
            long idCuentaGasto,
            DateTimeOffset? desde,
            DateTimeOffset? hasta,
            ServicioReportesGasto s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Reporte Estado de Cuenta de Gasto";

            if (idCuentaGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo generar el estado de cuenta.",
                    "El identificador de la cuenta debe ser mayor que cero.");

            if (desde.HasValue && hasta.HasValue && desde.Value > hasta.Value)
                return Respuestas.SolicitudInvalida(
                    "El rango de fechas no es válido.",
                    "La fecha inicial no puede ser posterior a la fecha final.");

            try
            {
                var r = await s.EstadoCuentaAsync(
                    new ReporteEstadoCuentaGastoFiltro(idCuentaGasto, desde, hasta), ct);

                if (r.Exito)
                    return Respuestas.Ok(r.Datos!, "Estado de cuenta generado.");

                return r.NoEncontrado
                    ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró la cuenta de gasto #{idCuentaGasto}.")
                    : Respuestas.SolicitudInvalida("No se pudo generar el estado de cuenta.", r.Error ?? "");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex, datos: new { idCuentaGasto });
                return Respuestas.ErrorInterno("No fue posible generar el estado de cuenta.");
            }
        }).WithName("ReporteEstadoCuentaGasto");
    }
}
