using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Búsquedas del módulo de cobro: clientes por nombre/apellidos, venta por
/// id o código, e historial de pagos de una venta.
/// Portado de VistaPagoAgregar (app WPF).
/// </summary>
public class ServicioBusqueda
{
    private readonly CobranzaDbContext _db;

    public ServicioBusqueda(CobranzaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Busca clientes activos por nombre/apPaterno/apMaterno (LIKE, ci).
    /// Replica EjecutarBusquedaCliente (sin LIMIT: puede haber homónimos).
    /// </summary>
    public async Task<List<ClienteDto>> BuscarClientesAsync(ClienteBusquedaFiltro f, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(f.Nombre)
            && string.IsNullOrWhiteSpace(f.ApPaterno)
            && string.IsNullOrWhiteSpace(f.ApMaterno))
            return new List<ClienteDto>();

        var nombre = BusquedaHelper.ParaLike(f.Nombre);
        var paterno = BusquedaHelper.ParaLike(f.ApPaterno);
        var materno = BusquedaHelper.ParaLike(f.ApMaterno);

        var q = from c in _db.Clientes.AsNoTracking()
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                where c.Estatus == Domain.Enums.Estatus.Activo
                      && EF.Functions.ILike(c.Nombre, nombre)
                      && EF.Functions.ILike(c.ApPaterno ?? "", paterno)
                      && EF.Functions.ILike(c.ApMaterno ?? "", materno)
                orderby c.Nombre, c.ApPaterno, c.ApMaterno, c.IdCliente
                select new { c, r };

        var filas = await q.ToListAsync(ct);

        return filas.Select(x => new ClienteDto(
            x.c.IdCliente, x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno,
            BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
            x.c.IdRuta, x.r?.Descripcion, x.r?.IdZona,
            x.c.IdVendedor, x.c.IdClasificacion, x.c.Observaciones)).ToList();
    }

    /// <summary>
    /// Resuelve una venta por texto: IdVenta (dígitos) o CodigoVenta (V#######).
    /// Devuelve null si el texto es inválido o la venta no existe.
    /// Replica EjecutarBusquedaVenta.
    /// </summary>
    public async Task<VentaDetalleDto?> BuscarVentaAsync(string texto, CancellationToken ct = default)
    {
        var tipo = BusquedaHelper.ClasificarBusquedaVenta(texto);

        IQueryable<long> idQuery;
        switch (tipo)
        {
            case BusquedaHelper.TipoBusquedaVenta.PorCodigo:
                var codigo = BusquedaHelper.NormalizarCodigoVenta(texto);
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

        var saldo = CalculoCobranza.CalcularSaldoAnterior(
            fila.v.TotalFinal, fila.v.Enganche, fila.v.TotalAbonos);

        return new VentaDetalleDto(
            fila.v.IdVenta, fila.v.IdCliente,
            BusquedaHelper.NombreCompleto(fila.c.Nombre, fila.c.ApPaterno, fila.c.ApMaterno),
            fila.r?.Descripcion, fila.r?.IdZona, fila.v.CodigoVenta,
            fila.v.TotalFinal, fila.v.Enganche, fila.v.TotalAbonos, saldo,
            fila.v.PagoRequerido, fila.v.IdEstatusVenta, fila.ev?.Descripcion,
            fila.v.FechaCompra, fila.v.FechaUltimoPago);
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
                join mov in _db.Movimientos.AsNoTracking() on p.IdMovimiento equals mov.IdMovimiento into movj
                from mov in movj.DefaultIfEmpty()
                orderby p.IdPago descending
                select new PagoHistoricoDto(
                    p.IdPago, p.FolioPago, p.ConsecutivoPago, p.MontoAbona,
                    p.SaldoAnterior, p.SaldoActual, p.IdCobrador,
                    cob != null ? cob.Nombre : null,
                    mov != null ? mov.Descripcion : null,
                    p.Observaciones, p.FechaPago);

        return await q.ToListAsync(ct);
    }
}
