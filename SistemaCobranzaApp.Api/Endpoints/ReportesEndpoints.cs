using SistemaCobranzaApp.Infrastructure.Reportes;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Api.Endpoints;

public class ReportesEndpoints: IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ===================== Reportes (solo lectura, protegidos) =====================
        app.MapGet("/api/reportes/cartera",
                async (long? idZona, long? idRuta, long? idEstatusVenta, ServicioReportes s, CancellationToken ct) =>
                    Results.Ok(await s.CarteraAsync(new ReporteCarteraFiltro(idZona, idRuta, idEstatusVenta), ct)))
            .RequireAuthorization();

        app.MapGet("/api/reportes/pagos", async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idCobrador,
                    long? idZona, long? idRuta, long? idMovimiento, ServicioReportes s, CancellationToken ct) =>
                Results.Ok(await s.PagosAsync(new ReportePagosFiltro(desde, hasta, idCobrador, idZona, idRuta, idMovimiento), ct)))
            .RequireAuthorization();

        app.MapGet("/api/reportes/ventas", async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idVendedor,
                    long? idZona, long? idRuta, ServicioReportes s, CancellationToken ct) =>
                Results.Ok(await s.VentasAsync(new ReporteVentasFiltro(desde, hasta, idVendedor, idZona, idRuta), ct)))
            .RequireAuthorization();

        app.MapGet("/api/reportes/recuperacion",
                async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idZona, long? idRuta,
                    ServicioReportes s, CancellationToken ct) =>
                    Results.Ok(await s.RecuperacionAsync(new ReporteRecuperacionFiltro(desde, hasta, idZona, idRuta), ct)))
            .RequireAuthorization();

        app.MapGet("/api/reportes/clientes", async (long? idRuta, long? idClasificacion, bool? incluirInactivos,
                long? idZona, ServicioReportes s, CancellationToken ct) =>
            Results.Ok(await s.ClientesAsync(
                new ReporteClientesFiltro(idRuta, idClasificacion, incluirInactivos ?? false, idZona),
                ct))).RequireAuthorization();

        app.MapGet("/api/reportes/dashboard",
                async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idZona,
                    ServicioReportes s, CancellationToken ct) =>
                    Results.Ok(await s.DashboardAsync(new DashboardFiltro(desde, hasta, idZona), ct)))
            .RequireAuthorization();

        app.MapGet("/api/reportes/seguimiento",
                async (long? idZona, DateTimeOffset? altaDesde, DateTimeOffset? altaHasta,
                    ServicioReportes s, CancellationToken ct) =>
                    Results.Ok(await s.SeguimientoAsync(new ReporteSeguimientoFiltro(idZona, altaDesde, altaHasta), ct)))
            .RequireAuthorization();
    }
}