using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Reportes;

/// <summary>
/// Reportes ejecutivos de cobranza. Todos parten de <see cref="FilasSeguimientoAsync"/> (una sola consulta
/// base por reporte) y agrupan por cliente en memoria; ninguno repite joins contra la base.
/// </summary>
public partial class ServicioReportes
{
    private const string SinZona = "(sin zona)";
    private const string SinClasificacion = "(sin clasificación)";

    // Umbrales históricos cuando la venta no tiene frecuencia de pago (mismos que el semáforo de seguimiento).
    private const int DiasAtrasoSinFrecuencia = 7;
    private const int DiasCriticoSinFrecuencia = 15;

    // Deuda consolidada por cliente: se toma la venta más rezagada como representante (días y último pago).
    private sealed record ClienteDeuda(
        long IdCliente, string Cliente, string? Zona, string? Ruta, long? IdClasificacion, string? Clasificacion,
        DateTimeOffset? FechaUltimoPago, int Dias, int DiasPeriodo, decimal Saldo);

    private static List<ClienteDeuda> AgruparPorCliente(IEnumerable<ReporteSeguimientoFila> filas)
        => filas.GroupBy(f => f.IdCliente).Select(g =>
        {
            var peor = g.OrderByDescending(x => x.DiasTranscurridos).First();
            // Ruta: se obtiene de cualquier fila del cliente (misma ruta para todas sus ventas).
            var ruta = g.Select(x => x.Ruta).FirstOrDefault(r => r is not null);
            return new ClienteDeuda(peor.IdCliente, peor.Cliente, peor.Zona, ruta, peor.IdClasificacion,
                peor.Clasificacion, peor.FechaUltimoPago, peor.DiasTranscurridos, peor.DiasPeriodo,
                g.Sum(x => x.TotalPendiente));
        }).ToList();

    private static decimal Porcentaje(decimal parte, decimal total)
        => total <= 0 ? 0m : Math.Round(parte / total * 100m, 2);

    private static bool TieneAtraso(ClienteDeuda c)
        => c.DiasPeriodo > 0 ? c.Dias > c.DiasPeriodo : c.Dias > DiasAtrasoSinFrecuencia;

    private static bool EsCritico(ClienteDeuda c, int graciaAlerta)
        => c.DiasPeriodo > 0 ? c.Dias > c.DiasPeriodo + graciaAlerta : c.Dias > DiasCriticoSinFrecuencia;

    private static string RangoAntiguedad(int dias) => dias switch
    {
        <= 7 => "0-7 días",
        <= 15 => "8-15 días",
        <= 30 => "16-30 días",
        <= 60 => "31-60 días",
        _ => "+60 días"
    };

    private static readonly string[] RangosAntiguedad = ["0-7 días", "8-15 días", "16-30 días", "31-60 días", "+60 días"];

    private async Task<List<ClienteDeuda>> DeudaPorClienteAsync(ReporteDeudaFiltro f, CancellationToken ct)
        => AgruparPorCliente(await FilasSeguimientoAsync(f.IdZona, f.IdRuta, f.IdClasificacion, null, null, ct));

    // ------------------------- Dashboard ejecutivo -------------------------

    public async Task<ReporteDashboardEjecutivoResponse> DashboardEjecutivoAsync(
        ReporteDashboardEjecutivoFiltro f, CancellationToken ct = default)
    {
        var clientes = await DeudaPorClienteAsync(new ReporteDeudaFiltro(f.IdZona), ct);

        // Ventas y cobranza del periodo: se reutiliza el cálculo del dashboard administrativo (misma regla de negocio).
        var resumen = await DashboardAsync(new DashboardFiltro(f.Desde, f.Hasta, f.IdZona), ct);

        var activosQ = from c in _db.Clientes.AsNoTracking()
                       where c.Estatus == Estatus.Activo
                       join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                       from r in rj.DefaultIfEmpty()
                       select new { c, r };
        if (f.IdZona is { } zona) activosQ = activosQ.Where(x => x.r != null && x.r.IdZona == zona);
        var activos = await activosQ.CountAsync(ct);

        var criticos = clientes.Where(c => EsCritico(c, f.DiasGraciaAlerta)).ToList();
        var mayor = clientes.OrderByDescending(c => c.Saldo).FirstOrDefault();

        // Zona de mayor riesgo = la que concentra más saldo crítico (si no hay críticos, más saldo con atraso).
        var baseRiesgo = criticos.Count > 0 ? criticos : clientes.Where(TieneAtraso).ToList();
        var zonaRiesgo = baseRiesgo.GroupBy(c => c.Zona ?? SinZona)
            .Select(g => new { Zona = g.Key, Saldo = g.Sum(x => x.Saldo) })
            .OrderByDescending(x => x.Saldo).FirstOrDefault();

        return new ReporteDashboardEjecutivoResponse(
            CarteraPendiente: clientes.Sum(c => c.Saldo),
            TotalCobrado: resumen.Kpis.TotalCobrado,
            VentasPeriodo: resumen.Kpis.VentasTotales,
            ClientesActivos: activos,
            ClientesConAtraso: clientes.Count(TieneAtraso),
            ClientesCriticos: criticos.Count,
            MayorDeudor: mayor?.Cliente,
            MayorDeudorSaldo: mayor?.Saldo ?? 0m,
            ZonaMayorRiesgo: zonaRiesgo?.Zona,
            ZonaMayorRiesgoSaldo: zonaRiesgo?.Saldo ?? 0m);
    }

    // ------------------------- Antigüedad de saldos -------------------------

    public async Task<ReporteAntiguedadResponse> AntiguedadSaldosAsync(ReporteDeudaFiltro f, CancellationToken ct = default)
    {
        var clientes = await DeudaPorClienteAsync(f, ct);
        var total = clientes.Sum(c => c.Saldo);

        var filas = clientes.OrderByDescending(c => c.Dias).ThenByDescending(c => c.Saldo)
            .Select(c => new ReporteAntiguedadFila(c.IdCliente, c.Cliente, c.Zona, c.Ruta, c.Clasificacion,
                c.FechaUltimoPago, c.Dias, c.Saldo, RangoAntiguedad(c.Dias)))
            .ToList();

        var rangos = RangosAntiguedad.Select(r =>
        {
            var g = clientes.Where(c => RangoAntiguedad(c.Dias) == r).ToList();
            var saldo = g.Sum(c => c.Saldo);
            return new ReporteAntiguedadRangoFila(r, g.Count, saldo, Porcentaje(saldo, total));
        }).ToList();

        return new ReporteAntiguedadResponse(
            filas, rangos, total,
            ClientesVencidos: clientes.Count(c => c.Dias > DiasAtrasoSinFrecuencia),
            SaldoCritico: clientes.Where(c => c.Dias > 30).Sum(c => c.Saldo),
            MaximoAtraso: clientes.Count == 0 ? 0 : clientes.Max(c => c.Dias));
    }

    // ------------------------- Clientes sin pago reciente -------------------------

    public async Task<ReporteSinPagoResponse> ClientesSinPagoAsync(ReporteSinPagoFiltro f, CancellationToken ct = default)
    {
        var clientes = await DeudaPorClienteAsync(new ReporteDeudaFiltro(f.IdZona, f.IdRuta, f.IdClasificacion), ct);
        var filas = clientes.Where(c => c.Dias >= f.DiasSinPago)
            .OrderByDescending(c => c.Dias)
            .Select(c => new ReporteSinPagoFila(c.IdCliente, c.Cliente, c.Zona, c.Ruta, c.Clasificacion,
                c.FechaUltimoPago, c.Dias, c.Saldo))
            .ToList();

        return new ReporteSinPagoResponse(
            filas, filas.Count, filas.Sum(x => x.SaldoPendiente),
            filas.Count == 0 ? 0m : Math.Round((decimal)filas.Average(x => x.DiasSinPago), 1),
            filas.Count == 0 ? 0 : filas.Max(x => x.DiasSinPago));
    }

    // ------------------------- Ranking de deudores -------------------------

    public async Task<ReporteRankingDeudoresResponse> RankingDeudoresAsync(ReporteDeudaFiltro f, CancellationToken ct = default)
    {
        var clientes = (await DeudaPorClienteAsync(f, ct)).OrderByDescending(c => c.Saldo).ToList();
        var total = clientes.Sum(c => c.Saldo);

        var filas = clientes.Select((c, i) => new ReporteRankingDeudoresFila(i + 1, c.IdCliente, c.Cliente, c.Zona,
            c.Ruta, c.Clasificacion, c.FechaUltimoPago, c.Dias, c.Saldo)).ToList();

        var top10 = clientes.Take(10).Sum(c => c.Saldo);
        return new ReporteRankingDeudoresResponse(filas, total, clientes.FirstOrDefault()?.Cliente,
            clientes.FirstOrDefault()?.Saldo ?? 0m, top10, Porcentaje(top10, total));
    }

    // ------------------------- Cartera por clasificación -------------------------

    public async Task<ReporteCarteraClasificacionResponse> CarteraPorClasificacionAsync(
        ReporteDeudaFiltro f, CancellationToken ct = default)
    {
        var clientes = await DeudaPorClienteAsync(f, ct);
        var total = clientes.Sum(c => c.Saldo);

        var filas = clientes.GroupBy(c => c.Clasificacion ?? SinClasificacion)
            .Select(g => new ReporteCarteraClasificacionFila(g.Key, g.Count(), g.Sum(x => x.Saldo), Porcentaje(g.Sum(x => x.Saldo), total)))
            .OrderByDescending(x => x.SaldoPendiente)
            .ToList();

        // Clasificación de riesgo = la que acumula más saldo con más de 30 días sin pago.
        var riesgo = clientes.Where(c => c.Dias > 30).GroupBy(c => c.Clasificacion ?? SinClasificacion)
            .Select(g => new { Clasificacion = g.Key, Saldo = g.Sum(x => x.Saldo) })
            .OrderByDescending(x => x.Saldo).FirstOrDefault();

        return new ReporteCarteraClasificacionResponse(filas, clientes.Count, total,
            filas.FirstOrDefault()?.Clasificacion, riesgo?.Clasificacion);
    }

    // ------------------------- Concentración de cartera -------------------------

    public async Task<ReporteConcentracionResponse> ConcentracionCarteraAsync(
        ReporteDeudaFiltro f, CancellationToken ct = default)
    {
        var clientes = (await DeudaPorClienteAsync(f, ct)).OrderByDescending(c => c.Saldo).ToList();
        var total = clientes.Sum(c => c.Saldo);

        var filas = clientes.Take(20).Select((c, i) => new ReporteConcentracionFila(i + 1, c.IdCliente, c.Cliente,
            c.Saldo, Porcentaje(c.Saldo, total), c.Clasificacion, c.Zona)).ToList();

        var top10 = clientes.Take(10).Sum(c => c.Saldo);
        return new ReporteConcentracionResponse(filas, total,
            clientes.Take(5).Sum(c => c.Saldo), top10, clientes.Take(20).Sum(c => c.Saldo), Porcentaje(top10, total));
    }
}
