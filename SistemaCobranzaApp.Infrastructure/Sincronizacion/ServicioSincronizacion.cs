using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Sincronizacion;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Sincronizacion;

/// <summary>
/// Motor de sincronización offline unificado para .NET MAUI:
///  - Push: Sube la cola de pagos capturados en calle sin conexión distribuyéndose en cascada.
///  - Pull: Descarga delta de cambios de cartera bajo el nuevo esquema financiero de cuotas.
/// </summary>
public class ServicioSincronizacion
{
    private readonly CobranzaDbContext _db;
    private readonly ServicioCobranza _cobranza;

    public ServicioSincronizacion(CobranzaDbContext db, ServicioCobranza cobranza)
    {
        _db = db;
        _cobranza = cobranza;
    }

    // ----------------------------- PUSH (SUBIR CAMBIOS OFFLINE) -----------------------------

    public async Task<PushPagosResponse> PushPagosAsync(PushPagosRequest req, CancellationToken ct = default)
    {
        var resultados = new List<PushPagoResultado>();

        foreach (var pago in req.Pagos)
        {
            var resultado = await ProcesarPagoEntranteAsync(pago, ct);
            resultados.Add(resultado);
        }

        return new PushPagosResponse(
            resultados,
            Aplicados: resultados.Count(r => r.Estado == EstadoSincronizacionPago.Aplicado),
            Duplicados: resultados.Count(r => r.Estado == EstadoSincronizacionPago.Duplicado),
            Conflictos: resultados.Count(r => r.Estado == EstadoSincronizacionPago.Conflicto),
            Errores: resultados.Count(r => r.Estado == EstadoSincronizacionPago.Error));
    }

    private async Task<PushPagoResultado> ProcesarPagoEntranteAsync(RegistrarPagoRequest pago, CancellationToken ct)
    {
        // 1. Idempotencia exacta mediante el UUID inmutable de .NET MAUI
        var existente = await _db.Pagos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PagoUuid == pago.PagoUuid, ct);
        var yaExiste = existente is not null;

        // 2. Extraer el saldo y estado autoritativo real del Servidor Central
        var venta = await _db.Ventas.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdVenta == pago.IdVenta, ct);

        if (venta is null)
            return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error,
                $"No se encontró la venta transaccional #{pago.IdVenta}.", null);

        // 🎯 CORREGIDO: Se sustituyó 'TotalAbonos' por 'TotalAbonado' alineado a tu tabla real de PostgreSQL
        var saldoReal = CalculoCobranza.CalcularSaldoAnterior(
            venta.TotalFinal, venta.Enganche, venta.TotalAbonado);

        var decision = ResolucionSincronizacion.Resolver(yaExiste, pago.MontoAbona, saldoReal);

        switch (decision)
        {
            case ResolucionSincronizacion.Decision.Duplicado:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Duplicado,
                    "El abono ya se encontraba sincronizado en el servidor central.",
                    existente is null ? null : new RegistrarPagoResponse(
                        existente.IdPago, existente.PagoUuid, existente.IdVenta,
                        existente.FolioPago ?? string.Empty, existente.ConsecutivoPago ?? 0,
                        existente.SaldoAnterior, existente.SaldoActual, existente.MontoAbona,
                        existente.SaldoActual <= 0));

            case ResolucionSincronizacion.Decision.Error:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error,
                    "El monto del abono offline es inválido o menor a cero.", null);

            case ResolucionSincronizacion.Decision.ConflictoYaLiquidada:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto,
                    "Conflicto: La cuenta ya se encuentra totalmente liquidada en el sistema.", null);

            case ResolucionSincronizacion.Decision.ConflictoExcedeSaldo:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto,
                    $"Conflicto de sincronización: El abono ({pago.MontoAbona:C2}) supera el saldo real ({saldoReal:C2}).", null);

            case ResolucionSincronizacion.Decision.Aplicar:
                var res = await _cobranza.RegistrarPagoAsync(pago, ct);
                return res.Exito
                    ? new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Aplicado, null, res.Respuesta)
                    : new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto, res.Error, null);

            default:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error, "Estado de resolución no reconocido.", null);
        }
    }

    // ----------------------------- PULL (DESCARGAR CAMBIOS DE CARTERA) -----------------------------

    public async Task<PullCambiosResponse> PullCambiosAsync(PullCambiosRequest req, CancellationToken ct = default)
    {
        var marcaServidor = DateTimeOffset.UtcNow;
        var desde = req.Desde ?? DateTimeOffset.MinValue;

        // 1. Construcción del query relacional optimizado para descargar delta de cuentas modificadas
        var ventasQ = from v in _db.Ventas.AsNoTracking()
                      join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                      join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                      from r in rj.DefaultIfEmpty()
                      join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                      from ev in evj.DefaultIfEmpty()
                      where v.FechaModificacion > desde
                      select new { v, c, r, ev };

        if (req.IdZona is { } zona)
            ventasQ = ventasQ.Where(x => x.r != null && x.r.IdZona == zona);

        var ventasFilas = await ventasQ.ToListAsync(ct);

        // 2. Mapeo estructural hacia el DTO local de almacenamiento SQLite del celular
        var ventas = ventasFilas.Select(x =>
        {
            // 🎯 CORREGIDO: Alineado al uso de 'TotalAbonado' para evitar inconsistencias en el cálculo del saldo offline
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonado);
            
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
        }).ToList();

        // 3. Obtener el historial comprimido de transacciones para amarrar la libreta del cobrador
        var pagosQ = _db.Pagos.AsNoTracking().Where(p => p.FechaModificacion > desde);
        if (req.IdCobrador is { } cob)
            pagosQ = pagosQ.Where(p => p.IdCobrador == cob);

        var pagos = await pagosQ
            .Select(p => new PagoSyncDto(
                p.IdPago, p.PagoUuid, p.IdVenta, p.IdCliente, p.MontoAbona,
                p.SaldoAnterior, p.SaldoActual, p.FolioPago, p.ConsecutivoPago,
                p.IdCobrador, p.FechaPago, p.FechaModificacion))
            .ToListAsync(ct);

        return new PullCambiosResponse(marcaServidor, ventas, pagos);
    }
}
