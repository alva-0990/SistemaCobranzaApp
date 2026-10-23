using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Inventario;

public class ResultadoPropuesta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public PropuestaDto? Propuesta { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoPropuesta Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoPropuesta NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoPropuesta Ok(PropuestaDto p) => new() { Exito = true, Propuesta = p };
}

/// <summary>
/// Propuestas de envío central→zona, portadas de InventarioRepository (WPF):
///  - Crear: nace PENDIENTE (no toca inventario).
///  - Confirmar: valida stock central suficiente, descuenta central, suma a
///    la zona destino, registra un movimiento por producto (SALIDA de central
///    y ENTRADA a zona) y marca CONFIRMADA. Todo en una transacción.
///  - Rechazar: marca RECHAZADA sin tocar inventario.
/// </summary>
public class ServicioPropuestas
{
    private readonly CobranzaDbContext _db;

    public ServicioPropuestas(CobranzaDbContext db) => _db = db;

    // ---------------- Crear ----------------

    public async Task<ResultadoPropuesta> CrearAsync(CrearPropuestaRequest req, CancellationToken ct = default)
    {
        var lineas = (req.Detalles ?? Array.Empty<CrearPropuestaDetalleRequest>())
            .Select(d => new CalculoPropuesta.LineaPropuesta(d.IdProducto, d.Cantidad))
            .ToList();

        var val = CalculoPropuesta.Validar(req.IdZonaDestino, lineas);
        if (val != CalculoPropuesta.ResultadoValidacion.Ok)
            return ResultadoPropuesta.Fallo(MensajeValidacion(val));

        if (!await _db.Zonas.AnyAsync(z => z.IdZona == req.IdZonaDestino, ct))
            return ResultadoPropuesta.Fallo($"La zona destino #{req.IdZonaDestino} no existe.");

        var idsProd = lineas.Select(l => l.IdProducto).ToList();
        var existentes = await _db.Productos.Where(p => idsProd.Contains(p.IdProducto)).Select(p => p.IdProducto).ToListAsync(ct);
        var faltante = idsProd.FirstOrDefault(id => !existentes.Contains(id));
        if (faltante != 0)
            return ResultadoPropuesta.Fallo($"El producto #{faltante} no existe.");

        var ahora = DateTimeOffset.UtcNow;
        var propuesta = new PropuestaEnvio
        {
            IdZonaDestino = req.IdZonaDestino,
            Estatus = EstatusPropuesta.Pendiente,
            Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
            IdUsuarioRegistro = req.IdUsuarioRegistro,
            FechaCreacion = ahora,
            Detalles = req.Detalles!.Select(d => new PropuestaEnvioDetalle
            { IdProducto = d.IdProducto, Cantidad = d.Cantidad }).ToList()
        };

        _db.PropuestasEnvio.Add(propuesta);
        await _db.SaveChangesAsync(ct);

        return ResultadoPropuesta.Ok((await ObtenerAsync(propuesta.IdPropuesta, ct))!);
    }

    // ---------------- Confirmar ----------------

    public async Task<ResultadoPropuesta> ConfirmarAsync(long idPropuesta, long? idUsuarioConfirma, CancellationToken ct = default)
    {
        var propuesta = await _db.PropuestasEnvio
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.IdPropuesta == idPropuesta, ct);

        if (propuesta is null)
            return ResultadoPropuesta.NoExiste($"No se encontró la propuesta #{idPropuesta}.");

        if (!CalculoPropuesta.PuedeResolver(propuesta.Estatus))
            return ResultadoPropuesta.Fallo($"La propuesta ya está {propuesta.Estatus}.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var ahora = DateTimeOffset.UtcNow;

            foreach (var det in propuesta.Detalles)
            {
                // Existencia central actual.
                var central = await _db.InventarioCentral.FirstOrDefaultAsync(x => x.IdProducto == det.IdProducto, ct);
                var existenciaCentral = central?.Cantidad ?? 0;

                if (!CalculoPropuesta.StockSuficiente(existenciaCentral, det.Cantidad))
                {
                    await tx.RollbackAsync(ct);
                    return ResultadoPropuesta.Fallo(
                        $"Stock central insuficiente para el producto #{det.IdProducto} " +
                        $"(disponible {existenciaCentral}, solicitado {det.Cantidad}).");
                }

                // Descontar del central.
                var nuevaCentral = existenciaCentral - det.Cantidad;
                if (central is null)
                {
                    central = new InventarioCentral { IdProducto = det.IdProducto, Cantidad = nuevaCentral, FechaActualizacion = ahora };
                    _db.InventarioCentral.Add(central);
                }
                else
                {
                    central.Cantidad = nuevaCentral;
                    central.FechaActualizacion = ahora;
                }

                _db.InventarioMovimientos.Add(new InventarioMovimiento
                {
                    IdProducto = det.IdProducto,
                    TipoMovimiento = TipoMovimientoInventario.SalidaZona, // salida del central hacia zona
                    Cantidad = det.Cantidad,
                    ExistenciaAnterior = existenciaCentral,
                    ExistenciaNueva = nuevaCentral,
                    IdZonaDestino = propuesta.IdZonaDestino,
                    IdPropuesta = propuesta.IdPropuesta,
                    IdUsuarioRegistro = idUsuarioConfirma,
                    FechaMovimiento = ahora
                });

                // Sumar a la zona destino.
                var zona = await _db.InventarioZona.FirstOrDefaultAsync(
                    x => x.IdZona == propuesta.IdZonaDestino && x.IdProducto == det.IdProducto, ct);
                var existenciaZona = zona?.Cantidad ?? 0;
                var nuevaZona = existenciaZona + det.Cantidad;

                if (zona is null)
                {
                    zona = new InventarioZona { IdZona = propuesta.IdZonaDestino, IdProducto = det.IdProducto, Cantidad = nuevaZona, FechaActualizacion = ahora };
                    _db.InventarioZona.Add(zona);
                }
                else
                {
                    zona.Cantidad = nuevaZona;
                    zona.FechaActualizacion = ahora;
                }

                _db.InventarioMovimientos.Add(new InventarioMovimiento
                {
                    IdProducto = det.IdProducto,
                    TipoMovimiento = TipoMovimientoInventario.EntradaZona,
                    Cantidad = det.Cantidad,
                    ExistenciaAnterior = existenciaZona,
                    ExistenciaNueva = nuevaZona,
                    IdZonaDestino = propuesta.IdZonaDestino,
                    IdPropuesta = propuesta.IdPropuesta,
                    IdUsuarioRegistro = idUsuarioConfirma,
                    FechaMovimiento = ahora
                });
            }

            propuesta.Estatus = EstatusPropuesta.Confirmada;
            propuesta.FechaResolucion = ahora;
            propuesta.IdUsuarioConfirma = idUsuarioConfirma;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoPropuesta.Ok((await ObtenerAsync(idPropuesta, ct))!);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ---------------- Rechazar ----------------

    public async Task<ResultadoPropuesta> RechazarAsync(long idPropuesta, long? idUsuario, CancellationToken ct = default)
    {
        var propuesta = await _db.PropuestasEnvio.FirstOrDefaultAsync(p => p.IdPropuesta == idPropuesta, ct);
        if (propuesta is null)
            return ResultadoPropuesta.NoExiste($"No se encontró la propuesta #{idPropuesta}.");

        if (!CalculoPropuesta.PuedeResolver(propuesta.Estatus))
            return ResultadoPropuesta.Fallo($"La propuesta ya está {propuesta.Estatus}.");

        // Rechazar NO toca inventario.
        propuesta.Estatus = EstatusPropuesta.Rechazada;
        propuesta.FechaResolucion = DateTimeOffset.UtcNow;
        propuesta.IdUsuarioConfirma = idUsuario;
        await _db.SaveChangesAsync(ct);

        return ResultadoPropuesta.Ok((await ObtenerAsync(idPropuesta, ct))!);
    }

    // ---------------- Consultas ----------------

    public Task<List<PropuestaDto>> ListarAsync(string? estatus, long? idZona, CancellationToken ct = default)
    {
        var q = _db.PropuestasEnvio.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estatus)) q = q.Where(p => p.Estatus == estatus);
        if (idZona is { } z) q = q.Where(p => p.IdZonaDestino == z);

        return (from p in q
                join zn in _db.Zonas.AsNoTracking() on p.IdZonaDestino equals zn.IdZona into znj
                from zn in znj.DefaultIfEmpty()
                orderby p.IdPropuesta descending
                select new PropuestaDto(
                    p.IdPropuesta, p.IdZonaDestino, zn != null ? zn.Descripcion : null,
                    p.Estatus, p.Observaciones, p.FechaCreacion, p.FechaResolucion,
                    new List<PropuestaDetalleDto>()))
               .ToListAsync(ct);
    }

    public async Task<PropuestaDto?> ObtenerAsync(long idPropuesta, CancellationToken ct = default)
    {
        var p = await (from x in _db.PropuestasEnvio.AsNoTracking()
                       where x.IdPropuesta == idPropuesta
                       join zn in _db.Zonas.AsNoTracking() on x.IdZonaDestino equals zn.IdZona into znj
                       from zn in znj.DefaultIfEmpty()
                       select new { x, zn }).FirstOrDefaultAsync(ct);
        if (p is null) return null;

        var detalles = await (from d in _db.PropuestasEnvioDetalle.AsNoTracking()
                              where d.IdPropuesta == idPropuesta
                              join pr in _db.Productos.AsNoTracking() on d.IdProducto equals pr.IdProducto into prj
                              from pr in prj.DefaultIfEmpty()
                              join mc in _db.CatalogoMarcas.AsNoTracking() on (pr != null ? pr.IdMarca : (long?)null) equals mc.IdMarca into mcj
                              from mc in mcj.DefaultIfEmpty()
                              join cat in _db.CatalogoCategorias.AsNoTracking() on (pr != null ? pr.IdCategoria : (long?)null) equals cat.IdCategoria into catj
                              from cat in catj.DefaultIfEmpty()
                              orderby d.IdPropuestaDetalle
                              select new PropuestaDetalleDto(
                                  d.IdPropuestaDetalle, d.IdProducto, pr != null ? pr.Descripcion : null, d.Cantidad,
                                  pr != null ? pr.CodigoClave : null,
                                  mc != null ? mc.Descripcion : null,
                                  cat != null ? cat.Descripcion : null))
                             .ToListAsync(ct);

        return new PropuestaDto(
            p.x.IdPropuesta, p.x.IdZonaDestino, p.zn?.Descripcion, p.x.Estatus,
            p.x.Observaciones, p.x.FechaCreacion, p.x.FechaResolucion, detalles);
    }

    private static string MensajeValidacion(CalculoPropuesta.ResultadoValidacion v) => v switch
    {
        CalculoPropuesta.ResultadoValidacion.ZonaInvalida => "La zona destino no es válida.",
        CalculoPropuesta.ResultadoValidacion.SinLineas => "La propuesta debe tener al menos un producto.",
        CalculoPropuesta.ResultadoValidacion.CantidadInvalida => "Hay una línea con producto o cantidad inválida.",
        CalculoPropuesta.ResultadoValidacion.ProductoDuplicado => "Hay productos repetidos en la propuesta.",
        _ => "Datos de propuesta inválidos."
    };
}
