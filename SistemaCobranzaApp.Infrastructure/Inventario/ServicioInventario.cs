using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Constantes;
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
                           join mc in _db.CatalogoMarcas.AsNoTracking() on p.IdMarca equals mc.IdMarca into mcj
                           from mc in mcj.DefaultIfEmpty()
                           join cat in _db.CatalogoCategorias.AsNoTracking() on p.IdCategoria equals cat.IdCategoria into catj
                           from cat in catj.DefaultIfEmpty()
                           join col in _db.CatalogoColores.AsNoTracking() on p.IdColor equals col.IdColor into colj
                           from col in colj.DefaultIfEmpty()
                           join tl in _db.CatalogoTallas.AsNoTracking() on p.IdTalla equals tl.IdTalla into tlj
                           from tl in tlj.DefaultIfEmpty()
                           select new
                           {
                               p.IdProducto,
                               p.CodigoClave,
                               p.Descripcion,
                               p.StockMini,
                               Cantidad = ic != null ? ic.Cantidad : 0,
                               FechaActualizacion = ic != null ? ic.FechaActualizacion : (DateTimeOffset?)null,
                               Marca = mc != null ? mc.Descripcion : null,
                               Categoria = cat != null ? cat.Descripcion : null,
                               Color = col != null ? col.Descripcion : null,
                               Talla = tl != null ? tl.Descripcion : null
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
                    g.Max(x => x.FechaActualizacion),
                    p.Marca, p.Categoria, p.Color, p.Talla);
            })
            .OrderBy(x => x.Descripcion)
            .ToList();
    }

    public async Task<List<ExistenciaZonaDto>> ExistenciasZonaAsync(long idZona, CancellationToken ct = default)
    {
        var filas = await (from iz in _db.InventarioZona.AsNoTracking()
                           where iz.IdZona == idZona
                           join p in _db.Productos.AsNoTracking() on iz.IdProducto equals p.IdProducto
                           join mc in _db.CatalogoMarcas.AsNoTracking() on p.IdMarca equals mc.IdMarca into mcj
                           from mc in mcj.DefaultIfEmpty()
                           join cat in _db.CatalogoCategorias.AsNoTracking() on p.IdCategoria equals cat.IdCategoria into catj
                           from cat in catj.DefaultIfEmpty()
                           join col in _db.CatalogoColores.AsNoTracking() on p.IdColor equals col.IdColor into colj
                           from col in colj.DefaultIfEmpty()
                           join tl in _db.CatalogoTallas.AsNoTracking() on p.IdTalla equals tl.IdTalla into tlj
                           from tl in tlj.DefaultIfEmpty()
                           select new
                           {
                               iz.IdZona,
                               iz.IdProducto,
                               p.CodigoClave,
                               p.Descripcion,
                               iz.Cantidad,
                               iz.FechaActualizacion,
                               Marca = mc != null ? mc.Descripcion : null,
                               Categoria = cat != null ? cat.Descripcion : null,
                               Color = col != null ? col.Descripcion : null,
                               Talla = tl != null ? tl.Descripcion : null
                           })
            .ToListAsync(ct);

        return filas
            .GroupBy(x => x.IdProducto)
            .Select(g =>
            {
                var p = g.First();
                return new ExistenciaZonaDto(
                    p.IdZona, p.IdProducto, p.CodigoClave, p.Descripcion,
                    g.Sum(x => x.Cantidad), g.Max(x => x.FechaActualizacion),
                    p.Marca, p.Categoria, p.Color, p.Talla);
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

    public async Task<List<MovimientoInventarioDto>> HistorialAsync(long idProducto, int max, CancellationToken ct = default)
    {
        max = max is < 1 or > 500 ? 100 : max;
    
        // Proyectamos directamente en la base de datos para transferir solo las propiedades necesarias por red
        return await _db.InventarioMovimientos.AsNoTracking()
            .Where(m => m.IdProducto == idProducto)
            .OrderByDescending(m => m.IdMovimientoInv)
            .Take(max)
            .Select(m => new MovimientoInventarioDto(
                m.IdMovimientoInv, 
                m.IdProducto, 
                m.TipoMovimiento, 
                m.Cantidad,
                m.ExistenciaAnterior, 
                m.ExistenciaNueva, 
                m.IdZonaOrigen, 
                m.IdZonaDestino, 
                m.FechaMovimiento
            ))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Historial global de movimientos de inventario con filtros opcionales.
    /// - tipos: lista de TipoMovimiento a incluir (null/vacía = todos).
    /// - idZona: filtra por la zona afectada (origen o destino).
    /// - desde/hasta: rango de fechas (inclusive). 'hasta' se amplía al final del día.
    /// Enriquece cada renglón con clave/descripción del producto, nombre de la zona
    /// afectada y usuario que lo registró.
    /// </summary>
    public async Task<List<MovimientoHistorialDto>> HistorialGlobalAsync(
        IReadOnlyList<string>? tipos, long? idZona,
        DateTimeOffset? desde, DateTimeOffset? hasta,
        int max = 500, CancellationToken ct = default)
    {
        max = max is < 1 or > 2000 ? 500 : max;

        var q = _db.InventarioMovimientos.AsNoTracking().AsQueryable();

        if (tipos is { Count: > 0 })
            q = q.Where(m => tipos.Contains(m.TipoMovimiento));

        if (idZona is { } z)
            q = q.Where(m => m.IdZonaOrigen == z || m.IdZonaDestino == z);

        if (desde is { } d)
        {
            var inicioUtc = d.ToUniversalTime();
            q = q.Where(m => m.FechaMovimiento >= inicioUtc);
        }

        if (hasta is { } h)
        {
            // Incluir todo el día indicado en 'hasta'.
            var finDiaOffset = SistemaCobranzaApp.Domain.Comun.FechaProyecto.FinDiaUtc(
                SistemaCobranzaApp.Domain.Comun.FechaProyecto.EnZonaProyecto(h).Date);
            q = q.Where(m => m.FechaMovimiento <= finDiaOffset);
        }

        return await (from m in q
                      join p in _db.Productos.AsNoTracking() on m.IdProducto equals p.IdProducto into pj
                      from p in pj.DefaultIfEmpty()
                      join mc in _db.CatalogoMarcas.AsNoTracking() on (p != null ? p.IdMarca : (long?)null) equals mc.IdMarca into mcj
                      from mc in mcj.DefaultIfEmpty()
                      join cat in _db.CatalogoCategorias.AsNoTracking() on (p != null ? p.IdCategoria : (long?)null) equals cat.IdCategoria into catj
                      from cat in catj.DefaultIfEmpty()
                      // Zona afectada: destino si existe, de lo contrario origen.
                      join zd in _db.Zonas.AsNoTracking() on m.IdZonaDestino equals zd.IdZona into zdj
                      from zd in zdj.DefaultIfEmpty()
                      join zo in _db.Zonas.AsNoTracking() on m.IdZonaOrigen equals zo.IdZona into zoj
                      from zo in zoj.DefaultIfEmpty()
                      join u in _db.Usuarios.AsNoTracking() on m.IdUsuarioRegistro equals u.IdUsuario into uj
                      from u in uj.DefaultIfEmpty()
                      orderby m.IdMovimientoInv descending
                      select new MovimientoHistorialDto(
                          m.IdMovimientoInv,
                          m.FechaMovimiento,
                          m.TipoMovimiento,
                          m.IdProducto,
                          p != null ? p.CodigoClave : string.Empty,
                          p != null ? p.Descripcion : string.Empty,
                          m.Cantidad,
                          m.ExistenciaAnterior,
                          m.ExistenciaNueva,
                          m.IdZonaDestino ?? m.IdZonaOrigen,
                          zd != null ? zd.Descripcion : (zo != null ? zo.Descripcion : null),
                          u != null ? u.NombreCompleto : null,
                          m.Observaciones,
                          mc != null ? mc.Descripcion : null,
                          cat != null ? cat.Descripcion : null))
            .Take(max)
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
    /// Bloquea las existencias y rechaza la venta si otro dispositivo agotó el stock.
    /// </summary>
    public async Task DescontarPorVentaAsync(
        long idVenta, long idZona,
        IReadOnlyList<(long IdProducto, int Cantidad)> lineas,
        long? idUsuario, CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;

        foreach (var (idProducto, cantidad) in lineas.OrderBy(l => l.IdProducto))
        {
            if (cantidad <= 0) continue;

            var inv = await _db.InventarioZona.FromSqlInterpolated(
                $"SELECT * FROM inventariozona WHERE idzona = {idZona} AND idproducto = {idProducto} FOR UPDATE")
                .FirstOrDefaultAsync(ct);
            if (inv is not null) await _db.Entry(inv).ReloadAsync(ct);
            var anterior = inv?.Cantidad ?? 0;
            if (inv is null || anterior < cantidad)
                throw new InvalidOperationException($"Inventario insuficiente para el producto #{idProducto}. Disponible en central: {anterior}.");
            var nueva = CalculoInventario.CalcularSalida(anterior, cantidad);

            inv.Cantidad = nueva;
            inv.FechaActualizacion = ahora;

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
    
    /// <summary>
/// Devuelve al inventario de una zona las cantidades correspondientes a una venta
/// cancelada.
///
/// IMPORTANTE:
/// - No abre una transacción propia.
/// - No ejecuta SaveChangesAsync.
/// - Debe ejecutarse dentro de la transacción de la operación que cancela la venta.
/// - Registra el movimiento asociado a la venta mediante IdVenta.
/// </summary>
public async Task RestaurarPorVentaAsync(
    long idVenta,
    long idZona,
    IReadOnlyList<(long IdProducto, int Cantidad)> lineas,
    long? idUsuario,
    CancellationToken ct = default)
{
    var ahora = DateTimeOffset.UtcNow;

    foreach (var (idProducto, cantidad) in lineas)
    {
        if (cantidad <= 0)
            continue;

        var inv = await _db.InventarioZona
            .FirstOrDefaultAsync(
                x => x.IdZona == idZona &&
                     x.IdProducto == idProducto,
                ct);

        var anterior = inv?.Cantidad ?? 0;
        var nueva = checked(anterior + cantidad);

        if (inv is null)
        {
            inv = new Domain.Entidades.InventarioZona
            {
                IdZona = idZona,
                IdProducto = idProducto,
                Cantidad = nueva,
                FechaActualizacion = ahora
            };

            _db.InventarioZona.Add(inv);
        }
        else
        {
            inv.Cantidad = nueva;
            inv.FechaActualizacion = ahora;
        }

        _db.InventarioMovimientos.Add(
            new Domain.Entidades.InventarioMovimiento
            {
                IdProducto = idProducto,

                TipoMovimiento =
                    TipoMovimientoInventario.DevolucionVenta,

                Cantidad = cantidad,

                ExistenciaAnterior = anterior,

                ExistenciaNueva = nueva,

                IdZonaDestino = idZona,

                IdVenta = idVenta,

                IdUsuarioRegistro = idUsuario,

                Observaciones =
                    $"Devolución de inventario por cancelación de venta #{idVenta}.",

                FechaMovimiento = ahora
            });
    }
}
}
