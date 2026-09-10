using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Sincronizacion;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Sincronizacion;

/// <summary>
/// Motor de sincronización offline:
///  - Push: sube la cola de pagos capturados sin conexión. Cada pago se
///    resuelve con idempotencia (por PagoUuid) y detección de conflictos
///    (el saldo autoritativo es el del servidor).
///  - Pull: baja los cambios (ventas y pagos) desde una marca de tiempo.
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

    // ----------------------------- PUSH -----------------------------

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
        // ¿Ya existe? (idempotencia)
        var existente = await _db.Pagos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PagoUuid == pago.PagoUuid, ct);
        var yaExiste = existente is not null;

        // Saldo autoritativo del servidor para esta venta.
        var venta = await _db.Ventas.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdVenta == pago.IdVenta, ct);

        if (venta is null)
            return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error,
                $"No se encontró la venta #{pago.IdVenta}.", null);

        var saldoReal = CalculoCobranza.CalcularSaldoAnterior(
            venta.TotalFinal, venta.Enganche, venta.TotalAbonos);

        var decision = ResolucionSincronizacion.Resolver(yaExiste, pago.MontoAbona, saldoReal);

        switch (decision)
        {
            case ResolucionSincronizacion.Decision.Duplicado:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Duplicado,
                    "El pago ya había sido sincronizado.",
                    existente is null ? null : new RegistrarPagoResponse(
                        existente.IdPago, existente.PagoUuid, existente.IdVenta,
                        existente.FolioPago ?? string.Empty, existente.ConsecutivoPago ?? 0,
                        existente.SaldoAnterior, existente.SaldoActual, existente.MontoAbona,
                        existente.SaldoActual <= 0));

            case ResolucionSincronizacion.Decision.Error:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error,
                    "El monto del abono es inválido.", null);

            case ResolucionSincronizacion.Decision.ConflictoYaLiquidada:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto,
                    "La venta ya estaba liquidada en el servidor.", null);

            case ResolucionSincronizacion.Decision.ConflictoExcedeSaldo:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto,
                    $"El abono ({pago.MontoAbona:C2}) excede el saldo real ({saldoReal:C2}).", null);

            case ResolucionSincronizacion.Decision.Aplicar:
                var res = await _cobranza.RegistrarPagoAsync(pago, ct);
                return res.Exito
                    ? new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Aplicado, null, res.Respuesta)
                    : new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Conflicto, res.Error, null);

            default:
                return new PushPagoResultado(pago.PagoUuid, EstadoSincronizacionPago.Error, "Estado no reconocido.", null);
        }
    }

    // ----------------------------- PULL -----------------------------

    public async Task<PullCambiosResponse> PullCambiosAsync(PullCambiosRequest req, CancellationToken ct = default)
    {
        var marcaServidor = DateTimeOffset.UtcNow;
        var desde = req.Desde ?? DateTimeOffset.MinValue;

        // Ventas modificadas desde la marca.
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

        var ventas = ventasFilas.Select(x =>
        {
            var saldo = CalculoCobranza.CalcularSaldoAnterior(x.v.TotalFinal, x.v.Enganche, x.v.TotalAbonos);
            var nombre = string.Join(" ", new[] { x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno }
                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            return new CarteraCuentaDto(
                x.v.IdVenta, x.v.IdCliente, nombre, x.r?.Descripcion, x.r?.IdZona,
                x.v.CodigoVenta, x.v.TotalFinal, saldo, x.v.PagoRequerido,
                x.v.IdEstatusVenta, x.ev?.Descripcion, x.v.FechaUltimoPago);
        }).ToList();

        // Pagos modificados desde la marca (opcionalmente del cobrador).
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
