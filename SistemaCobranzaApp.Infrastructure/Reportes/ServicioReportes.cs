using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Reportes;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Reportes;

/// <summary>
/// Reportes de solo lectura, portados de las vistas VistaReporte* (app WPF)
/// y optimizados bajo el nuevo estándar financiero unificado.
/// </summary>
public class ServicioReportes
{
    private readonly CobranzaDbContext _db;

    public ServicioReportes(CobranzaDbContext db) => _db = db;

    private async Task<Dictionary<long, string>> ProductosPorVentaAsync(IQueryable<long> ventas, CancellationToken ct)
    {
        var detalles = await (from d in _db.DetallesVenta.AsNoTracking()
                              join p in _db.Productos.AsNoTracking() on d.IdProducto equals p.IdProducto
                              where ventas.Contains(d.IdVenta)
                              orderby d.IdVenta, d.IdDetalle
                              select new { d.IdVenta, p.Descripcion, d.Cantidad }).ToListAsync(ct);
        return detalles.GroupBy(d => d.IdVenta).ToDictionary(g => g.Key,
            g => string.Join("; ", g.Select(d => $"{d.Descripcion} (x{d.Cantidad})")));
    }

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

        var productos = await ProductosPorVentaAsync(q.Select(x => x.v.IdVenta), ct);
        var filas = (await q.ToListAsync(ct)).Select(x =>
        {
            // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' para reflejar el saldo real autoritativo
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonado);
            var nombre = BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno);
            
            return new ReporteCarteraFila(
                x.v.IdVenta, x.v.CodigoVenta, x.v.IdCliente, nombre,
                x.r?.Descripcion, x.z?.Descripcion, x.v.TotalFinal, x.v.Enganche,
                x.v.TotalAbonado, saldo, x.ev?.Descripcion, x.v.FechaCompra, x.v.FechaUltimoPago)
            { Productos = productos.GetValueOrDefault(x.v.IdVenta, "") };
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
                join v in _db.Ventas.AsNoTracking() on p.IdVenta equals v.IdVenta
                join c in _db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
                join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                from cob in cobj.DefaultIfEmpty()
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join m in _db.Movimientos.AsNoTracking() on p.IdMovimiento equals (long?)m.IdMovimiento into mj
                from m in mj.DefaultIfEmpty()
                select new { p, v, c, cob, r, m };

        if (f.IdCobrador is { } idc) q = q.Where(x => x.p.IdCobrador == idc);
        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.IdRuta is { } ruta) q = q.Where(x => x.c.IdRuta == ruta);
        if (f.IdMovimiento is { } mov) q = q.Where(x => x.p.IdMovimiento == mov);

        var productos = await ProductosPorVentaAsync(q.Select(x => x.p.IdVenta), ct);
        var filas = (await q.OrderByDescending(x => x.p.FechaPago).ToListAsync(ct)).Select(x =>
            new ReportePagoFila(
                x.p.IdPago, x.p.FolioPago, x.p.IdVenta,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.cob?.Nombre, x.p.MontoAbona, x.p.SaldoActual, x.p.FechaPago)
            {
                Productos = productos.GetValueOrDefault(x.p.IdVenta, ""),
                TotalVenta = x.v.TotalFinal,
                IdMovimiento = x.p.IdMovimiento,
                Movimiento = x.m != null ? x.m.Descripcion : null
            }).ToList();

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
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                select new { v, c, ven, ev, r };

        if (f.IdVendedor is { } idv) q = q.Where(x => x.v.IdVendedor == idv);
        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.IdRuta is { } ruta) q = q.Where(x => x.c.IdRuta == ruta);

        var productos = await ProductosPorVentaAsync(q.Select(x => x.v.IdVenta), ct);
        var filas = (await q.OrderByDescending(x => x.v.FechaCompra).ToListAsync(ct)).Select(x =>
        {
            // 🎯 CORREGIDO: Se alineó al uso de 'TotalAbonado' para mantener la consistencia entre reportes
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonado);
            
            return new ReporteVentaFila(
                x.v.IdVenta, x.v.CodigoVenta,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.ven?.Nombre, x.v.TotalFinal, x.v.Enganche, saldo, x.ev?.Descripcion, x.v.FechaCompra)
            { Productos = productos.GetValueOrDefault(x.v.IdVenta, "") };
        }).ToList();

        return new ReporteVentasResponse(filas, filas.Count, filas.Sum(x => x.TotalFinal));
    }

    // ------------------------- Recuperación -------------------------

    public async Task<ReporteRecuperacionResponse> RecuperacionAsync(ReporteRecuperacionFiltro f, CancellationToken ct = default)
    {
        var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);

        // Unimos con el cliente y su ruta para poder acotar por zona/ruta antes de agrupar por cobrador.
        var baseQuery = from p in _db.Pagos.AsNoTracking()
                        where p.FechaPago >= desde && p.FechaPago <= hasta
                        join c in _db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
                        join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                        from r in rj.DefaultIfEmpty()
                        select new { p, c, r };

        if (f.IdZona is { } zona) baseQuery = baseQuery.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.IdRuta is { } ruta) baseQuery = baseQuery.Where(x => x.c.IdRuta == ruta);

        var pagos = await baseQuery
            .GroupBy(x => x.p.IdCobrador)
            .Select(g => new { IdCobrador = g.Key, Numero = g.Count(), Monto = g.Sum(x => x.p.MontoAbona) })
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
        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
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

    // ------------------------- Dashboard administrativo -------------------------

    // ------------------------- Seguimiento (días desde el último pago) -------------------------

    /// <summary>
    /// Reporte de control: por cada venta con saldo pendiente, muestra el cliente, su producto,
    /// los totales (venta, cobrado, pendiente), la fecha de venta, la fecha del último pago y
    /// cuántos días han pasado desde ese último pago (o desde la venta si aún no hay pagos).
    /// Permite filtrar por zona y por rango de fecha de alta del cliente. Ordena por días
    /// transcurridos descendente para priorizar las cuentas más rezagadas.
    /// </summary>
    public async Task<ReporteSeguimientoResponse> SeguimientoAsync(ReporteSeguimientoFiltro f, CancellationToken ct = default)
    {
        var q = from v in _db.Ventas.AsNoTracking()
                where v.IdEstatusVenta != EstatusVentaIds.Cancelada
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
                from cl in clj.DefaultIfEmpty()
                select new { v, c, r, z, cl };

        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.AltaDesde is { } desde) q = q.Where(x => x.c.FechaAlta >= desde);
        if (f.AltaHasta is { } hasta) q = q.Where(x => x.c.FechaAlta <= hasta);

        var productos = await ProductosPorVentaAsync(q.Select(x => x.v.IdVenta), ct);
        var hoy = FechaProyecto.Hoy;

        var filas = (await q.ToListAsync(ct)).Select(x =>
        {
            // Saldo autoritativo: TotalFinal − Enganche − TotalAbonado (piso en 0).
            var pendiente = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonado);
            // Cobrado real = enganche + abonos aplicados.
            var cobrado = x.v.Enganche + x.v.TotalAbonado;
            // Referencia para contar días: último pago si existe; si no, la fecha de la venta.
            var referencia = (x.v.FechaUltimoPago ?? x.v.FechaCompra).EnZonaProyecto().Date;
            var dias = Math.Max(0, (hoy - referencia).Days);

            return new ReporteSeguimientoFila(
                x.c.IdCliente,
                BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
                x.v.IdVenta,
                x.v.CodigoVenta,
                productos.GetValueOrDefault(x.v.IdVenta, ""),
                x.v.TotalFinal,
                cobrado,
                pendiente,
                x.v.FechaCompra,
                x.v.FechaUltimoPago,
                dias,
                x.c.IdClasificacion,
                x.cl?.Descripcion,
                x.c.FechaAlta,
                x.z?.Descripcion,
                x.c.Observaciones);
        })
        .Where(x => x.TotalPendiente > 0)
        .OrderByDescending(x => x.DiasTranscurridos)
        .ToList();

        return new ReporteSeguimientoResponse(filas, filas.Count, filas.Sum(x => x.TotalPendiente));
    }

    /// <summary>
    /// Agrega KPIs, series por zona/día y un resumen por zona para el panel de control (Home).
    /// Reutiliza la convención de zona del proyecto para el corte por día y el saldo autoritativo
    /// de cartera (TotalFinal − Enganche − TotalAbonado).
    /// </summary>
    public async Task<DashboardResponse> DashboardAsync(DashboardFiltro f, CancellationToken ct = default)
    {
        try
        {
            var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);

            // Ventas liquidadas por renovación: su saldo se transfirió a otra venta (la nueva ya
            // absorbió el monto en su TotalFinal). Se identifican por tener un pago de salida de
            // tipo LiquidacionRenovacion (IdMovimiento = 7). Se excluyen de "Ventas totales" para
            // no contar el mismo dinero dos veces.
            var ventasRenovadas = _db.Pagos.AsNoTracking()
                .Where(p => p.IdMovimiento == MovimientoIds.LiquidacionRenovacion)
                .Select(p => p.IdVenta);

            // ---------- Ventas del rango (por zona) ----------
            var ventasQ = from v in _db.Ventas.AsNoTracking()
                          where v.IdEstatusVenta != EstatusVentaIds.Cancelada
                                && !ventasRenovadas.Contains(v.IdVenta)
                                && v.FechaCompra >= desde && v.FechaCompra <= hasta
                          join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                          join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                          from r in rj.DefaultIfEmpty()
                          join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                          from z in zj.DefaultIfEmpty()
                          join ven in _db.Vendedores.AsNoTracking() on v.IdVendedor equals ven.IdVendedor into venj
                          from ven in venj.DefaultIfEmpty()
                          select new { v, r, z, ven };

            if (f.IdZona is { } zonaV) ventasQ = ventasQ.Where(x => x.r != null && x.r.IdZona == zonaV);

            var ventas = await ventasQ.Select(x => new
            {
                IdZona = (long?)(x.r != null ? x.r.IdZona : null),
                Zona = x.z != null ? x.z.Descripcion : null,
                Vendedor = x.ven != null ? x.ven.Nombre : null,
                x.v.TotalFinal
            }).ToListAsync(ct);

            // ---------- Pagos del rango (por zona / día / cobrador) ----------
            // Solo se considera cobranza real: movimientos de tipo ABONO (IdMovimiento = 1).
            // Se excluyen enganches, ajustes, bonificaciones y liquidaciones por renovación,
            // que no representan dinero cobrado al cliente en el periodo.
            var pagosQ = from p in _db.Pagos.AsNoTracking()
                         where p.FechaPago >= desde && p.FechaPago <= hasta
                               && p.IdMovimiento == MovimientoIds.Abono
                         join c in _db.Clientes.AsNoTracking() on p.IdCliente equals c.IdCliente
                         join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                         from r in rj.DefaultIfEmpty()
                         join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                         from z in zj.DefaultIfEmpty()
                         join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                         from cob in cobj.DefaultIfEmpty()
                         select new { p, r, z, cob };

            if (f.IdZona is { } zonaP) pagosQ = pagosQ.Where(x => x.r != null && x.r.IdZona == zonaP);

            var pagos = await pagosQ.Select(x => new
            {
                IdZona = (long?)(x.r != null ? x.r.IdZona : null),
                Zona = x.z != null ? x.z.Descripcion : null,
                Cobrador = x.cob != null ? x.cob.Nombre : null,
                x.p.MontoAbona,
                x.p.FechaPago
            }).ToListAsync(ct);

            // ---------- Cartera vigente (saldo por cobrar, independiente del rango) ----------
            // Se usa la columna autoritativa Venta.SaldoPendiente (mantenida por los triggers y el
            // proceso de renovación), NO un recálculo: las ventas liquidadas por renovación quedan
            // en SaldoPendiente = 0, por lo que se excluyen de forma natural al filtrar > 0.
            var carteraQ = from v in _db.Ventas.AsNoTracking()
                           where v.IdEstatusVenta != EstatusVentaIds.Cancelada
                           join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                           join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                           from r in rj.DefaultIfEmpty()
                           join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                           from z in zj.DefaultIfEmpty()
                           select new { v, r, z };

            if (f.IdZona is { } zonaC) carteraQ = carteraQ.Where(x => x.r != null && x.r.IdZona == zonaC);

            var carteraPorVenta = await carteraQ
                .Select(x => new
                {
                    IdZona = (long?)(x.r != null ? x.r.IdZona : null),
                    Zona = x.z != null ? x.z.Descripcion : null,
                    x.v.SaldoPendiente
                })
                .Where(x => x.SaldoPendiente > 0)
                .ToListAsync(ct);

            var saldos = carteraPorVenta.Select(x => x.SaldoPendiente).ToList();

            var saldoPorCobrar = saldos.Sum();
            var cuentasConSaldo = saldos.Count;

            // ---------- KPIs ----------
            var inicioHoy = FechaProyecto.InicioDiaUtc(FechaProyecto.Hoy);
            var finHoy = FechaProyecto.FinDiaUtc(FechaProyecto.Hoy);
            var pagosHoy = pagos.Where(p => p.FechaPago >= inicioHoy && p.FechaPago <= finHoy).ToList();

            var ventasTotales = ventas.Sum(v => v.TotalFinal);
            var totalCobrado = pagos.Sum(p => p.MontoAbona);

            var kpis = new DashboardKpis(
                VentasTotales: ventasTotales,
                TotalCobrado: totalCobrado,
                SaldoPorCobrar: saldoPorCobrar,
                CobradoHoy: pagosHoy.Sum(p => p.MontoAbona),
                CuentasConSaldo: cuentasConSaldo,
                PagosHoy: pagosHoy.Count);

            // ---------- Series por zona ----------
            const string sinZona = "(sin zona)";

            var ventasPorZona = ventas
                .GroupBy(v => v.Zona ?? sinZona)
                .Select(g => new DashboardSerieZona(g.Key, g.Sum(x => x.TotalFinal)))
                .OrderByDescending(x => x.Monto)
                .ToList();

            var cobranzaPorZona = pagos
                .GroupBy(p => p.Zona ?? sinZona)
                .Select(g => new DashboardSerieZona(g.Key, g.Sum(x => x.MontoAbona)))
                .OrderByDescending(x => x.Monto)
                .ToList();

            // ---------- Cobranza por día (zona del proyecto) ----------
            var cobranzaPorDia = pagos
                .GroupBy(p => p.FechaPago.EnZonaProyecto().Date)
                .Select(g => new DashboardSerieDia(
                    FechaProyecto.DesdeFechaLocal(g.Key),
                    g.Sum(x => x.MontoAbona)))
                .OrderBy(x => x.Dia)
                .ToList();

            // ---------- Resumen por zona ----------
            var ventaPorZonaId = ventas
                .GroupBy(v => v.IdZona)
                .ToDictionary(g => g.Key ?? -1, g => new
                {
                    Zona = g.Select(x => x.Zona).FirstOrDefault(z => z != null) ?? sinZona,
                    Total = g.Sum(x => x.TotalFinal),
                    Vendedor = g.Select(x => x.Vendedor).FirstOrDefault(v => v != null)
                });

            var cobroPorZonaId = pagos
                .GroupBy(p => p.IdZona)
                .ToDictionary(g => g.Key ?? -1, g => new
                {
                    Zona = g.Select(x => x.Zona).FirstOrDefault(z => z != null) ?? sinZona,
                    Total = g.Sum(x => x.MontoAbona),
                    Cobrador = g.Select(x => x.Cobrador).FirstOrDefault(c => c != null)
                });

            // Saldo pendiente real por zona (columna autoritativa Venta.SaldoPendiente).
            var saldoPorZonaId = carteraPorVenta
                .GroupBy(x => x.IdZona)
                .ToDictionary(g => g.Key ?? -1, g => new
                {
                    Zona = g.Select(x => x.Zona).FirstOrDefault(z => z != null) ?? sinZona,
                    Total = g.Sum(x => x.SaldoPendiente)
                });

            var zonasResumen = ventaPorZonaId.Keys
                .Union(cobroPorZonaId.Keys)
                .Union(saldoPorZonaId.Keys);

            var resumen = zonasResumen.Select(id =>
            {
                ventaPorZonaId.TryGetValue(id, out var ve);
                cobroPorZonaId.TryGetValue(id, out var co);
                saldoPorZonaId.TryGetValue(id, out var sa);

                var zona = ve?.Zona ?? co?.Zona ?? sa?.Zona ?? sinZona;
                var totalVendido = ve?.Total ?? 0m;
                var totalCobradoZona = co?.Total ?? 0m;
                var saldoPendiente = sa?.Total ?? 0m;

                return new DashboardResumenFila(
                    Zona: zona,
                    Vendedor: ve?.Vendedor,
                    Cobrador: co?.Cobrador,
                    TotalVendido: totalVendido,
                    TotalCobrado: totalCobradoZona,
                    SaldoPendiente: saldoPendiente,
                    PorcentajeRecuperacion: CalculoReportes.PorcentajeRecuperacion(totalCobradoZona, totalVendido));
            })
            .OrderByDescending(x => x.TotalVendido)
            .ToList();

            return new DashboardResponse(kpis, ventasPorZona, cobranzaPorZona, cobranzaPorDia, resumen);
        }
        catch
        {
            // Ante cualquier fallo, devolvemos una respuesta vacía coherente para no romper el panel.
            return new DashboardResponse(
                new DashboardKpis(0, 0, 0, 0, 0, 0),
                Array.Empty<DashboardSerieZona>(),
                Array.Empty<DashboardSerieZona>(),
                Array.Empty<DashboardSerieDia>(),
                Array.Empty<DashboardResumenFila>());
        }
    }
}
