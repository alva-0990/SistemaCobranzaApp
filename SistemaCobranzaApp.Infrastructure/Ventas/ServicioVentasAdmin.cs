using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Ventas;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Ventas;

public class ResultadoVenta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public VentaCompletaDto? Venta { get; init; }

    public static ResultadoVenta Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoVenta Ok(VentaCompletaDto v) => new() { Exito = true, Venta = v };
}

/// <summary>
/// Alta transaccional de ventas, portada de VentanaNuevaVenta (app WPF):
/// calcula totales/estatus, inserta la venta, asigna CodigoVenta = V#######
/// (que requiere el Id ya generado) e inserta los detalles, todo en una
/// transacción.
/// </summary>
public class ServicioVentasAdmin
{
    private readonly CobranzaDbContext _db;
    private readonly ServicioInventario _inventario;

    public ServicioVentasAdmin(CobranzaDbContext db, ServicioInventario inventario)
    {
        _db = db;
        _inventario = inventario;
    }

    public async Task<ResultadoVenta> CrearAsync(CrearVentaRequest req, CancellationToken ct = default)
    {
        // Validación pura de las líneas y el enganche.
        var lineas = (req.Detalles ?? Array.Empty<CrearDetalleVentaRequest>())
            .Select(d => new CalculoVenta.LineaCalculo(d.Cantidad, d.PrecioCobrado))
            .ToList();

        var validacion = CalculoVenta.Validar(lineas, req.Enganche, req.PagoRequerido);
        if (validacion != CalculoVenta.ResultadoValidacion.Ok)
            return ResultadoVenta.Fallo(MensajeValidacion(validacion));

        // Validación de FKs contra la base y zona operativa del cliente.
        var clienteZona = await (from c in _db.Clientes.AsNoTracking()
                                 where c.IdCliente == req.IdCliente
                                 join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                                 from r in rj.DefaultIfEmpty()
                                 select new { c.IdCliente, IdZona = r != null ? r.IdZona : null })
            .FirstOrDefaultAsync(ct);

        if (clienteZona is null)
            return ResultadoVenta.Fallo($"El cliente #{req.IdCliente} no existe.");

        if (clienteZona.IdZona is not { } idZonaCliente)
            return ResultadoVenta.Fallo("El cliente no tiene una zona asignada en su ruta. No es posible tomar inventario.");

        if (!await _db.Condiciones.AnyAsync(c => c.IdCondicion == req.IdCondicion, ct))
            return ResultadoVenta.Fallo($"La condición #{req.IdCondicion} no existe.");

        if (req.IdVendedor is { } vend && !await _db.Vendedores.AnyAsync(
                v => v.IdVendedor == vend && v.Estatus == Estatus.Activo && v.IdZona == idZonaCliente, ct))
            return ResultadoVenta.Fallo($"El vendedor #{vend} no existe o no pertenece a la zona del cliente.");

        var idsProductos = req.Detalles!.Select(d => d.IdProducto).Distinct().ToList();
        var productosExistentes = await _db.Productos.AsNoTracking()
            .Where(p => idsProductos.Contains(p.IdProducto) && p.Estatus == Estatus.Activo)
            .Select(p => p.IdProducto).ToListAsync(ct);
        var faltante = idsProductos.FirstOrDefault(id => !productosExistentes.Contains(id));
        if (faltante != 0)
            return ResultadoVenta.Fallo($"El producto #{faltante} no existe o está inactivo.");

        var cantidadesPorProducto = req.Detalles!
            .GroupBy(d => d.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();

        var inventarioZona = await _db.InventarioZona.AsNoTracking()
            .Where(i => i.IdZona == idZonaCliente && idsProductos.Contains(i.IdProducto))
            .Select(i => new { i.IdProducto, i.Cantidad })
            .ToListAsync(ct);

        foreach (var linea in cantidadesPorProducto)
        {
            var disponible = inventarioZona.FirstOrDefault(i => i.IdProducto == linea.IdProducto)?.Cantidad ?? 0;
            if (disponible < linea.Cantidad)
                return ResultadoVenta.Fallo($"Inventario insuficiente en la zona para el producto #{linea.IdProducto}. Disponible: {disponible}.");
        }

        // Totales y estatus.
        var subtotal = CalculoVenta.CalcularSubtotal(lineas);
        var totalFinal = subtotal; // sin descuento global en el alta (igual que la app WPF)
        var saldoPagado = CalculoVenta.SaldoPagadoInicial(req.Enganche);
        var saldoPendiente = CalculoVenta.SaldoPendienteInicial(totalFinal, req.Enganche);
        var idEstatus = CalculoVenta.DeterminarEstatusInicial(totalFinal, req.Enganche);
        var ahora = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var venta = new Venta
            {
                IdCliente = req.IdCliente,
                IdVendedor = req.IdVendedor,
                FechaCompra = ahora,
                FechaUltimoPago = null,
                Subtotal = subtotal,
                PagoRequerido = req.PagoRequerido,
                Enganche = req.Enganche,
                TotalAbonos = 0m,
                SaldoPagado = saldoPagado,
                SaldoPendiente = saldoPendiente,
                SaldoPagadoDesc = 0m,
                TotalFinal = totalFinal,
                IdCondicion = req.IdCondicion,
                IdEstatusVenta = idEstatus,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = req.IdUsuarioRegistro,
                FechaModificacion = ahora
            };

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync(ct); // genera IdVenta

            // CodigoVenta = V####### (requiere el Id ya asignado).
            venta.CodigoVenta = CalculoVenta.FormatearCodigoVenta(venta.IdVenta);

            foreach (var d in req.Detalles!)
            {
                _db.DetallesVenta.Add(new DetalleVenta
                {
                    IdVenta = venta.IdVenta,
                    IdProducto = d.IdProducto,
                    Cantidad = d.Cantidad,
                    PrecioCobrado = d.PrecioCobrado,
                    Importe = d.Cantidad * d.PrecioCobrado
                });
            }

            await _db.SaveChangesAsync(ct);
            await _inventario.DescontarPorVentaAsync(
                venta.IdVenta,
                idZonaCliente,
                cantidadesPorProducto.Select(x => (x.IdProducto, x.Cantidad)).ToList(),
                req.IdUsuarioRegistro,
                ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await ObtenerAsync(venta.IdVenta, ct);
            return ResultadoVenta.Ok(dto!);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<VentaCompletaDto?> ObtenerAsync(long idVenta, CancellationToken ct = default)
    {
        var fila = await (from v in _db.Ventas.AsNoTracking()
                          where v.IdVenta == idVenta
                          join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                          join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                          from ev in evj.DefaultIfEmpty()
                          select new { v, c, ev }).FirstOrDefaultAsync(ct);

        if (fila is null) return null;

        var detalles = await (from d in _db.DetallesVenta.AsNoTracking()
                              where d.IdVenta == idVenta
                              join p in _db.Productos.AsNoTracking() on d.IdProducto equals p.IdProducto into pj
                              from p in pj.DefaultIfEmpty()
                              orderby d.IdDetalle
                              select new DetalleVentaDto(
                                  d.IdDetalle, d.IdProducto, p != null ? p.Descripcion : null,
                                  d.Cantidad, d.PrecioCobrado, d.Importe)).ToListAsync(ct);

        var cliente = BusquedaHelper.NombreCompleto(fila.c.Nombre, fila.c.ApPaterno, fila.c.ApMaterno);

        return new VentaCompletaDto(
            fila.v.IdVenta, fila.v.CodigoVenta, fila.v.IdCliente, cliente, fila.v.IdVendedor,
            fila.v.IdCondicion, fila.v.Subtotal, fila.v.Enganche, fila.v.TotalFinal,
            fila.v.SaldoPagado, fila.v.SaldoPendiente, fila.v.IdEstatusVenta, fila.ev?.Descripcion,
            fila.v.PagoRequerido, fila.v.Observaciones, fila.v.FechaCompra, detalles);
    }

    private static string MensajeValidacion(CalculoVenta.ResultadoValidacion v) => v switch
    {
        CalculoVenta.ResultadoValidacion.SinLineas => "La venta debe tener al menos un producto.",
        CalculoVenta.ResultadoValidacion.LineaInvalida => "Hay una línea con cantidad o precio inválido.",
        CalculoVenta.ResultadoValidacion.EngancheNegativo => "El enganche no puede ser negativo.",
        CalculoVenta.ResultadoValidacion.EngancheExcedeTotal => "El enganche no puede superar el total de la venta.",
        CalculoVenta.ResultadoValidacion.PagoRequeridoNegativo => "El pago requerido no puede ser negativo.",
        _ => "Datos de venta inválidos."
    };
}
