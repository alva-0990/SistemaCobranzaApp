using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Consulta de cartera del cobrador: ventas con saldo pendiente,
/// filtrables por cobrador (vía pagos históricos), zona o ruta bajo el esquema de cuotas fijas.
/// </summary>
public class ServicioCartera
{
    private readonly CobranzaDbContext _db;

    public ServicioCartera(CobranzaDbContext db)
    {
        _db = db;
    }

    public async Task<CarteraResponse> ConsultarAsync(CarteraFiltro filtro, CancellationToken ct = default)
    {
        // Base: ventas no canceladas, uniendo cliente/ruta/estatus.
        var q = from v in _db.Ventas.AsNoTracking()
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                from ev in evj.DefaultIfEmpty()
                where v.IdEstatusVenta != EstatusVentaIds.Cancelada
                select new { v, c, r, ev };

        if (filtro.IdZona is { } zona)
            q = q.Where(x => x.r != null && x.r.IdZona == zona);

        if (filtro.IdRuta is { } ruta)
            q = q.Where(x => x.c.IdRuta == ruta);

        // Filtro por cobrador: ventas que ese cobrador ha cobrado alguna vez.
        if (filtro.IdCobrador is { } cobrador)
        {
            var ventasDelCobrador = _db.Pagos.AsNoTracking()
                .Where(p => p.IdCobrador == cobrador)
                .Select(p => p.IdVenta);
            q = q.Where(x => ventasDelCobrador.Contains(x.v.IdVenta));
        }

        var filas = await q.ToListAsync(ct);

        var cuentas = filas.Select(x =>
        {
            // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' alineado a tu tabla real de PostgreSQL
            var saldo = Domain.Cobranza.CalculoCobranza.CalcularSaldoAnterior(
                x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonado);

            var nombre = string.Join(" ", new[] { x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno }
                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

            return new CarteraCuentaDto(
                x.v.IdVenta,
                x.v.IdCliente,
                nombre,
                x.r?.Descripcion,
                x.r?.IdZona,
                x.v.CodigoVenta,
                x.v.TotalFinal,
                saldo,
                x.v.MontoCuotaFija, // 🎯 CORREGIDO: Cambiado de PagoRequerido a tu columna unificada MontoCuotaFija
                x.v.IdEstatusVenta,
                x.ev?.Descripcion,
                x.v.FechaUltimoPago);
        });

        if (filtro.SoloConSaldo)
            cuentas = cuentas.Where(c => c.SaldoPendiente > 0);

        var lista = cuentas
            .OrderByDescending(c => c.SaldoPendiente)
            .ToList();

        return new CarteraResponse(lista, lista.Count, lista.Sum(c => c.SaldoPendiente));
    }
}
