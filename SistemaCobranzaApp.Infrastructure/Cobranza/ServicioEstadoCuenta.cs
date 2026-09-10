using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Estado de cuenta consolidado de un cliente: datos del cliente, sus ventas
/// (no canceladas) con saldo, historial de pagos y totales.
/// </summary>
public class ServicioEstadoCuenta
{
    private readonly CobranzaDbContext _db;

    public ServicioEstadoCuenta(CobranzaDbContext db) => _db = db;

    public async Task<EstadoCuentaResponse?> ObtenerAsync(long idCliente, CancellationToken ct = default)
    {
        var clienteFila = await (from c in _db.Clientes.AsNoTracking()
                                 where c.IdCliente == idCliente
                                 join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                                 from r in rj.DefaultIfEmpty()
                                 join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                                 from z in zj.DefaultIfEmpty()
                                 select new { c, r, z }).FirstOrDefaultAsync(ct);

        if (clienteFila is null) return null;

        // Ventas (no canceladas) del cliente.
        var ventasFilas = await (from v in _db.Ventas.AsNoTracking()
                                 where v.IdCliente == idCliente && v.IdEstatusVenta != EstatusVentaIds.Cancelada
                                 join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                                 from ev in evj.DefaultIfEmpty()
                                 orderby v.FechaCompra descending
                                 select new { v, ev }).ToListAsync(ct);

        var ventas = ventasFilas.Select(x =>
        {
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos);
            return new EstadoCuentaVentaDto(
                x.v.IdVenta, x.v.CodigoVenta, x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos,
                saldo, x.ev?.Descripcion, x.v.FechaCompra, x.v.FechaUltimoPago);
        }).ToList();

        // Pagos del cliente.
        var pagos = await (from p in _db.Pagos.AsNoTracking()
                           where p.IdCliente == idCliente
                           join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                           from cob in cobj.DefaultIfEmpty()
                           orderby p.FechaPago descending
                           select new EstadoCuentaPagoDto(
                               p.IdPago, p.FolioPago, p.IdVenta, p.MontoAbona, p.SaldoActual,
                               cob != null ? cob.Nombre : null, p.FechaPago)).ToListAsync(ct);

        // Totales (lógica pura).
        var totales = CalculoEstadoCuenta.CalcularTotales(
            ventasFilas.Select(x => new CalculoEstadoCuenta.VentaResumen(
                x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos)));

        var nombre = BusquedaHelper.NombreCompleto(
            clienteFila.c.Nombre, clienteFila.c.ApPaterno, clienteFila.c.ApMaterno);

        return new EstadoCuentaResponse(
            clienteFila.c.IdCliente, nombre, clienteFila.r?.Descripcion, clienteFila.z?.Descripcion,
            totales.TotalVendido, totales.TotalPagado, totales.SaldoTotal, ventas, pagos);
    }
}
