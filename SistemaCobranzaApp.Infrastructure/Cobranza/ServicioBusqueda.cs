using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Búsquedas del módulo de cobro: venta por id o código e historial de pagos de una venta.
/// Optimizadas bajo el nuevo estándar financiero unificado de cuotas y abonos en cascada.
/// </summary>
public class ServicioBusqueda
{
    private readonly CobranzaDbContext _db;

    public ServicioBusqueda(CobranzaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resuelve una venta por texto: IdVenta (dígitos) o CodigoVenta (Vendedor-Timestamp-Hash).
    /// Devuelve null si el texto es inválido o la venta no existe.
    /// </summary>
    public async Task<VentaDetalleDto?> BuscarVentaAsync(string texto, CancellationToken ct = default)
    {
        var tipo = BusquedaHelper.ClasificarBusquedaVenta(texto);

        IQueryable<long> idQuery;
        switch (tipo)
        {
            case BusquedaHelper.TipoBusquedaVenta.PorCodigo:
                var codigo = texto.Trim().ToUpper(); // El código unificado puede contener guiones, se busca directo
                idQuery = _db.Ventas.AsNoTracking()
                    .Where(v => v.CodigoVenta != null && v.CodigoVenta.ToUpper() == codigo)
                    .Select(v => v.IdVenta);
                break;

            case BusquedaHelper.TipoBusquedaVenta.PorId:
                if (!BusquedaHelper.TryParseIdVenta(texto, out var id))
                    return null;
                idQuery = _db.Ventas.AsNoTracking().Where(v => v.IdVenta == id).Select(v => v.IdVenta);
                break;

            default:
                return null;
        }

        var q = from v in _db.Ventas.AsNoTracking()
                where idQuery.Contains(v.IdVenta)
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                from ev in evj.DefaultIfEmpty()
                select new { v, c, r, ev };

        var fila = await q.FirstOrDefaultAsync(ct);
        if (fila is null)
            return null;

        // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' para calcular correctamente el saldo vivo
        var saldo = CalculoCobranza.CalcularSaldoAnterior(
            fila.v.TotalFinal, fila.v.Enganche, fila.v.TotalAbonado);

        // Agrega dirección, teléfono y geolocalización al DTO de ventas
        return new VentaDetalleDto(
            fila.v.IdVenta, 
            fila.v.IdCliente,
            BusquedaHelper.NombreCompleto(fila.c.Nombre, fila.c.ApPaterno, fila.c.ApMaterno),
            fila.r?.Descripcion, 
            fila.r?.IdZona, 
            fila.v.CodigoVenta,
            fila.v.TotalFinal, 
            fila.v.Enganche, 
            fila.v.TotalAbonado, // 🎯 CORREGIDO: Alineado a tu columna real
            saldo,
            fila.v.MontoCuotaFija, // 🎯 CORREGIDO: Cambiado de PagoRequerido a MontoCuotaFija (tu cuota exigida)
            fila.v.IdEstatusVenta, 
            fila.ev?.Descripcion,
            fila.v.FechaCompra, 
            fila.v.FechaUltimoPago,
            fila.c.TelefonoPrincipal, 
            fila.c.Direccion, 
            fila.c.Latitud, 
            fila.c.Longitud);
    }

    /// <summary>
    /// Historial de pagos de una venta (más reciente primero).
    /// </summary>
    public async Task<List<PagoHistoricoDto>> HistorialPagosAsync(long idVenta, CancellationToken ct = default)
    {
        var q = from p in _db.Pagos.AsNoTracking()
                where p.IdVenta == idVenta
                join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                from cob in cobj.DefaultIfEmpty()
                join mov in _db.Movimientos.AsNoTracking() on p.IdMovimiento equals mov.IdMovimiento into movj // 🎯 CORREGIDO: Alineado a CatalogoMovimientos
                from mov in movj.DefaultIfEmpty()
                orderby p.IdPago descending
                select new { p, cob, mov };

        var filas = await q.ToListAsync(ct);

        // Satisface los parámetros de auditoría GPS opcional en el historial
        return filas.Select(x => new PagoHistoricoDto(
            x.p.IdPago, 
            x.p.FolioPago, 
            x.p.ConsecutivoPago ?? 0, 
            x.p.MontoAbona,
            x.p.SaldoAnterior, 
            x.p.SaldoActual, 
            x.p.IdCobrador,
            x.cob != null ? x.cob.Nombre : null,
            x.p.IdMovimiento ?? 0,
            x.mov != null ? x.mov.Descripcion : null,
            x.p.Observaciones, 
            x.p.FechaPago,
            null, // Espacio para latitud de auditoría si la implementas en la entidad Pago
            null  // Espacio para longitud de auditoría si la implementas en la entidad Pago
        )).ToList();
    }
}
