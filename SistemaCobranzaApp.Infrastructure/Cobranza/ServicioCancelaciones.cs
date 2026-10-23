using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

public class ResultadoCancelacionPago
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public bool NoEncontrado { get; init; }
    public CancelarPagoResponse? Respuesta { get; init; }

    public static ResultadoCancelacionPago Fallo(string e) =>
        new()
        {
            Exito = false,
            Error = e
        };

    public static ResultadoCancelacionPago NoExiste(string e) =>
        new()
        {
            Exito = false,
            Error = e,
            NoEncontrado = true
        };

    public static ResultadoCancelacionPago Ok(
        CancelarPagoResponse r) =>
        new()
        {
            Exito = true,
            Respuesta = r
        };
}

public class ResultadoCancelacionVenta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public bool NoEncontrado { get; init; }
    public CancelarVentaResponse? Respuesta { get; init; }

    public static ResultadoCancelacionVenta Fallo(string e) =>
        new()
        {
            Exito = false,
            Error = e
        };

    public static ResultadoCancelacionVenta NoExiste(string e) =>
        new()
        {
            Exito = false,
            Error = e,
            NoEncontrado = true
        };

    public static ResultadoCancelacionVenta Ok(
        CancelarVentaResponse r) =>
        new()
        {
            Exito = true,
            Respuesta = r
        };
}

/// <summary>
/// Servicio encargado de la cancelación de transacciones.
///
/// Reglas:
/// - Un pago puede cancelarse si la venta no está cancelada.
/// - Una venta solamente puede cancelarse si NO tiene abonos.
/// - La venta nunca se elimina físicamente.
/// - La cancelación cambia IdEstatusVenta a CANCELADA.
/// - El inventario se devuelve dentro de la misma transacción.
/// </summary>
public class ServicioCancelaciones
{
    private readonly CobranzaDbContext _db;
    private readonly ServicioInventario _inventario;

    public ServicioCancelaciones(
        CobranzaDbContext db,
        ServicioInventario inventario)
    {
        _db = db;
        _inventario = inventario;
    }

    // =====================================================================
    // CANCELAR PAGO
    // =====================================================================

    public async Task<ResultadoCancelacionPago> CancelarPagoAsync(
        long idPago,
        CancellationToken ct = default)
    {
        await using var tx =
            await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var pago =
                await _db.Pagos
                    .FirstOrDefaultAsync(
                        p => p.IdPago == idPago,
                        ct);

            if (pago is null)
            {
                return ResultadoCancelacionPago.NoExiste(
                    $"No se encontró el pago #{idPago}.");
            }

            var venta =
                await _db.Ventas
                    .FirstOrDefaultAsync(
                        v => v.IdVenta == pago.IdVenta,
                        ct);

            if (venta is null)
            {
                return ResultadoCancelacionPago.Fallo(
                    $"No se encontró la venta #{pago.IdVenta} " +
                    "vinculada al pago.");
            }

            if (venta.IdEstatusVenta ==
                EstatusVentaIds.Cancelada)
            {
                return ResultadoCancelacionPago.Fallo(
                    "La venta asociada se encuentra cancelada; " +
                    "no es posible alterar sus movimientos históricos.");
            }

            var montoRevertido =
                pago.MontoAbona;

            _db.Pagos.Remove(pago);

            await _db.SaveChangesAsync(ct);

            // Los triggers de PostgreSQL recalculan los saldos
            // de la venta y sus cuotas.
            await _db.Entry(venta).ReloadAsync(ct);

            await tx.CommitAsync(ct);

            return ResultadoCancelacionPago.Ok(
                new CancelarPagoResponse(
                    idPago,
                    venta.IdVenta,
                    montoRevertido,
                    venta.SaldoPendiente,
                    venta.IdEstatusVenta));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // =====================================================================
    // CANCELAR VENTA
    // =====================================================================
public async Task<ResultadoCancelacionVenta> CancelarVentaAsync(
    long idVenta,
    long? idUsuario = null,
    CancellationToken ct = default)
{
    await using var tx =
        await _db.Database.BeginTransactionAsync(ct);

    try
    {
        var venta = await _db.Ventas
            .FirstOrDefaultAsync(
                v => v.IdVenta == idVenta,
                ct);

        if (venta is null)
        {
            return ResultadoCancelacionVenta.NoExiste(
                $"No se encontró la venta #{idVenta}.");
        }

        // Una venta cancelada no puede volver a cancelarse.
        if (venta.IdEstatusVenta ==
            EstatusVentaIds.Cancelada)
        {
            return ResultadoCancelacionVenta.Fallo(
                "La venta ya se encuentra en estado CANCELADA.");
        }

        // Regla de negocio:
        // solamente se pueden cancelar ventas sin abonos.
        if (venta.TotalAbonado > 0)
        {
            return ResultadoCancelacionVenta.Fallo(
                "La venta no puede cancelarse porque ya tiene " +
                "abonos registrados.");
        }

        // La venta tomó inventario de la zona del cliente.
        // Por lo tanto, necesitamos identificar esa misma zona
        // para devolver exactamente el inventario.
        var idZona = await (
            from cliente in _db.Clientes.AsNoTracking()

            join ruta in _db.Rutas.AsNoTracking()
                on cliente.IdRuta equals ruta.IdRuta

            where cliente.IdCliente == venta.IdCliente

            select (long?)ruta.IdZona
        ).FirstOrDefaultAsync(ct);

        if (!idZona.HasValue)
        {
            return ResultadoCancelacionVenta.Fallo(
                "No se pudo determinar la zona del cliente " +
                "asociada a la venta.");
        }

        // Recuperamos exactamente los productos y cantidades
        // que pertenecen a la venta.
        var detalles = await _db.DetallesVenta
            .AsNoTracking()
            .Where(d => d.IdVenta == idVenta)
            .Select(d => new
            {
                d.IdProducto,
                d.Cantidad
            })
            .ToListAsync(ct);

        if (detalles.Count == 0)
        {
            return ResultadoCancelacionVenta.Fallo(
                "La venta no contiene productos para cancelar.");
        }

        // Si el mismo producto aparece en varias líneas,
        // devolvemos la cantidad total.
        var productos = detalles
            .GroupBy(d => d.IdProducto)
            .Select(g => (
                IdProducto: g.Key,
                Cantidad: g.Sum(x => x.Cantidad)
            ))
            .ToList();

        // Restaurar exactamente el inventario que la venta
        // había descontado de la zona.
        await _inventario.RestaurarPorVentaAsync(
            venta.IdVenta,
            idZona.Value,
            productos,
            idUsuario,
            ct);

        // Finalmente cambiamos el estado de la venta.
        venta.IdEstatusVenta =
            EstatusVentaIds.Cancelada;

        venta.FechaModificacion =
            DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var estatusDescripcion =
            await _db.EstatusVenta
                .AsNoTracking()
                .Where(e =>
                    e.IdEstatusVenta ==
                    EstatusVentaIds.Cancelada)
                .Select(e => e.Descripcion)
                .FirstOrDefaultAsync(ct);

        await tx.CommitAsync(ct);

        return ResultadoCancelacionVenta.Ok(
            new CancelarVentaResponse(
                idVenta,
                venta.IdEstatusVenta,
                estatusDescripcion));
    }
    catch
    {
        await tx.RollbackAsync(ct);
        throw;
    }
}
}