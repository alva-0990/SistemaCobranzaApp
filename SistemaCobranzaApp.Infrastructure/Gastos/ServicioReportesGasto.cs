
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

/// <summary>
/// Reportes de solo lectura del módulo de Gastos.
///
/// Regla transversal para evitar duplicar importes: el "total pagado" de
/// cada gasto se obtiene de una agregación por gasto sobre GastosPagos
/// (GroupBy + Sum) y se asocia por un left-join contra esos totales. Nunca
/// se hace un join fila-a-fila entre Gastos y GastosPagos, que multiplicaría
/// el Importe del gasto por su número de pagos.
/// </summary>
public class ServicioReportesGasto
{
    private const string Proceso = "ServicioReportesGasto";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioReportesGasto(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // Proyección interna: gasto + total pagado (sin duplicar importes).
    private sealed record FilaInterna(Gasto Gasto, decimal TotalPagado);

    private async Task<List<FilaInterna>> ObtenerFilasAsync(
        ReporteGastosFiltro f, CancellationToken ct)
    {
        // Totales de pago agregados por gasto.
        var pagosPorGasto = _db.GastosPagos
            .AsNoTracking()
            .GroupBy(p => p.IdGasto)
            .Select(g => new { IdGasto = g.Key, Total = g.Sum(x => x.Importe) });

        var query =
            from g in _db.Gastos.AsNoTracking()
                .Include(x => x.Categoria)
                .Include(x => x.Zona)
                .Include(x => x.Cobrador)
                .Include(x => x.Vendedor)
            join p in pagosPorGasto on g.IdGasto equals p.IdGasto into pj
            from p in pj.DefaultIfEmpty()
            select new { g, Pagado = (decimal?)(p == null ? 0m : p.Total) };

        if (f.Desde is { } d0) { var d = d0.ToUniversalTime(); query = query.Where(x => x.g.FechaGasto >= d); }
        if (f.Hasta is { } h0) { var h = h0.ToUniversalTime(); query = query.Where(x => x.g.FechaGasto <= h); }
        if (f.IdCategoria is { } cat) query = query.Where(x => x.g.IdCategoriaGasto == cat);
        if (!string.IsNullOrWhiteSpace(f.Estatus))
        {
            var est = f.Estatus.Trim().ToUpperInvariant();
            query = query.Where(x => x.g.Estatus == est);
        }
        if (f.IdZona is { } z) query = query.Where(x => x.g.IdZona == z);
        if (f.IdCobrador is { } cob) query = query.Where(x => x.g.IdCobrador == cob);
        if (f.IdVendedor is { } ven) query = query.Where(x => x.g.IdVendedor == ven);
        if (f.SoloCentrales)
            query = query.Where(x =>
                x.g.IdZona == null && x.g.IdCobrador == null && x.g.IdVendedor == null);

        var filas = await query
            .OrderByDescending(x => x.g.FechaGasto)
            .ThenByDescending(x => x.g.IdGasto)
            .ToListAsync(ct);

        return filas
            .Select(x => new FilaInterna(x.g, x.Pagado ?? 0m))
            .ToList();
    }

    // ------------------------- Detalle de gastos -------------------------

    public async Task<ReporteGastosResponse> DetalleAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        try
        {
            var internas = await ObtenerFilasAsync(f, ct);

            var filas = internas.Select(x => new ReporteGastoFila(
                    x.Gasto.IdGasto,
                    x.Gasto.FolioGasto,
                    x.Gasto.FechaGasto,
                    x.Gasto.Categoria?.Nombre ?? string.Empty,
                    x.Gasto.Concepto,
                    x.Gasto.Proveedor,
                    x.Gasto.Importe,
                    x.TotalPagado,
                    x.Gasto.Importe - x.TotalPagado,
                    x.Gasto.Estatus,
                    x.Gasto.Zona?.Descripcion,
                    x.Gasto.Cobrador?.Nombre,
                    x.Gasto.Vendedor?.Nombre))
                .ToList();

            return new ReporteGastosResponse(
                filas,
                filas.Count,
                filas.Sum(x => x.Importe),
                filas.Sum(x => x.TotalPagado),
                filas.Sum(x => x.SaldoPendiente));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            throw;
        }
    }

    // ------------------------- Agrupaciones -------------------------

    private static ReporteGastosAgrupadoResponse Agrupar(
        IEnumerable<FilaInterna> filas,
        Func<FilaInterna, (string Clave, string Etiqueta)> selector)
    {
        var grupos = filas
            .GroupBy(selector)
            .Select(g => new ReporteGastosAgrupadoFila(
                g.Key.Clave,
                g.Key.Etiqueta,
                g.Count(),
                g.Sum(x => x.Gasto.Importe),
                g.Sum(x => x.TotalPagado),
                g.Sum(x => x.Gasto.Importe - x.TotalPagado)))
            .OrderByDescending(x => x.Importe)
            .ToList();

        return new ReporteGastosAgrupadoResponse(
            grupos,
            grupos.Count,
            grupos.Sum(x => x.Importe),
            grupos.Sum(x => x.TotalPagado),
            grupos.Sum(x => x.SaldoPendiente));
    }

    public async Task<ReporteGastosAgrupadoResponse> PorCategoriaAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        var internas = await ObtenerFilasAsync(f, ct);
        return Agrupar(internas, x => (
            x.Gasto.IdCategoriaGasto.ToString(),
            x.Gasto.Categoria?.Nombre ?? "(sin categoría)"));
    }

    public async Task<ReporteGastosAgrupadoResponse> PorZonaAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        var internas = await ObtenerFilasAsync(f, ct);
        return Agrupar(internas, x => (
            x.Gasto.IdZona?.ToString() ?? "0",
            x.Gasto.Zona?.Descripcion ?? "(central / sin zona)"));
    }

    public async Task<ReporteGastosAgrupadoResponse> PorCobradorAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        var internas = await ObtenerFilasAsync(f, ct);
        return Agrupar(internas, x => (
            x.Gasto.IdCobrador?.ToString() ?? "0",
            x.Gasto.Cobrador?.Nombre ?? "(sin cobrador)"));
    }

    public async Task<ReporteGastosAgrupadoResponse> PorVendedorAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        var internas = await ObtenerFilasAsync(f, ct);
        return Agrupar(internas, x => (
            x.Gasto.IdVendedor?.ToString() ?? "0",
            x.Gasto.Vendedor?.Nombre ?? "(sin vendedor)"));
    }

    public async Task<ReporteGastosAgrupadoResponse> PorEstatusAsync(
        ReporteGastosFiltro f, CancellationToken ct = default)
    {
        var internas = await ObtenerFilasAsync(f, ct);
        return Agrupar(internas, x => (x.Gasto.Estatus, x.Gasto.Estatus));
    }

    // ------------------------- Estado de cuenta -------------------------

    /// <summary>
    /// Estado de cuenta de una cuenta de gasto: saldo inicial, entradas,
    /// salidas y saldo final, con el detalle cronológico de movimientos y
    /// el saldo acumulado por fila. Las entradas por depósito/reposición y
    /// las salidas por pago de gasto ya están materializadas una sola vez
    /// en MovimientosCuentaGasto, por lo que no se duplican importes.
    /// </summary>
    public async Task<ResultadoOperacion<ReporteEstadoCuentaGastoResponse>> EstadoCuentaAsync(
        ReporteEstadoCuentaGastoFiltro f, CancellationToken ct = default)
    {
        try
        {
            var fDesde = f.Desde?.ToUniversalTime();
            var fHasta = f.Hasta?.ToUniversalTime();

            var cuenta = await _db.CuentasGasto
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == f.IdCuentaGasto, ct);

            if (cuenta is null)
                return ResultadoOperacion<ReporteEstadoCuentaGastoResponse>
                    .NoExiste($"No se encontró la cuenta de gasto #{f.IdCuentaGasto}.");

            // Entradas/salidas ANTERIORES al rango: forman el saldo de apertura.
            var baseQuery = _db.MovimientosCuentaGasto
                .AsNoTracking()
                .Where(x => x.IdCuentaGasto == f.IdCuentaGasto);

            decimal saldoApertura = cuenta.SaldoInicial;

            if (fDesde is { } desde)
            {
                var entradasPrev = await baseQuery
                    .Where(x => x.FechaMovimiento < desde && x.TipoMovimiento == "ENTRADA")
                    .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;

                var salidasPrev = await baseQuery
                    .Where(x => x.FechaMovimiento < desde && x.TipoMovimiento == "SALIDA")
                    .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;

                saldoApertura += entradasPrev - salidasPrev;
            }

            var query = baseQuery;
            if (fDesde is { } d) query = query.Where(x => x.FechaMovimiento >= d);
            if (fHasta is { } h) query = query.Where(x => x.FechaMovimiento <= h);

            var movimientos = await query
                .OrderBy(x => x.FechaMovimiento)
                .ThenBy(x => x.IdMovimientoCuenta)
                .ToListAsync(ct);

            var filas = new List<EstadoCuentaGastoMovimientoFila>(movimientos.Count);
            decimal saldo = saldoApertura;
            decimal totalEntradas = 0m, totalSalidas = 0m;

            foreach (var m in movimientos)
            {
                var entrada = m.TipoMovimiento == "ENTRADA" ? m.Importe : 0m;
                var salida = m.TipoMovimiento == "SALIDA" ? m.Importe : 0m;

                saldo += entrada - salida;
                totalEntradas += entrada;
                totalSalidas += salida;

                filas.Add(new EstadoCuentaGastoMovimientoFila(
                    m.IdMovimientoCuenta,
                    m.FechaMovimiento,
                    m.TipoMovimiento,
                    m.Concepto,
                    m.Referencia,
                    entrada,
                    salida,
                    saldo));
            }

            var respuesta = new ReporteEstadoCuentaGastoResponse(
                cuenta.IdCuentaGasto,
                cuenta.Nombre,
                cuenta.TipoCuenta,
                cuenta.Moneda,
                saldoApertura,
                totalEntradas,
                totalSalidas,
                saldo,
                filas);

            return ResultadoOperacion<ReporteEstadoCuentaGastoResponse>.Ok(respuesta);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { f.IdCuentaGasto });
            return ResultadoOperacion<ReporteEstadoCuentaGastoResponse>
                .Fallo("No fue posible generar el estado de cuenta.");
        }
    }
}
