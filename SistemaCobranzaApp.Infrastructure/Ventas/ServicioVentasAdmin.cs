using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Constantes;
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
    public VentaDto? Venta { get; init; } // 👈 CORREGIDO: Cambiado de VentaCompletaDto a VentaDto

    public static ResultadoVenta Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoVenta Ok(VentaDto v) => new() { Exito = true, Venta = v };
}

public class ServicioVentasAdmin
{
    private readonly CobranzaDbContext _db;
    private readonly ServicioInventario _inventario;

    public ServicioVentasAdmin(CobranzaDbContext db, ServicioInventario inventario)
    {
        _db = db;
        _inventario = inventario;
    }

    public async Task<ResultadoVenta> CrearAsync(CrearVentaRequest req, long? idUsuario = null, CancellationToken ct = default)
    {
        // 1. CORREGIDO: Se eliminó el operador ?? inválido sobre List<T>. Se valida nulidad de forma directa.
        if (req.Detalles is null || !req.Detalles.Any())
            return ResultadoVenta.Fallo("La venta debe tener al menos un producto.");

        var lineas = req.Detalles
            .Select(d => new CalculoVenta.LineaCalculo(d.Cantidad, d.PrecioCobrado))
            .ToList();

        bool esCredito = req.IdFrecuenciaPago.HasValue;
        var validacion = CalculoVenta.Validar(lineas, req.Enganche, req.MontoCuotaFija, esCredito);
        if (validacion != CalculoVenta.ResultadoValidacion.Ok)
            return ResultadoVenta.Fallo(MensajeValidacion(validacion));

        // 2. Validación de Llaves Foráneas y Zona Operativa del Cliente
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

        // 👈 CORREGIDO: Alineado a tu Enum 'Estatus.Activo'
        if (req.IdVendedor is { } vend && !await _db.Vendedores.AnyAsync(
                v => v.IdVendedor == vend && v.Estatus == Estatus.Activo && v.IdZona == idZonaCliente, ct))
            return ResultadoVenta.Fallo($"El vendedor #{vend} no existe o no pertenece a la zona del cliente.");

        // 3. Verificación de existencia de Productos Activos
        var idsProductos = req.Detalles.Select(d => d.IdProducto).Distinct().ToList();
        var productosExisting = await _db.Productos.AsNoTracking()
            .Where(p => idsProductos.Contains(p.IdProducto) && p.Estatus == Estatus.Activo)
            .Select(p => p.IdProducto).ToListAsync(ct);
        
        var faltante = idsProductos.FirstOrDefault(id => !productosExisting.Contains(id));
        if (faltante != 0)
            return ResultadoVenta.Fallo($"El producto #{faltante} no existe o está inactivo.");

        // 4. Validación de Inventario Físico Disponible en la Zona
        var cantidadesPorProducto = req.Detalles
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

        // Pasa la ejecución al bloque de la transacción (Parte 2), propagando el usuario autenticado.
        return await EjecutarTransaccionVentaAsync(req, lineas, idZonaCliente, idUsuario, ct);
    }
    private async Task<ResultadoVenta> EjecutarTransaccionVentaAsync(
        CrearVentaRequest req, 
        List<CalculoVenta.LineaCalculo> lineas, 
        long idZonaCliente, 
        long? idUsuario,
        CancellationToken ct)
    {
        var subtotal = CalculoVenta.CalcularSubtotal(lineas);
        var totalFinal = subtotal - req.DescuentoGlobal;
        var montoFinanciado = totalFinal - req.Enganche;
        var idEstatus = CalculoVenta.DeterminarEstatusInicial(totalFinal, req.Enganche);
        var ahora = DateTimeOffset.UtcNow;

        // Asignación Simétrica del Folio de Venta (Respeta Offline o genera en Central)
        string codigoComercial = string.IsNullOrWhiteSpace(req.CodigoVenta)
            ? CalculoVenta.GenerarCodigoVenta(req.IdVendedor ?? 0) // 👈 CORREGIDO: Evita error si IdVendedor es null
            : req.CodigoVenta.Trim().ToUpper();

        // RenovarVentaAsync controla la transacción de todo el proceso.
        await using var tx = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var venta = new Venta
            {
                IdCliente = req.IdCliente,
                IdVendedor = req.IdVendedor,
                CodigoVenta = codigoComercial,
                VentaUuid = req.VentaUuid,
                FechaCompra = req.FechaCaptura?.ToUniversalTime() ?? ahora,
                FechaUltimoPago = null,
                Subtotal = subtotal,
                DescuentoGlobal = req.DescuentoGlobal,
                TotalFinal = totalFinal,
                Enganche = req.Enganche,
                
                IdFrecuenciaPago = req.IdFrecuenciaPago,
                MontoCuotaFija = req.MontoCuotaFija ?? 0.00m,
                TasaInteres = req.TasaInteres ?? 0.00m,
                FechaPrimerVence = req.FechaPrimerVence,
                NumeroCuotas = 1, 

                TotalAbonado = 0.00m,
                SaldoPendiente = montoFinanciado,
                IdCondicion = req.IdCondicion,
                IdEstatusVenta = idEstatus,
                Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones,
                IdUsuarioRegistro = idUsuario, // Usuario autenticado que registra la venta (null si no viene en el token).
                FechaModificacion = ahora
            };

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync(ct); 

            // A. Registro de Renglones de Artículos Vendidos
            foreach (var d in req.Detalles)
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

            // B. Registro de Planes de Liquidación Promocionales por Meses
            if (req.PlanesPromocionales is not null)
            {
                foreach (var p in req.PlanesPromocionales)
                {
                    _db.VentaPlan.Add(new VentaPlan // 👈 CORREGIDO: Asegura coincidencia con el nombre de tu clase de entidad
                    {
                        IdVenta = venta.IdVenta,
                        NumeroMeses = p.NumeroMeses,
                        MontoLiquidacion = p.MontoLiquidacion,
                        Activo = true
                    });
                }
            }

            // C. 🚀 GENERACIÓN DE LA TABLA DE AMORTIZACIÓN (Cuota pactada/exigida)
            if (montoFinanciado > 0 && venta.IdFrecuenciaPago.HasValue)
            {
                var frecuencia = await _db.CatalogoFrecuenciasPago
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.IdFrecuenciaPago == venta.IdFrecuenciaPago, ct);

                if (frecuencia is null)
                    throw new InvalidOperationException("La frecuencia de pago seleccionada no es válida.");

                int diasCiclo = frecuencia.DiasPeriodo;
                decimal saldoPorAmortizar = montoFinanciado;
                decimal cuotaPactada = venta.MontoCuotaFija;

                int totalCuotasCalculadas = (int)Math.Ceiling(saldoPorAmortizar / cuotaPactada);
                venta.NumeroCuotas = totalCuotasCalculadas;

                DateTimeOffset fechaVencimientoActual = venta.FechaPrimerVence ?? ahora.AddDays(diasCiclo);

                for (int i = 1; i <= totalCuotasCalculadas; i++)
                {
                    decimal montoCuotaFinal = (i == totalCuotasCalculadas) ? saldoPorAmortizar : cuotaPactada;

                    if (saldoPorAmortizar < montoCuotaFinal)
                        montoCuotaFinal = saldoPorAmortizar;

                    _db.VentaCuotas.Add(new VentaCuota // 👈 CORREGIDO: Cambiado de VentaCuotas a VentaCuota (tu entidad real)
                    {
                        IdVenta = venta.IdVenta,
                        NumeroCuota = i,
                        FechaVencimiento = fechaVencimientoActual,
                        MontoCuota = montoCuotaFinal,
                        MontoInteres = 0.00m,
                        SaldoCuota = montoCuotaFinal,
                        EstatusCuota = "PENDIENTE"
                    });

                    saldoPorAmortizar -= montoCuotaFinal;
                    fechaVencimientoActual = fechaVencimientoActual.AddDays(diasCiclo);
                }
            }

            // D. Disminución física del stock en la ruta
            await _db.SaveChangesAsync(ct);
            
            // 👈 CORREGIDO: Se tipó explícitamente el Select para evitar problemas de inferencia en colecciones anónimas
            var stockDescuento = req.Detalles.GroupBy(d => d.IdProducto)
                .Select(g => (IdProducto: g.Key, Cantidad: g.Sum(x => x.Cantidad))).ToList();

            // El id de usuario es opcional en inventariomovimiento y tiene FK a Usuarios: pasar 0 la viola.
            // Se registra el usuario autenticado que originó la venta (null solo si el token no lo trae).
            await _inventario.DescontarPorVentaAsync(venta.IdVenta, idZonaCliente, stockDescuento, idUsuario, ct);

            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            // E. Obtener el DTO de salida unificado (Pasa a la Parte 3)
            var dto = await ObtenerAsync(venta.IdVenta, ct);
            return ResultadoVenta.Ok(dto!);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }
    // ===========================================================================
    // EDICIÓN DE VENTA (solo ventas sin abonos)
    // ===========================================================================
    public async Task<ResultadoVenta> ActualizarAsync(long idVenta, ActualizarVentaRequest req, long? idUsuario = null, CancellationToken ct = default)
    {
        if (req.Detalles is null || !req.Detalles.Any())
            return ResultadoVenta.Fallo("La venta debe tener al menos un producto.");

        var lineas = req.Detalles
            .Select(d => new CalculoVenta.LineaCalculo(d.Cantidad, d.PrecioCobrado))
            .ToList();

        bool esCredito = req.IdFrecuenciaPago.HasValue;
        var validacion = CalculoVenta.Validar(lineas, req.Enganche, req.MontoCuotaFija, esCredito);
        if (validacion != CalculoVenta.ResultadoValidacion.Ok)
            return ResultadoVenta.Fallo(MensajeValidacion(validacion));

        // 1. La venta debe existir y estar en condición de ser editada.
        var venta = await _db.Ventas.FirstOrDefaultAsync(v => v.IdVenta == idVenta, ct);
        if (venta is null)
            return ResultadoVenta.Fallo($"No se encontró la venta #{idVenta}.");

        if (venta.IdEstatusVenta == EstatusVentaIds.Cancelada)
            return ResultadoVenta.Fallo("La venta se encuentra cancelada y no puede editarse.");

        // Regla de negocio: solo se pueden editar ventas sin abonos.
        if (venta.TotalAbonado > 0)
            return ResultadoVenta.Fallo("La venta no puede editarse porque ya tiene abonos registrados.");

        // 2. Zona operativa del cliente (de donde se tomó y se repondrá el inventario).
        var idZonaCliente = await (from c in _db.Clientes.AsNoTracking()
                                   join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta
                                   where c.IdCliente == venta.IdCliente
                                   select (long?)r.IdZona).FirstOrDefaultAsync(ct);

        if (!idZonaCliente.HasValue)
            return ResultadoVenta.Fallo("El cliente no tiene una zona asignada en su ruta. No es posible ajustar inventario.");

        // 3. Validaciones de llaves foráneas.
        if (!await _db.Condiciones.AnyAsync(c => c.IdCondicion == req.IdCondicion, ct))
            return ResultadoVenta.Fallo($"La condición #{req.IdCondicion} no existe.");

        if (req.IdVendedor is { } vend && !await _db.Vendedores.AnyAsync(
                v => v.IdVendedor == vend && v.Estatus == Estatus.Activo && v.IdZona == idZonaCliente, ct))
            return ResultadoVenta.Fallo($"El vendedor #{vend} no existe o no pertenece a la zona del cliente.");

        var idsProductos = req.Detalles.Select(d => d.IdProducto).Distinct().ToList();
        var productosExisting = await _db.Productos.AsNoTracking()
            .Where(p => idsProductos.Contains(p.IdProducto) && p.Estatus == Estatus.Activo)
            .Select(p => p.IdProducto).ToListAsync(ct);

        var faltante = idsProductos.FirstOrDefault(id => !productosExisting.Contains(id));
        if (faltante != 0)
            return ResultadoVenta.Fallo($"El producto #{faltante} no existe o está inactivo.");

        return await EjecutarTransaccionEdicionAsync(venta, req, lineas, idZonaCliente.Value, idUsuario, ct);
    }

    private async Task<ResultadoVenta> EjecutarTransaccionEdicionAsync(
        Venta venta,
        ActualizarVentaRequest req,
        List<CalculoVenta.LineaCalculo> lineas,
        long idZonaCliente,
        long? idUsuario,
        CancellationToken ct)
    {
        var subtotal = CalculoVenta.CalcularSubtotal(lineas);
        var totalFinal = subtotal - req.DescuentoGlobal;
        var montoFinanciado = totalFinal - req.Enganche;
        var idEstatus = CalculoVenta.DeterminarEstatusInicial(totalFinal, req.Enganche);
        var ahora = DateTimeOffset.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // A. Reponer al inventario de la zona las cantidades de los detalles ACTUALES.
            var detallesActuales = await _db.DetallesVenta.AsNoTracking()
                .Where(d => d.IdVenta == venta.IdVenta)
                .Select(d => new { d.IdProducto, d.Cantidad })
                .ToListAsync(ct);

            var reposicion = detallesActuales
                .GroupBy(d => d.IdProducto)
                .Select(g => ((long IdProducto, int Cantidad))(g.Key, g.Sum(x => x.Cantidad)))
                .ToList();

            await _inventario.RestaurarPorVentaAsync(venta.IdVenta, idZonaCliente, reposicion, idUsuario, ct);
            await _db.SaveChangesAsync(ct);

            // B. Validar que haya inventario suficiente para los detalles NUEVOS
            //    (ya con el stock repuesto del paso anterior).
            var cantidadesNuevas = req.Detalles
                .GroupBy(d => d.IdProducto)
                .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
                .ToList();

            var inventarioZona = await _db.InventarioZona.AsNoTracking()
                .Where(i => i.IdZona == idZonaCliente)
                .Select(i => new { i.IdProducto, i.Cantidad })
                .ToListAsync(ct);

            foreach (var linea in cantidadesNuevas)
            {
                var disponible = inventarioZona.FirstOrDefault(i => i.IdProducto == linea.IdProducto)?.Cantidad ?? 0;
                if (disponible < linea.Cantidad)
                    throw new InvalidOperationException(
                        $"Inventario insuficiente en la zona para el producto #{linea.IdProducto}. Disponible: {disponible}.");
            }

            // C. Eliminar los hijos actuales (detalles, cuotas de amortización y planes).
            var detalles = await _db.DetallesVenta.Where(d => d.IdVenta == venta.IdVenta).ToListAsync(ct);
            _db.DetallesVenta.RemoveRange(detalles);

            var cuotas = await _db.VentaCuotas.Where(c => c.IdVenta == venta.IdVenta).ToListAsync(ct);
            _db.VentaCuotas.RemoveRange(cuotas);

            var planes = await _db.VentaPlan.Where(p => p.IdVenta == venta.IdVenta).ToListAsync(ct);
            _db.VentaPlan.RemoveRange(planes);

            await _db.SaveChangesAsync(ct);

            // D. Recalcular la cabecera de la venta.
            venta.IdVendedor = req.IdVendedor;
            venta.Subtotal = subtotal;
            venta.DescuentoGlobal = req.DescuentoGlobal;
            venta.TotalFinal = totalFinal;
            venta.Enganche = req.Enganche;
            venta.IdFrecuenciaPago = req.IdFrecuenciaPago;
            venta.MontoCuotaFija = req.MontoCuotaFija ?? 0.00m;
            venta.TasaInteres = req.TasaInteres ?? 0.00m;
            venta.FechaPrimerVence = req.FechaPrimerVence;
            venta.NumeroCuotas = 1;
            venta.TotalAbonado = 0.00m;
            venta.SaldoPendiente = montoFinanciado;
            venta.IdCondicion = req.IdCondicion;
            venta.IdEstatusVenta = idEstatus;
            venta.Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones;
            venta.FechaModificacion = ahora;

            // E. Insertar los detalles nuevos.
            foreach (var d in req.Detalles)
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

            // F. Insertar los planes promocionales nuevos.
            if (req.PlanesPromocionales is not null)
            {
                foreach (var p in req.PlanesPromocionales)
                {
                    _db.VentaPlan.Add(new VentaPlan
                    {
                        IdVenta = venta.IdVenta,
                        NumeroMeses = p.NumeroMeses,
                        MontoLiquidacion = p.MontoLiquidacion,
                        Activo = true
                    });
                }
            }

            // G. Regenerar la tabla de amortización.
            if (montoFinanciado > 0 && venta.IdFrecuenciaPago.HasValue)
            {
                var frecuencia = await _db.CatalogoFrecuenciasPago
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.IdFrecuenciaPago == venta.IdFrecuenciaPago, ct);

                if (frecuencia is null)
                    throw new InvalidOperationException("La frecuencia de pago seleccionada no es válida.");

                int diasCiclo = frecuencia.DiasPeriodo;
                decimal saldoPorAmortizar = montoFinanciado;
                decimal cuotaPactada = venta.MontoCuotaFija;

                int totalCuotasCalculadas = (int)Math.Ceiling(saldoPorAmortizar / cuotaPactada);
                venta.NumeroCuotas = totalCuotasCalculadas;

                DateTimeOffset fechaVencimientoActual = venta.FechaPrimerVence ?? ahora.AddDays(diasCiclo);

                for (int i = 1; i <= totalCuotasCalculadas; i++)
                {
                    decimal montoCuotaFinal = (i == totalCuotasCalculadas) ? saldoPorAmortizar : cuotaPactada;

                    if (saldoPorAmortizar < montoCuotaFinal)
                        montoCuotaFinal = saldoPorAmortizar;

                    _db.VentaCuotas.Add(new VentaCuota
                    {
                        IdVenta = venta.IdVenta,
                        NumeroCuota = i,
                        FechaVencimiento = fechaVencimientoActual,
                        MontoCuota = montoCuotaFinal,
                        MontoInteres = 0.00m,
                        SaldoCuota = montoCuotaFinal,
                        EstatusCuota = "PENDIENTE"
                    });

                    saldoPorAmortizar -= montoCuotaFinal;
                    fechaVencimientoActual = fechaVencimientoActual.AddDays(diasCiclo);
                }
            }

            await _db.SaveChangesAsync(ct);

            // H. Descontar del inventario de la zona las cantidades de los detalles nuevos.
            var stockDescuento = req.Detalles.GroupBy(d => d.IdProducto)
                .Select(g => (IdProducto: g.Key, Cantidad: g.Sum(x => x.Cantidad))).ToList();

            await _inventario.DescontarPorVentaAsync(venta.IdVenta, idZonaCliente, stockDescuento, idUsuario, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await ObtenerAsync(venta.IdVenta, ct);
            return ResultadoVenta.Ok(dto!);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return ResultadoVenta.Fallo(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<VentaDto?> ObtenerAsync(long idVenta, CancellationToken ct = default)
    {
        // 1. Consultar la Cabecera de la Venta con su Cliente, Estatus y Frecuencia correspondiente
        var fila = await (from v in _db.Ventas.AsNoTracking()
                          where v.IdVenta == idVenta
                          join c in _db.Clientes.AsNoTracking() on v.IdCliente equals c.IdCliente
                          join ev in _db.EstatusVenta.AsNoTracking() on v.IdEstatusVenta equals ev.IdEstatusVenta into evj
                          from ev in evj.DefaultIfEmpty()
                          join fp in _db.CatalogoFrecuenciasPago.AsNoTracking() on v.IdFrecuenciaPago equals fp.IdFrecuenciaPago into fpj
                          from fp in fpj.DefaultIfEmpty()
                          join cond in _db.Condiciones.AsNoTracking() on v.IdCondicion equals cond.IdCondicion into condj
                          from cond in condj.DefaultIfEmpty()
                          join vend in _db.Vendedores.AsNoTracking() on v.IdVendedor equals vend.IdVendedor into vendj
                          from vend in vendj.DefaultIfEmpty()
                          select new { v, c, ev, Frecuencia = fp != null ? fp.Descripcion : null, Condicion = cond != null ? cond.Descripcion : "DESCONOCIDA", Vendedor = vend != null ? vend.Nombre : null }).FirstOrDefaultAsync(ct);

        if (fila is null) return null;

        // 2. Consultar los Renglones de la Mercancía Vendida (Zapatos, Ropa, Electrónica)
        var detalles = await (from d in _db.DetallesVenta.AsNoTracking()
                              where d.IdVenta == idVenta
                              join p in _db.Productos.AsNoTracking() on d.IdProducto equals p.IdProducto into pj
                              from p in pj.DefaultIfEmpty()
                              orderby d.IdDetalle
                              select new DetalleVentaDto(
                                  d.IdDetalle, d.IdVenta, d.IdProducto, p != null ? p.Descripcion : "PRODUCTO DESCONOCIDO",
                                  d.Cantidad, d.PrecioCobrado, d.Importe)).ToListAsync(ct);

        // 3. Consultar la Tabla de Amortización Proyectada (Plan de Cuotas)
        var cuotas = await (from cu in _db.VentaCuotas.AsNoTracking() // 👈 CORREGIDO: Mapea al DbSet de tu DbContext
                            where cu.IdVenta == idVenta
                            orderby cu.NumeroCuota
                            select new VentaCuotaDto(
                                cu.IdCuota, cu.IdVenta, cu.NumeroCuota, cu.FechaVencimiento,
                                cu.MontoCuota, cu.MontoInteres, cu.SaldoCuota, cu.EstatusCuota)).ToListAsync(ct);

        // 4. Consultar las Opciones de Liquidación por Meses (Planes Promocionales)
        var planes = await (from pl in _db.VentaPlan.AsNoTracking()
                            where pl.IdVenta == idVenta && pl.Activo
                            orderby pl.NumeroMeses
                            select new VentaPlanDto(
                                pl.IdPlan, pl.IdVenta, pl.NumeroMeses, pl.MontoLiquidacion, pl.Activo)).ToListAsync(ct);

        var cliente = BusquedaHelper.NombreCompleto(fila.c.Nombre, fila.c.ApPaterno, fila.c.ApMaterno);

        // 5. 🎯 CORREGIDO: Retorna el VentaDto nativo unificado que requiere tu API
        return new VentaDto(
            IdVenta: fila.v.IdVenta,
            IdCliente: fila.v.IdCliente,
            NombreCliente: cliente,
            IdVendedor: fila.v.IdVendedor,
            NombreVendedor: fila.Vendedor,
            CodigoVenta: fila.v.CodigoVenta,
            FechaCompra: fila.v.FechaCompra,
            IdCondicion: fila.v.IdCondicion,
            DescripcionCondicion: fila.Condicion,             Subtotal: fila.v.Subtotal,
            DescuentoGlobal: fila.v.DescuentoGlobal,
            TotalFinal: fila.v.TotalFinal,
            Enganche: fila.v.Enganche,
            MontoFinanciado: fila.v.MontoFinanciado,
            IdFrecuenciaPago: fila.v.IdFrecuenciaPago,
            DescripcionFrecuencia: fila.Frecuencia,
            NumeroCuotas: fila.v.NumeroCuotas,
            MontoCuotaFija: fila.v.MontoCuotaFija,
            TasaInteres: fila.v.TasaInteres,
            FechaPrimerVence: fila.v.FechaPrimerVence,
            TotalAbonado: fila.v.TotalAbonado,
            SaldoPendiente: fila.v.SaldoPendiente,
            FechaUltimoPago: fila.v.FechaUltimoPago,
            IdEstatusVenta: fila.v.IdEstatusVenta,
            DescripcionEstatus: fila.ev?.Descripcion ?? "ACTIVA",
            Observaciones: fila.v.Observaciones,
            Detalles: detalles,
            Cuotas: cuotas,
            PlanesPromocionales: planes
        );
    }

    private static string MensajeValidacion(CalculoVenta.ResultadoValidacion v) => v switch
    {
        CalculoVenta.ResultadoValidacion.SinLineas => "La venta debe tener al menos un producto.",
        CalculoVenta.ResultadoValidacion.LineaInvalida => "Hay una línea con cantidad o precio inválido.",
        CalculoVenta.ResultadoValidacion.EngancheNegativo => "El enganche no puede ser negativo.",
        CalculoVenta.ResultadoValidacion.EngancheExcedeTotal => "El enganche no puede superar el total de la venta.",
        CalculoVenta.ResultadoValidacion.CuotaFijaInvalida => "La cuota fija pactada con el cliente no puede ser negativa ni cero.",
        CalculoVenta.ResultadoValidacion.CuotaFijaExcedeFinanciamiento => "La cuota fija exigida no puede ser mayor al monto neto a financiar.",
        _ => "Datos de venta inválidos."
    };
    
    public async Task<ResultadoVenta> RenovarVentaAsync(long idVentaVieja,
        CrearVentaRequest req, long? idUsuario = null, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Bloquear el origen hasta liquidarlo: evita absorber dos veces la misma deuda.
            var origen = await _db.Ventas.FromSqlInterpolated(
                $"SELECT * FROM venta WHERE idventa = {idVentaVieja} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (origen is null) return ResultadoVenta.Fallo("La venta de origen no existe.");
            if (origen.IdCliente != req.IdCliente) return ResultadoVenta.Fallo("Las cuentas deben pertenecer al mismo cliente.");
            if (origen.SaldoPendiente <= 0 || origen.IdEstatusVenta == EstatusVentaIds.Cancelada)
                return ResultadoVenta.Fallo("La cuenta de origen no tiene saldo disponible para renovar.");
            if (req.SaldoRenovacionEsperado is { } esperado && esperado != origen.SaldoPendiente)
                return ResultadoVenta.Fallo("El saldo de origen cambió en central. Revisa la renovación antes de volver a enviarla.");
            if (!req.IdFrecuenciaPago.HasValue || req.MontoCuotaFija is not > 0
                || req.Subtotal - req.DescuentoGlobal - req.Enganche <= 0)
                return ResultadoVenta.Fallo("La renovación requiere una nueva venta a crédito con saldo financiado.");

            // Request base: el procedimiento añade el saldo de origen una sola vez.
            var alta = await CrearAsync(req, idUsuario, ct);
            if (!alta.Exito || alta.Venta is null) return alta;
            await _db.Database.ExecuteSqlRawAsync(
                "CALL pr_unificar_cuentas_ventas({0}, {1}, {2})",
                new object[] { idVentaVieja, alta.Venta.IdVenta, (object?)idUsuario ?? DBNull.Value }, ct);
            var finalizada = await ObtenerAsync(alta.Venta.IdVenta, ct);
            await tx.CommitAsync(ct);
            return ResultadoVenta.Ok(finalizada!);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            return ResultadoVenta.Fallo("No se pudo renovar la cuenta. No se guardaron cambios.");
        }
    }
}
