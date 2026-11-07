using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Reportes;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Reportes;

/// <summary>
/// Reporte de COBERTURA DE COBRANZA (por crédito/venta).
///
/// Para cada crédito vigente con saldo pendiente, calcula cuánto DEBERÍA haberse cobrado en el
/// rango según su cuota pactada y frecuencia (cuota × número de periodos del rango, topado al
/// saldo) y lo compara con lo realmente cobrado (abonos del periodo). Clasifica cada crédito como:
///   • PAGO            → cobró al menos lo esperado.
///   • PARCIAL         → cobró algo, pero menos de lo esperado.
///   • NO_PAGO         → no cobró nada en el rango teniendo obligación.
///   • SIN_FRECUENCIA  → el crédito no tiene frecuencia/cuota definida (no se puede calcular esperado),
///                       salvo que tenga saldo, no haya registrado pago en el rango y haya transcurrido
///                       más de un periodo desde la venta, en cuyo caso se marca NO_PAGO.
/// Responde "del total de créditos de la zona/ruta, cuántos pagaron y cuántos no".
/// </summary>
public partial class ServicioReportes
{
    public async Task<ReporteCoberturaResponse> CoberturaCobranzaAsync(
        ReporteCoberturaFiltro f, CancellationToken ct = default)
    {
        var (desde, hasta) = CalculoReportes.NormalizarRango(f.Desde, f.Hasta);
        var diasRango = Math.Max(1, (int)Math.Floor((hasta - desde).TotalDays) + 1);

        // Universo: créditos vigentes (no cancelados) con saldo pendiente, con su cliente/ruta/zona
        // y su frecuencia de pago (para conocer DiasPeriodo).
        var q = from v in _db.Ventas.AsNoTracking()
                where v.IdEstatusVenta != EstatusVentaIds.Cancelada && v.SaldoPendiente > 0
                join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                join fp in _db.CatalogoFrecuenciasPago.AsNoTracking() on v.IdFrecuenciaPago equals fp.IdFrecuenciaPago into fpj
                from fp in fpj.DefaultIfEmpty()
                select new { v, c, r, z, fp };

        if (f.IdZona is { } zona) q = q.Where(x => x.r != null && x.r.IdZona == zona);
        if (f.IdRuta is { } ruta) q = q.Where(x => x.c.IdRuta == ruta);

        var registros = await q.ToListAsync(ct);
        var productos = await ProductosPorVentaAsync(q.Select(x => x.v.IdVenta), ct);

        // Cobranza real por venta en el rango: solo movimientos de tipo ABONO.
        var ventaIds = registros.Select(x => x.v.IdVenta).ToList();
        var cobradoPorVenta = (await _db.Pagos.AsNoTracking()
            .Where(p => p.FechaPago >= desde && p.FechaPago <= hasta
                        && p.IdMovimiento == MovimientoIds.Abono
                        && ventaIds.Contains(p.IdVenta))
            .GroupBy(p => p.IdVenta)
            .Select(g => new { IdVenta = g.Key, Monto = g.Sum(x => (decimal?)x.MontoAbona) ?? 0m })
            .ToListAsync(ct))
            .ToDictionary(x => x.IdVenta, x => x.Monto);

        var hoy = FechaProyecto.Hoy;

        var detalle = registros.Select(x =>
        {
            var diasPeriodo = x.fp?.DiasPeriodo ?? 0;
            var cuota = x.v.MontoCuotaFija;
            var saldo = x.v.SaldoPendiente;
            var cobrado = cobradoPorVenta.GetValueOrDefault(x.v.IdVenta, 0m);
            var nombre = BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno);
            var referencia = (x.v.FechaUltimoPago ?? x.v.FechaCompra).EnZonaProyecto().Date;
            var diasSinPago = Math.Max(0, (hoy - referencia).Days);
            var diasDesdeVenta = Math.Max(0, (hoy - x.v.FechaCompra.EnZonaProyecto().Date).Days);

            decimal esperado;
            string estado;

            if (diasPeriodo > 0 && cuota > 0)
            {
                // Periodos completos que caben en el rango (al menos 1 para no subestimar la obligación).
                var periodos = Math.Max(1, diasRango / diasPeriodo);
                esperado = Math.Min(cuota * periodos, saldo);

                estado = cobrado <= 0m
                    ? EstadoCobertura.NoPago
                    : cobrado >= esperado ? EstadoCobertura.Pago : EstadoCobertura.Parcial;
            }
            else
            {
                // Sin frecuencia/cuota: no hay "esperado" calculable.
                esperado = 0m;
                // Pero si tiene saldo, no pagó en el rango y ya pasó más de un periodo desde la venta,
                // se considera NO_PAGO (regla confirmada con el usuario). DiasPeriodo 0 no da umbral,
                // por lo que se usa el umbral histórico sin frecuencia (7 días).
                var umbral = diasPeriodo > 0 ? diasPeriodo : DiasAtrasoSinFrecuencia;
                estado = cobrado <= 0m && saldo > 0 && diasDesdeVenta > umbral
                    ? EstadoCobertura.NoPago
                    : EstadoCobertura.SinFrecuencia;
            }

            return new ReporteCoberturaFila(
                x.v.IdVenta, x.v.CodigoVenta, x.c.IdCliente, nombre,
                x.c.Direccion, x.c.TelefonoPrincipal, x.z?.Descripcion, x.r?.Descripcion,
                productos.GetValueOrDefault(x.v.IdVenta, ""),
                cuota, x.fp?.Descripcion, diasPeriodo,
                esperado, cobrado, esperado - cobrado, estado,
                x.v.FechaUltimoPago, diasSinPago, saldo);
        })
        .OrderBy(x => x.Estado == EstadoCobertura.NoPago ? 0 : x.Estado == EstadoCobertura.Parcial ? 1 : 2)
        .ThenByDescending(x => x.SaldoPendiente)
        .ToList();

        // Resumen por zona.
        const string sinZona = "(sin zona)";
        var resumen = detalle
            .GroupBy(d => d.Zona ?? sinZona)
            .Select(g =>
            {
                var esperado = g.Sum(x => x.MontoEsperado);
                var cobrado = g.Sum(x => x.MontoCobrado);
                return new ReporteCoberturaResumenFila(
                    g.Key,
                    g.Count(),
                    g.Count(x => x.Estado == EstadoCobertura.Pago),
                    g.Count(x => x.Estado == EstadoCobertura.Parcial),
                    g.Count(x => x.Estado == EstadoCobertura.NoPago),
                    g.Count(x => x.Estado == EstadoCobertura.SinFrecuencia),
                    esperado, cobrado, Porcentaje(cobrado, esperado));
            })
            .OrderByDescending(x => x.EsperadoTotal)
            .ToList();

        var esperadoGlobal = detalle.Sum(x => x.MontoEsperado);
        var cobradoGlobal = detalle.Sum(x => x.MontoCobrado);

        return new ReporteCoberturaResponse(
            resumen, detalle,
            TotalCreditos: detalle.Count,
            Pagaron: detalle.Count(x => x.Estado == EstadoCobertura.Pago),
            Parciales: detalle.Count(x => x.Estado == EstadoCobertura.Parcial),
            NoPagaron: detalle.Count(x => x.Estado == EstadoCobertura.NoPago),
            SinFrecuencia: detalle.Count(x => x.Estado == EstadoCobertura.SinFrecuencia),
            EsperadoGlobal: esperadoGlobal,
            CobradoGlobal: cobradoGlobal,
            PorcentajeCoberturaGlobal: Porcentaje(cobradoGlobal, esperadoGlobal));
    }
}
