using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Inventario;

public class ResultadoMovimiento
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public MovimientoInventarioDto? Movimiento { get; init; }

    public static ResultadoMovimiento Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoMovimiento Ok(MovimientoInventarioDto m) => new() { Exito = true, Movimiento = m };
}

/// <summary>
/// Existencias e ingresos/ajustes de inventario (central y por zona),
/// portado del modelo de InventarioRepository (app WPF). Cada movimiento
/// actualiza la existencia y registra el historial, en una transacción.
/// Las salidas por venta y las propuestas de envío se abordan aparte.
/// </summary>
public class ServicioInventario
{
    private readonly CobranzaDbContext _db;

    public ServicioInventario(CobranzaDbContext db) => _db = db;

    // ---------------- Existencias ----------------

    public async Task<List<ExistenciaCentralDto>> ExistenciasCentralAsync(CancellationToken ct = default)
    {
        var filas = await (from p in _db.Productos.AsNoTracking()
                           join ic in _db.InventarioCentral.AsNoTracking() on p.IdProducto equals ic.IdProducto into icj
                           from ic in icj.DefaultIfEmpty()
                           select new
                           {
                               p.IdProducto,
                               p.CodigoClave,
                               p.Descripcion,
                               p.StockMini,
                               Cantidad = ic != null ? ic.Cantidad : 0,
                               FechaActualizacion = ic != null ? ic.FechaActualizacion : (DateTimeOffset?)null
                           })
            .ToListAsync(ct);

        return filas
            .GroupBy(x => x.IdProducto)
            .Select(g =>
            {
                var p = g.First();
                return new ExistenciaCentralDto(
                    p.IdProducto, p.CodigoClave, p.Descripcion,
                    g.Sum(x => x.Cantidad), p.StockMini,
                    g.Max(x => x.FechaActualizacion));
            })
            .OrderBy(x => x.Descripcion)
            .ToList();
    }

    public async Task<List<ExistenciaZonaDto>> ExistenciasZonaAsync(long idZona, CancellationToken ct = default)
    {
        var filas = await (from iz in _db.InventarioZona.AsNoTracking()
                           where iz.IdZona == idZona
                           join p in _db.Productos.AsNoTracking() on iz.IdProducto equals p.IdProducto
                           select new
                           {
                               iz.IdZona,
                               iz.IdProducto,
                               p.CodigoClave,
                               p.Descripcion,
                               iz.Cantidad,
                               iz.FechaActualizacion
                           })
            .ToListAsync(ct);

        return filas
            .GroupBy(x => x.IdProducto)
            .Select(g =>
            {
                var p = g.First();
                return new ExistenciaZonaDto(
                    p.IdZona, p.IdProducto, p.CodigoClave, p.Descripcion,
                    g.Sum(x => x.Cantidad), g.Max(x => x.FechaActualizacion));
            })
            .OrderBy(x => x.Descripcion)
            .ToList();
    }

    // ---------------- Movimiento central ----------------

    public async Task<ResultadoMovimiento> MovimientoCentralAsync(MovimientoCentralRequest req, CancellationToken ct = default)
    {
        var validacion = CalculoInventario.ValidarEntradaOAjuste(req.Cantidad, req.EsAjuste);
        if (validacion != CalculoInventario.ResultadoValidacion.Ok)
            return ResultadoMovimiento.Fallo(MensajeValidacion(validacion));

        if (!await _db.Productos.AnyAsync(p => p.IdProducto == req.IdProducto, ct))
            return ResultadoMovimiento.Fallo($"El producto #{req.IdProducto} no existe.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var inv = await _db.InventarioCentral.FirstOrDefaultAsync(x => x.IdProducto == req.IdProducto, ct);
            var anterior = inv?.Cantidad ?? 0;
            var nueva = CalculoInventario.CalcularNuevaExistencia(anterior, req.Cantidad, req.EsAjuste);
            var ahora = DateTimeOffset.UtcNow;

            if (inv is null)
            {
                inv = new InventarioCentral { IdProducto = req.IdProducto, Cantidad = nueva, FechaActualizacion = ahora };
                _db.InventarioCentral.Add(inv);
            }
            else
            {
                inv.Cantidad = nueva;
                inv.FechaActualizacion = ahora;
            }

            var mov = new InventarioMovimiento
            {
                IdProducto = req.IdProducto,
                TipoMovimiento = CalculoInventario.TipoCentral(req.EsAjuste),
                Cantidad = req.Cantidad,
                ExistenciaAnterior = anterior,
                ExistenciaNueva = nueva,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = req.IdUsuarioRegistro,
                FechaMovimiento = ahora
            };
            _db.InventarioMovimientos.Add(mov);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoMovimiento.Ok(Map(mov));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------- Movimiento zona ----------------

    public async Task<ResultadoMovimiento> MovimientoZonaAsync(MovimientoZonaRequest req, CancellationToken ct = default)
    {
        var validacion = CalculoInventario.ValidarEntradaOAjuste(req.Cantidad, req.EsAjuste);
        if (validacion != CalculoInventario.ResultadoValidacion.Ok)
            return ResultadoMovimiento.Fallo(MensajeValidacion(validacion));

        if (!await _db.Zonas.AnyAsync(z => z.IdZona == req.IdZona, ct))
            return ResultadoMovimiento.Fallo($"La zona #{req.IdZona} no existe.");

        if (!await _db.Productos.AnyAsync(p => p.IdProducto == req.IdProducto, ct))
            return ResultadoMovimiento.Fallo($"El producto #{req.IdProducto} no existe.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var inv = await _db.InventarioZona.FirstOrDefaultAsync(
                x => x.IdZona == req.IdZona && x.IdProducto == req.IdProducto, ct);
            var anterior = inv?.Cantidad ?? 0;
            var nueva = CalculoInventario.CalcularNuevaExistencia(anterior, req.Cantidad, req.EsAjuste);
            var ahora = DateTimeOffset.UtcNow;

            if (inv is null)
            {
                inv = new InventarioZona { IdZona = req.IdZona, IdProducto = req.IdProducto, Cantidad = nueva, FechaActualizacion = ahora };
                _db.InventarioZona.Add(inv);
            }
            else
            {
                inv.Cantidad = nueva;
                inv.FechaActualizacion = ahora;
            }

            var mov = new InventarioMovimiento
            {
                IdProducto = req.IdProducto,
                TipoMovimiento = CalculoInventario.TipoZona(req.EsAjuste),
                Cantidad = req.Cantidad,
                ExistenciaAnterior = anterior,
                ExistenciaNueva = nueva,
                IdZonaDestino = req.IdZona,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = req.IdUsuarioRegistro,
                FechaMovimiento = ahora
            };
            _db.InventarioMovimientos.Add(mov);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoMovimiento.Ok(Map(mov));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------- Historial ----------------

    public Task<List<MovimientoInventarioDto>> HistorialAsync(long idProducto, int max, CancellationToken ct = default)
    {
        max = max is < 1 or > 500 ? 100 : max;
        return _db.InventarioMovimientos.AsNoTracking()
            .Where(m => m.IdProducto == idProducto)
            .OrderByDescending(m => m.IdMovimientoInv)
            .Take(max)
            .Select(m => Map(m))
            .ToListAsync(ct);
    }

    private static MovimientoInventarioDto Map(InventarioMovimiento m) => new(
        m.IdMovimientoInv, m.IdProducto, m.TipoMovimiento, m.Cantidad,
        m.ExistenciaAnterior, m.ExistenciaNueva, m.IdZonaOrigen, m.IdZonaDestino, m.FechaMovimiento);

    private static string MensajeValidacion(CalculoInventario.ResultadoValidacion v) => v switch
    {
        CalculoInventario.ResultadoValidacion.CantidadInvalida => "La cantidad de entrada debe ser mayor a cero.",
        CalculoInventario.ResultadoValidacion.AjusteNegativo => "La existencia ajustada no puede ser negativa.",
        _ => "Movimiento inválido."
    };

    // ---------------- Salida por venta ----------------

    /// <summary>
    /// Descuenta de la zona del cliente las cantidades vendidas y registra un
    /// movimiento tipo VENTA por producto. La zona se resuelve por la ruta del
    /// cliente. Pensado para llamarse dentro de la transacción del alta de venta,
    /// por eso NO abre transacción propia (usa la del contexto llamante) ni
    /// llama SaveChanges: el llamador confirma.
    /// La existencia nunca queda negativa (piso 0), igual que la app WPF.
    /// </summary>
    public async Task DescontarPorVentaAsync(
        long idVenta, long idZona,
        IReadOnlyList<(long IdProducto, int Cantidad)> lineas,
        long? idUsuario, CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;

        foreach (var (idProducto, cantidad) in lineas)
        {
            if (cantidad <= 0) continue;

            var inv = await _db.InventarioZona.FirstOrDefaultAsync(
                x => x.IdZona == idZona && x.IdProducto == idProducto, ct);
            var anterior = inv?.Cantidad ?? 0;
            var nueva = CalculoInventario.CalcularSalida(anterior, cantidad);

            if (inv is null)
            {
                inv = new Domain.Entidades.InventarioZona
                { IdZona = idZona, IdProducto = idProducto, Cantidad = nueva, FechaActualizacion = ahora };
                _db.InventarioZona.Add(inv);
            }
            else
            {
                inv.Cantidad = nueva;
                inv.FechaActualizacion = ahora;
            }

            _db.InventarioMovimientos.Add(new Domain.Entidades.InventarioMovimiento
            {
                IdProducto = idProducto,
                TipoMovimiento = TipoMovimientoInventario.Venta,
                Cantidad = cantidad,
                ExistenciaAnterior = anterior,
                ExistenciaNueva = nueva,
                IdZonaOrigen = idZona,
                IdVenta = idVenta,
                IdUsuarioRegistro = idUsuario,
                FechaMovimiento = ahora
            });
        }
    }
}
