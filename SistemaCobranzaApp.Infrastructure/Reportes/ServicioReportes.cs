using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Reportes;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Reportes;

/// <summary>
/// Reportes de solo lectura, portados de las vistas VistaReporte* (app WPF).
/// </summary>
public class ServicioReportes
{
    private readonly CobranzaDbContext _db;

    public ServicioReportes(CobranzaDbContext db) => _db = db;

    // ------------------------- Cartera -------------------------

    public async Task<ReporteCarteraResponse> CarteraAsync(ReporteCarteraFiltro f, CancellationToken ct = default)
    {
        var q = from v in _db.Ventas.AsNoTracking()
                where v.IdEstatusVenta != EstatusVentaIds.Cancelada
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                from ev in evj.DefaultIfEmpty()
                select new { v, c, r, z, ev };

        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.IdRuta is { } ruta) q = q.Where(x => x.c.IdRuta == ruta);
        if (f.IdEstatusVenta is { } est) q = q.Where(x => x.v.IdEstatusVenta == est);

        var filas = (await q.ToListAsync(ct)).Select(x =>
        {
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos);
            var nombre = BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno);
            return new ReporteCarteraFila(
                x.v.IdVenta, x.v.CodigoVenta, x.v.IdCliente, nombre,
                x.r?.Descripcion, x.z?.Descripcion, x.v.TotalFinal, x.v.Enganche,
                x.v.TotalAbonos, saldo, x.ev?.Descripcion, x.v.FechaCompra, x.v.FechaUltimoPago);
        })
        .Where(x => x.Saldo > 0)
        .OrderByDescending(x => x.Saldo)
        .ToList();

        return new ReporteCarteraResponse(filas, filas.Count, filas.Sum(x => x.Saldo));
    }

    // ------------------------- Pagos -------------------------

    public async Task<ReportePagosResponse> PagosAsync(ReportePagosFiltro f, CancellationToken ct = default)
    {
        var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);

        var q = from p in _db.Pagos.AsNoTracking()
                where p.FechaPago >= desde && p.FechaPago <= hasta
                join c in _db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
                join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                from cob in cobj.DefaultIfEmpty()
                select new { p, c, cob };

        if (f.IdCobrador is { } idc) q = q.Where(x => x.p.IdCobrador == idc);

        var filas = (await q.OrderByDescending(x => x.p.FechaPago).ToListAsync(ct)).Select(x =>
            new ReportePagoFila(
                x.p.IdPago, x.p.FolioPago, x.p.IdVenta,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.cob?.Nombre, x.p.MontoAbona, x.p.SaldoActual, x.p.FechaPago)).ToList();

        return new ReportePagosResponse(filas, filas.Count, filas.Sum(x => x.MontoAbona));
    }

    // ------------------------- Ventas -------------------------

    public async Task<ReporteVentasResponse> VentasAsync(ReporteVentasFiltro f, CancellationToken ct = default)
    {
        var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);

        var q = from v in _db.Ventas.AsNoTracking()
                where v.FechaCompra >= desde && v.FechaCompra <= hasta
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join ven in _db.Vendedores.AsNoTracking() on v.IdVendedor equals ven.IdVendedor into venj
                from ven in venj.DefaultIfEmpty()
                join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                from ev in evj.DefaultIfEmpty()
                select new { v, c, ven, ev };

        if (f.IdVendedor is { } idv) q = q.Where(x => x.v.IdVendedor == idv);

        var filas = (await q.OrderByDescending(x => x.v.FechaCompra).ToListAsync(ct)).Select(x =>
        {
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos);
            return new ReporteVentaFila(
                x.v.IdVenta, x.v.CodigoVenta,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.ven?.Nombre, x.v.TotalFinal, x.v.Enganche, saldo, x.ev?.Descripcion, x.v.FechaCompra);
        }).ToList();

        return new ReporteVentasResponse(filas, filas.Count, filas.Sum(x => x.TotalFinal));
    }

    // ------------------------- Recuperación -------------------------

    public async Task<ReporteRecuperacionResponse> RecuperacionAsync(ReporteRecuperacionFiltro f, CancellationToken ct = default)
    {
        var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);

        var pagos = await _db.Pagos.AsNoTracking()
            .Where(p => p.FechaPago >= desde && p.FechaPago <= hasta)
            .GroupBy(p => p.IdCobrador)
            .Select(g => new { IdCobrador = g.Key, Numero = g.Count(), Monto = g.Sum(x => x.MontoAbona) })
            .ToListAsync(ct);

        var cobradores = await _db.Cobradores.AsNoTracking()
            .ToDictionaryAsync(c => c.IdCobrador, c => c.Nombre, ct);

        var filas = pagos.Select(g => new ReporteRecuperacionFila(
                g.IdCobrador,
                g.IdCobrador is { } id && cobradores.TryGetValue(id, out var n) ? n : "(sin cobrador)",
                g.Numero, g.Monto))
            .OrderByDescending(x => x.MontoCobrado)
            .ToList();

        return new ReporteRecuperacionResponse(filas, filas.Sum(x => x.MontoCobrado));
    }

    // ------------------------- Clientes -------------------------

    public async Task<ReporteClientesResponse> ClientesAsync(ReporteClientesFiltro f, CancellationToken ct = default)
    {
        var q = from c in _db.Clientes.AsNoTracking()
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
                from cl in clj.DefaultIfEmpty()
                select new { c, r, z, cl };

        if (!f.incluirInactivos) q = q.Where(x => x.c.Estatus == Estatus.Activo);
        if (f.IdRuta is { } ruta) q = q.Where(x => x.c.IdRuta == ruta);
        if (f.IdClasificacion is { } cla) q = q.Where(x => x.c.IdClasificacion == cla);

        var filas = (await q.OrderBy(x => x.c.Nombre).ToListAsync(ct)).Select(x =>
            new ReporteClienteFila(
                x.c.IdCliente,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.r?.Descripcion, x.z?.Descripcion, x.cl?.Descripcion,
                x.c.Estatus == Estatus.Activo, x.c.FechaAlta)).ToList();

        return new ReporteClientesResponse(filas, filas.Count);
    }
}
