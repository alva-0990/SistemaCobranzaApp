using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Estado de cuenta integral del cliente (documento completo): datos del cliente, perfil de riesgo,
/// dashboard financiero, créditos activos con productos/cuotas/pagos, liquidados y cancelados.
/// Reutiliza las mismas reglas de saldo del estado de cuenta resumido (SaldoPendiente autoritativo).
/// </summary>
public class ServicioEstadoCuentaIntegral
{
    private readonly CobranzaDbContext _db;

    public ServicioEstadoCuentaIntegral(CobranzaDbContext db) => _db = db;

    /// <summary>Arma el estado de cuenta integral; null si el cliente no existe.</summary>
    public async Task<EstadoCuentaIntegralDto?> ObtenerAsync(long idCliente, long? idUsuario, CancellationToken ct = default)
    {
        var fila = await (from c in _db.Clientes.AsNoTracking()
                          where c.IdCliente == idCliente
                          join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                          from r in rj.DefaultIfEmpty()
                          join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                          from z in zj.DefaultIfEmpty()
                          join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
                          from cl in clj.DefaultIfEmpty()
                          join ven in _db.Vendedores.AsNoTracking() on c.IdVendedor equals ven.IdVendedor into venj
                          from ven in venj.DefaultIfEmpty()
                          select new { c, r, z, cl, ven }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var ventas = await (from v in _db.Ventas.AsNoTracking()
                            where v.IdCliente == idCliente
                            join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                            from ev in evj.DefaultIfEmpty()
                            join co in _db.Condiciones.AsNoTracking() on v.IdCondicion equals co.IdCondicion into coj
                            from co in coj.DefaultIfEmpty()
                            orderby v.FechaCompra descending
                            select new { v, Estado = ev != null ? ev.Descripcion : null, Condicion = co != null ? co.Descripcion : null })
            .ToListAsync(ct);


        // Movimientos no monetarios por venta: 4 pronto pago (plan a meses), 5 ajuste/acuerdo, 6 racha, 7 renovación.
        // No suman a TotalAbonado; sin ellos el estado de cuenta no cuadra con el saldo.
        var idsVentas = ventas.Select(x => x.v.IdVenta).ToList();
        var movsNoMonetarios = new long?[]
        {
            MovimientoIds.BonificacionProntoPago, MovimientoIds.Ajuste,
            MovimientoIds.BonificacionPromocion, MovimientoIds.LiquidacionRenovacion
        };
        var movimientos = await _db.Pagos.AsNoTracking()
            .Where(p => idsVentas.Contains(p.IdVenta) && movsNoMonetarios.Contains(p.IdMovimiento))
            .GroupBy(p => new { p.IdVenta, p.IdMovimiento })
            .Select(g => new { g.Key.IdVenta, g.Key.IdMovimiento, Monto = g.Sum(p => p.MontoAbona) })
            .ToListAsync(ct);
        decimal Mov(long idVenta, long mov) => movimientos.Where(m => m.IdVenta == idVenta && m.IdMovimiento == mov).Sum(m => m.Monto);
        decimal Bonif(long idVenta) => Mov(idVenta, MovimientoIds.BonificacionPromocion)
                                       + Mov(idVenta, MovimientoIds.BonificacionProntoPago) + Mov(idVenta, MovimientoIds.Ajuste);
        decimal Traspaso(long idVenta) => Mov(idVenta, MovimientoIds.LiquidacionRenovacion);

        // Renovaciones: qué cuentas absorbió cada crédito y en qué crédito nuevo terminó cada cuenta liquidada.
        var idsTodas = ventas.Select(x => x.v.IdVenta).ToList();
        var unificaciones = await _db.VentaUnificaciones.AsNoTracking()
            .Where(u => idsTodas.Contains(u.IdVentaNueva) || idsTodas.Contains(u.IdVentaVieja))
            .ToListAsync(ct);
        string CodigoDe(long id)
        {
            var v = ventas.FirstOrDefault(x => x.v.IdVenta == id)?.v;
            return string.IsNullOrWhiteSpace(v?.CodigoVenta) ? $"V{id}" : v!.CodigoVenta!;
        }
        decimal SaldoRenovado(long idVenta) => unificaciones.Where(u => u.IdVentaNueva == idVenta).Sum(u => u.MontoTraspasado);
        string? Renueva(long idVenta)
        {
            var l = unificaciones.Where(u => u.IdVentaNueva == idVenta).Select(u => CodigoDe(u.IdVentaVieja)).ToList();
            return l.Count == 0 ? null : string.Join(", ", l);
        }
        string? RenovadaEn(long idVenta)
        {
            var u = unificaciones.FirstOrDefault(x => x.IdVentaVieja == idVenta);
            return u is null ? null : CodigoDe(u.IdVentaNueva);
        }

        EstadoCuentaIntegralCreditoDto Mapear(Domain.Entidades.Venta v, string? estado, string? condicion)
            => new(v.IdVenta, string.IsNullOrWhiteSpace(v.CodigoVenta) ? $"V{v.IdVenta}" : v.CodigoVenta!,
                v.FechaCompra, estado ?? "—", condicion,
                v.Subtotal, v.DescuentoGlobal, v.TotalFinal, v.Enganche, v.TotalAbonado,
                // Pagado = enganche + abonos aplicados (las bonificaciones/ajustes solo reducen el saldo).
                v.Enganche + v.TotalAbonado, Bonif(v.IdVenta), Traspaso(v.IdVenta), v.SaldoPendiente, v.FechaUltimoPago)
            {
                SaldoRenovado = SaldoRenovado(v.IdVenta),
                RenuevaCuentas = Renueva(v.IdVenta),
                RenovadaEnCuenta = RenovadaEn(v.IdVenta)
            };

        var noCanceladas = ventas.Where(x => x.v.IdEstatusVenta != EstatusVentaIds.Cancelada).ToList();
        var creditos = noCanceladas.Select(x => Mapear(x.v, x.Estado, x.Condicion)).ToList();

        var activas = noCanceladas.Where(x => x.v.IdEstatusVenta != EstatusVentaIds.Pagada && x.v.SaldoPendiente > 0).ToList();
        var liquidadas = noCanceladas.Where(x => x.v.IdEstatusVenta == EstatusVentaIds.Pagada || x.v.SaldoPendiente <= 0)
            .Where(x => !activas.Contains(x)).ToList();
        var canceladas = ventas.Where(x => x.v.IdEstatusVenta == EstatusVentaIds.Cancelada).ToList();

        // Detalle de créditos activos: productos, cuotas y pagos (consultas por lote para no hacer N+1).
        var idsActivas = activas.Select(x => x.v.IdVenta).ToList();
        var productos = await (from d in _db.DetallesVenta.AsNoTracking()
                               where idsActivas.Contains(d.IdVenta)
                               join p in _db.Productos.AsNoTracking() on d.IdProducto equals p.IdProducto
                               orderby d.IdVenta, d.IdDetalle
                               select new { d.IdVenta, Dto = new EstadoCuentaIntegralProductoDto(p.CodigoClave, p.Descripcion, d.Cantidad, d.PrecioCobrado, d.Importe) })
            .ToListAsync(ct);
        var cuotas = await _db.VentaCuotas.AsNoTracking()
            .Where(c => idsActivas.Contains(c.IdVenta)).OrderBy(c => c.IdVenta).ThenBy(c => c.NumeroCuota).ToListAsync(ct);
        var pagos = await (from p in _db.Pagos.AsNoTracking()
                           where idsActivas.Contains(p.IdVenta)
                           join m in _db.Movimientos.AsNoTracking() on p.IdMovimiento equals (long?)m.IdMovimiento into mj
                           from m in mj.DefaultIfEmpty()
                           join cob in _db.Cobradores.AsNoTracking() on p.IdCobrador equals cob.IdCobrador into cobj
                           from cob in cobj.DefaultIfEmpty()
                           orderby p.FechaPago
                           select new
                           {
                               p.IdVenta,
                               Dto = new EstadoCuentaIntegralPagoDto(p.FolioPago, p.FechaPago, m != null ? m.Descripcion : null,
                                   p.MontoAbona, p.SaldoActual, cob != null ? cob.Nombre : null)
                           }).ToListAsync(ct);

        var activos = activas.Select(x =>
        {
            var credito = Mapear(x.v, x.Estado, x.Condicion);
            var cuotasVenta = cuotas.Where(c => c.IdVenta == x.v.IdVenta).ToList();
            var cuotasDto = cuotasVenta.Select(c => new EstadoCuentaIntegralCuotaDto(
                c.NumeroCuota, c.FechaVencimiento, c.MontoCuota, c.MontoCuota - c.SaldoCuota, c.EstatusCuota,
                // Saldo insoluto programado: lo que falta por cubrir de la agenda tras esta cuota.
                cuotasVenta.Where(o => o.NumeroCuota > c.NumeroCuota).Sum(o => o.MontoCuota))).ToList();
            // Avance = lo cubierto (pagado + bonificaciones) sobre el total del crédito.
            var avance = credito.Total <= 0 ? 0m
                : Math.Min(100m, Math.Round((credito.Pagado + credito.Bonificaciones) / credito.Total * 100m, 1));
            return new EstadoCuentaIntegralDetalleDto(credito, x.v.Enganche, avance,
                productos.Where(p => p.IdVenta == x.v.IdVenta).Select(p => p.Dto).ToList(),
                cuotasDto,
                pagos.Where(p => p.IdVenta == x.v.IdVenta).Select(p => p.Dto).ToList())
            {
                BonificacionRacha = Mov(x.v.IdVenta, MovimientoIds.BonificacionPromocion),
                LiquidacionPlan = Mov(x.v.IdVenta, MovimientoIds.BonificacionProntoPago),
                Condonacion = Mov(x.v.IdVenta, MovimientoIds.Ajuste)
            };
        }).ToList();

        EstadoCuentaIntegralCerradoDto Cerrado(Domain.Entidades.Venta v, DateTimeOffset? cierre) => new(
            v.IdVenta, string.IsNullOrWhiteSpace(v.CodigoVenta) ? $"V{v.IdVenta}" : v.CodigoVenta!,
            v.FechaCompra, cierre, v.DescuentoGlobal, v.TotalFinal, v.Enganche, v.Enganche + v.TotalAbonado,
            Bonif(v.IdVenta), Traspaso(v.IdVenta))
        {
            SaldoRenovado = SaldoRenovado(v.IdVenta),
            RenuevaCuentas = Renueva(v.IdVenta),
            RenovadaEnCuenta = RenovadaEn(v.IdVenta)
        };

        var liquidados = liquidadas.Select(x => Cerrado(x.v, x.v.FechaUltimoPago ?? x.v.FechaModificacion)).ToList();
        var cancelados = canceladas.Select(x => Cerrado(x.v, x.v.FechaModificacion)).ToList();

        var resumen = new EstadoCuentaIntegralResumenDto(
            HistoricoSubtotal: noCanceladas.Sum(x => x.v.Subtotal),
            HistoricoDescuentos: noCanceladas.Sum(x => x.v.DescuentoGlobal),
            HistoricoVendido: noCanceladas.Sum(x => x.v.TotalFinal),
            HistoricoEnganches: noCanceladas.Sum(x => x.v.Enganche),
            HistoricoAbonos: noCanceladas.Sum(x => x.v.TotalAbonado),
            HistoricoPagado: noCanceladas.Sum(x => x.v.Enganche + x.v.TotalAbonado),
            HistoricoBonificaciones: noCanceladas.Sum(x => Bonif(x.v.IdVenta)),
            HistoricoTraspasos: noCanceladas.Sum(x => Traspaso(x.v.IdVenta)),
            SaldoPendiente: noCanceladas.Sum(x => x.v.SaldoPendiente),
            CreditosActivos: activas.Count,
            Liquidados: liquidadas.Count,
            Cancelados: canceladas.Count);

        var c0 = fila.c;
        var telefonos = string.Join(" / ", new[] { c0.TelefonoPrincipal, c0.TelefonoSecundario }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var cliente = new EstadoCuentaIntegralClienteDto(
            c0.IdCliente, BusquedaHelper.NombreCompleto(c0.Nombre, c0.ApPaterno, c0.ApMaterno), fila.cl?.Descripcion,
            telefonos, c0.Email, c0.Estatus == Estatus.Activo, c0.Direccion, fila.r?.Descripcion, fila.z?.Descripcion,
            fila.ven?.Nombre, c0.ReferenciaUbicacion);

        var perfil = RiesgoClienteRegla.Evaluar(c0.IdClasificacion) switch
        {
            NivelRiesgoCliente.Precaucion => "Medio",
            NivelRiesgoCliente.Bloqueado => "Alto",
            _ => "Bajo"
        };

        var empresa = await _db.Empresas.AsNoTracking().Select(e => new { e.NombreComercial, e.NombreCorto }).FirstOrDefaultAsync(ct);
        var nombreEmpresa = !string.IsNullOrWhiteSpace(empresa?.NombreComercial) ? empresa!.NombreComercial
            : !string.IsNullOrWhiteSpace(empresa?.NombreCorto) ? empresa!.NombreCorto : "Sistema de Cobranza";

        var operador = idUsuario is { } idu
            ? await _db.Usuarios.AsNoTracking().Where(u => u.IdUsuario == idu).Select(u => u.NombreCompleto).FirstOrDefaultAsync(ct)
            : null;

        var emision = DateTimeOffset.UtcNow;
        var observaciones = string.IsNullOrWhiteSpace(c0.Observaciones)
            ? "Sin observaciones registradas para este cliente."
            : c0.Observaciones!.Trim();

        // Huella de integridad: SHA-256 de las cifras principales del documento (permite verificar que no se alteró).
        var huellaBase = $"{c0.IdCliente}|{emision:yyyyMMddHHmm}|{resumen.HistoricoVendido}|{resumen.HistoricoDescuentos}|{resumen.HistoricoEnganches}|{resumen.HistoricoPagado}|{resumen.HistoricoBonificaciones}|{resumen.HistoricoTraspasos}|{resumen.SaldoPendiente}|{creditos.Count}";
        var sello = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(huellaBase))).ToLowerInvariant();

        return new EstadoCuentaIntegralDto(nombreEmpresa, emision, cliente, perfil, resumen, creditos, activos, liquidados,
            cancelados, observaciones, string.IsNullOrWhiteSpace(operador) ? "Sistema" : operador!, fila.z?.Descripcion, sello);
    }
}
