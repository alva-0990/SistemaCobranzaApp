using SistemaCobranzaApp.Domain.Comun;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Infrastructure.Clientes;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Sincronizacion;
using SistemaCobranzaApp.Infrastructure.Ventas;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

internal static class PruebasPostgres
{
    public static async Task EjecutarAsync(string nombreBase)
    {
        if (!nombreBase.StartsWith("codex_jornada_", StringComparison.Ordinal))
            throw new InvalidOperationException("Las pruebas solo escriben en bases temporales codex_jornada_*.");
        var conexion = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = 5432, Database = nombreBase,
            Username = "cobranza", Password = "cobranza_dev", Timezone = "America/Mexico_City"
        };
        await using var db = new CobranzaDbContext(new DbContextOptionsBuilder<CobranzaDbContext>().UseNpgsql(conexion.ConnectionString).Options);
        var log = new LogPrueba();
        var inv = new ServicioInventario(db);
        var ventas = new ServicioVentasAdmin(db, inv);
        var s = new ServicioJornada(db, new ServicioClientesAdmin(db), new ServicioProductosAdmin(db, log),
            ventas, new ServicioCobranza(db), inv, new ServicioCondicionCreditoAdmin(db, log),
            new ServicioFrecuenciasPagoAdmin(db, log), new ServicioMesesAdmin(db, log));
        var zona = new Zona { Descripcion = "Zona integración offline" };
        var zonaAjena = new Zona { Descripcion = "Zona ajena" };
        db.AddRange(zona, zonaAjena); await db.SaveChangesAsync();
        var ruta = new Ruta { Descripcion = "Ruta autorizada", IdZona = zona.IdZona };
        var otraRuta = new Ruta { Descripcion = "Ruta ajena", IdZona = zonaAjena.IdZona };
        var segundaRuta = new Ruta { Descripcion = "Segunda ruta zona", IdZona = zona.IdZona };
        var vendedor = new Vendedor { Nombre = "Vendedor pruebas", IdZona = zona.IdZona };
        var cobrador = new Cobrador { Nombre = "Cobrador pruebas", IdZona = zona.IdZona };
        var usuarioV = new Usuario { IdRol = 3, Usuario_ = "vend-offline", NombreCompleto = "Vendedor", PasswordHash = SistemaCobranzaApp.Infrastructure.Seguridad.PasswordHelper.CrearHash("Offline123!"), FechaAlta = DateTimeOffset.UtcNow };
        var usuarioC = new Usuario { IdRol = 4, Usuario_ = "cob-offline", NombreCompleto = "Cobrador", PasswordHash = SistemaCobranzaApp.Infrastructure.Seguridad.PasswordHelper.CrearHash("Offline123!"), FechaAlta = DateTimeOffset.UtcNow };
        var categoria = new CatalogoCategoria { Descripcion = "Categoría pruebas" };
        var marca = new CatalogoMarca { Descripcion = "Marca pruebas" };
        db.AddRange(ruta, otraRuta, segundaRuta, vendedor, cobrador, usuarioV, usuarioC, categoria, marca);
        await db.SaveChangesAsync();
        vendedor.IdUsuario = usuarioV.IdUsuario;
        cobrador.IdUsuario = usuarioC.IdUsuario;
        await db.SaveChangesAsync();
        var producto = new Producto { CodigoClave = "OFF-TEST", Descripcion = "Producto prueba", Costo = 5, PrecioSugerido = 100,
            PrecioMinimo = 10, IdCategoria = categoria.IdCategoria, IdMarca = marca.IdMarca };
        db.Add(producto); await db.SaveChangesAsync();
        db.Add(new InventarioZona { IdZona = zona.IdZona, IdProducto = producto.IdProducto, Cantidad = 10, FechaActualizacion = DateTimeOffset.UtcNow });
        var clientes = Enumerable.Range(0, 205).Select(n => new Cliente { Nombre = $"Cliente {n}", Direccion = "Domicilio",
            TelefonoPrincipal = "555", IdRuta = n == 204 ? segundaRuta.IdRuta : ruta.IdRuta, IdVendedor = vendedor.IdVendedor, IdUsuarioRegistro = usuarioV.IdUsuario,
            FechaAlta = DateTimeOffset.UtcNow, FechaModificacion = DateTimeOffset.UtcNow }).ToList();
        db.AddRange(clientes);
        var ajeno = new Cliente { Nombre = "Cliente ajeno", Direccion = "Otra ruta", TelefonoPrincipal = "555", IdRuta = otraRuta.IdRuta, IdUsuarioRegistro = usuarioV.IdUsuario,
            FechaAlta = DateTimeOffset.UtcNow, FechaModificacion = DateTimeOffset.UtcNow };
        db.Add(ajeno); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var ct = CancellationToken.None;
        var descarga = await s.DescargarAsync(usuarioV.IdUsuario, ruta.IdRuta, ct);
        Verificar(descarga.Clientes.Count == 205 && descarga.Clientes.Any(c => c.IdRuta == segundaRuta.IdRuta) && descarga.EsZonaCompleta && descarga.Rutas.Count == 2, "Descarga completa, más de una página, todas las rutas de la zona");
        await Prohibido(() => s.DescargarAsync(usuarioV.IdUsuario, otraRuta.IdRuta, ct));

        var fechaCaptura = DateTimeOffset.UtcNow.AddHours(-2);
        var uuid = Guid.NewGuid();
        var req = new CrearVentaRequest(clientes[0].IdCliente, vendedor.IdVendedor, null, 1, 100, 0, 100, 0,
            1, 10, 10, 0, DateTimeOffset.UtcNow.AddDays(7), null,
            [new(producto.IdProducto, 1, 100)], null);
        var venta = await s.VenderAsync(usuarioV.IdUsuario, ruta.IdRuta, new(uuid, fechaCaptura, req), ct);
        Verificar(venta.Aplicado && venta.Venta is not null, "Aplicar venta offline real");
        db.ChangeTracker.Clear();
        var duplicada = await s.VenderAsync(usuarioV.IdUsuario, ruta.IdRuta, new(uuid, fechaCaptura, req), ct);
        Verificar(duplicada.Aplicado && duplicada.Duplicado && await db.Ventas.CountAsync(v => v.VentaUuid == uuid) == 1, "Idempotencia de venta en PostgreSQL");
        Verificar(await db.InventarioZona.Where(i => i.IdProducto == producto.IdProducto && i.IdZona == zona.IdZona).Select(i => i.Cantidad).SingleAsync() == 9,
            "Descontar existencias una sola vez");
        Verificar(Math.Abs((venta.Venta!.FechaCompra - fechaCaptura).TotalMilliseconds) < 1, "Conservar fecha capturada offline");
        await Prohibido(() => s.VenderAsync(usuarioC.IdUsuario, ruta.IdRuta, new(Guid.NewGuid(), fechaCaptura, req), ct));
        await Prohibido(() => s.VenderAsync(usuarioV.IdUsuario, ruta.IdRuta, new(Guid.NewGuid(), fechaCaptura, req with { IdCliente = ajeno.IdCliente }), ct));

        var pagoUuid = Guid.NewGuid();
        var pago = new RegistrarPagoRequest(pagoUuid, venta.Venta.IdVenta, 20, cobrador.IdCobrador, 1, usuarioC.IdUsuario, "Offline", fechaCaptura);
        db.ChangeTracker.Clear();
        var cobrado = await s.CobrarAsync(usuarioC.IdUsuario, new(ruta.IdRuta, pago), ct);
        Verificar(cobrado.Aplicado && cobrado.Pago is not null, "Aplicar abono offline real");
        db.ChangeTracker.Clear();
        var pagoDuplicado = await s.CobrarAsync(usuarioC.IdUsuario, new(ruta.IdRuta, pago), ct);
        Verificar(pagoDuplicado.Aplicado && pagoDuplicado.Duplicado && await db.Pagos.CountAsync(p => p.PagoUuid == pagoUuid) == 1,
            "Idempotencia de abonos en PostgreSQL");
        var corte = await s.DescargarAsync(usuarioC.IdUsuario, ruta.IdRuta, ct);
        Verificar(corte.Pagos.Any(p => p.PagoUuid == pagoUuid) && corte.Ventas.Single(v => v.IdVenta == venta.Venta.IdVenta).SaldoPendiente == 80,
            "La siguiente descarga incluye saldo y pagos confirmados");
        var historial = corte.Pagos.Single(p => p.PagoUuid == pagoUuid);
        Verificar(historial.IdMovimiento == 1 && historial.Cobrador == "Cobrador pruebas" && historial.Observaciones == "Offline",
            "La pantalla offline recibe movimiento, cobrador y observaciones del historial");
        await Prohibido(() => s.CobrarAsync(usuarioV.IdUsuario, new(ruta.IdRuta, pago with { PagoUuid = Guid.NewGuid() }), ct));

        // Dos dispositivos intentan cobrar más que el saldo combinado: solo uno puede confirmar.
        var pagosConcurrentes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var paralelo = new CobranzaDbContext(new DbContextOptionsBuilder<CobranzaDbContext>().UseNpgsql(conexion.ConnectionString).Options);
            return await new ServicioCobranza(paralelo).RegistrarPagoAsync(pago with { PagoUuid = Guid.NewGuid(), MontoAbona = 60 });
        }));
        Verificar(pagosConcurrentes.Count(p => p.Exito) == 1, "No sobrecobrar con dos dispositivos concurrentes");
        var uuidConcurrente = Guid.NewGuid();
        var reintentos = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var paralelo = new CobranzaDbContext(new DbContextOptionsBuilder<CobranzaDbContext>().UseNpgsql(conexion.ConnectionString).Options);
            return await new ServicioCobranza(paralelo).RegistrarPagoAsync(pago with { PagoUuid = uuidConcurrente, MontoAbona = 10 });
        }));
        Verificar(reintentos.All(p => p.Exito) && await db.Pagos.CountAsync(p => p.PagoUuid == uuidConcurrente) == 1,
            "Dos reintentos simultáneos del mismo pago deben confirmar un solo registro");
        var ventasConcurrentes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var paralelo = new CobranzaDbContext(new DbContextOptionsBuilder<CobranzaDbContext>().UseNpgsql(conexion.ConnectionString).Options);
            try
            {
                var r = await new ServicioVentasAdmin(paralelo, new ServicioInventario(paralelo)).CrearAsync(req with
                {
                    VentaUuid = Guid.NewGuid(), CodigoVenta = $"PRUEBA-{Guid.NewGuid():N}",
                    Subtotal = 600, TotalFinal = 600, Detalles = [new(producto.IdProducto, 6, 100)]
                }, usuarioV.IdUsuario);
                return r.Exito;
            }
            catch (InvalidOperationException) { return false; }
        }));
        Verificar(ventasConcurrentes.Count(ok => ok) == 1 && await db.InventarioZona.Where(i => i.IdProducto == producto.IdProducto && i.IdZona == zona.IdZona)
            .Select(i => i.Cantidad).SingleAsync() == 3, "No sobrevendender inventario con dos dispositivos concurrentes");
        // Altas offline: conservar fecha, zona y UUID; una respuesta perdida no duplica al cliente.
        var altaUuid = Guid.NewGuid();
        var altaReq = new CrearClienteRequest("Nuevo offline", "Pérez", null, "555789", null, null, "Domicilio nuevo", null, null,
            null, null, segundaRuta.IdRuta, vendedor.IdVendedor, null, "Captura local", usuarioV.IdUsuario);
        var alta = await s.CrearClienteAsync(usuarioV.IdUsuario, new(altaUuid, fechaCaptura, altaReq), ct);
        Verificar(alta.Aplicado && alta.Cliente is not null && alta.Cliente.IdRuta == segundaRuta.IdRuta
            && alta.Cliente.FechaAlta == fechaCaptura, "Crear cliente de la zona y conservar fecha de captura");
        db.ChangeTracker.Clear();
        var altaDuplicada = await s.CrearClienteAsync(usuarioV.IdUsuario, new(altaUuid, fechaCaptura, altaReq), ct);
        Verificar(altaDuplicada.Aplicado && altaDuplicada.Duplicado && altaDuplicada.Cliente?.IdCliente == alta.Cliente!.IdCliente
            && await db.Clientes.CountAsync(c => c.ClienteUuid == altaUuid) == 1, "Idempotencia del alta de cliente");
        await Prohibido(() => s.CrearClienteAsync(usuarioV.IdUsuario, new(Guid.NewGuid(), fechaCaptura,
            altaReq with { IdRuta = otraRuta.IdRuta }), ct));
        await Prohibido(() => s.CrearClienteAsync(usuarioC.IdUsuario, new(Guid.NewGuid(), fechaCaptura, altaReq), ct));
        db.ChangeTracker.Clear();
        var ventaNuevo = await s.VenderAsync(usuarioV.IdUsuario, 0, new(Guid.NewGuid(), fechaCaptura,
            req with { IdCliente = alta.Cliente!.IdCliente }), ct);
        Verificar(ventaNuevo.Aplicado && ventaNuevo.Venta?.IdCliente == alta.Cliente.IdCliente,
            "Venta del cliente recién sincronizado");
        var corteAltas = await s.DescargarAsync(usuarioV.IdUsuario, 0, ct);
        Verificar(corteAltas.Clientes.Any(c => c.IdCliente == alta.Cliente.IdCliente)
            && corteAltas.Ventas.Any(v => v.IdVenta == ventaNuevo.Venta!.IdVenta), "Descarga de nuevas altas confirmadas");
        Console.WriteLine("OK PostgreSQL: alta de cliente idempotente, fecha original, permisos por zona y venta dependiente.");
        // Reportes: varios artículos conservan una fila por venta/pago y no multiplican totales.
        var segundoProducto = new Producto { CodigoClave = "REP-ART2", Descripcion = "Artículo adicional & prueba", Costo = 5,
            PrecioSugerido = 15, PrecioMinimo = 10, IdCategoria = categoria.IdCategoria, IdMarca = marca.IdMarca };
        db.Add(segundoProducto); await db.SaveChangesAsync(ct);
        db.Add(new InventarioZona { IdZona = zona.IdZona, IdProducto = segundoProducto.IdProducto, Cantidad = 5, FechaActualizacion = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        var paraReporte = await ventas.CrearAsync(req with { CodigoVenta = null, Subtotal = 145, TotalFinal = 145,
            Detalles = [new(producto.IdProducto, 1, 100), new(segundoProducto.IdProducto, 3, 15)] }, usuarioV.IdUsuario, ct);
        Verificar(paraReporte.Exito && paraReporte.Venta is not null, "Venta de varios productos para reportes");
        var idReporte = paraReporte.Venta!.IdVenta;
        var abonoReporte = await s.CobrarAsync(usuarioC.IdUsuario, new(0, pago with { PagoUuid = Guid.NewGuid(), IdVenta = idReporte, MontoAbona = 10 }), ct);
        Verificar(abonoReporte.Aplicado, "Abono para reporte de pagos");
        var reportes = new SistemaCobranzaApp.Infrastructure.Reportes.ServicioReportes(db);
        var carteraReporte = await reportes.CarteraAsync(new(IdZona: zona.IdZona), ct);
        var filaCartera = carteraReporte.Filas.Single(v => v.IdVenta == idReporte);
        Verificar(filaCartera.TotalFinal == 145 && filaCartera.Productos.Contains("Producto prueba (x1)")
            && filaCartera.Productos.Contains("Artículo adicional & prueba (x3)"), "Cartera: productos sin duplicar venta ni total");
        var ventasReporte = await reportes.VentasAsync(new(FechaProyecto.Ahora.AddDays(-2), FechaProyecto.Ahora.AddDays(1), IdZona: zona.IdZona), ct);
        Verificar(ventasReporte.Filas.Single(v => v.IdVenta == idReporte).Productos == filaCartera.Productos,
            "Ventas: productos de la operación");
        var pagosReporte = await reportes.PagosAsync(new(FechaProyecto.Ahora.AddDays(-2), FechaProyecto.Ahora.AddDays(1), IdZona: zona.IdZona), ct);
        var filaPago = pagosReporte.Filas.Single(p => p.IdVenta == idReporte);
        Verificar(filaPago.TotalVenta == 145 && filaPago.MontoAbona == 10 && filaPago.Productos == filaCartera.Productos,
            "Pagos: total de venta y productos sin multiplicar abonos");
        Console.WriteLine("OK PostgreSQL: productos y total en cartera, ventas y pagos, sin duplicar filas financieras.");
        await db.Cobradores.Where(c => c.IdCobrador == cobrador.IdCobrador)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.Estatus, SistemaCobranzaApp.Domain.Enums.Estatus.Inactivo));
        await Prohibido(() => s.DescargarAsync(usuarioC.IdUsuario, ruta.IdRuta, ct));
        Console.WriteLine("OK PostgreSQL: descarga de 205 clientes, rutas y roles, venta y abono reales, fechas, UUID, inventario y revocación.");
        Console.WriteLine("OK PostgreSQL: concurrencia de cobros, reintentos simultáneos e inventario de ventas.");
    }

    private static void Verificar(bool condicion, string mensaje)
    { if (!condicion) throw new Exception(mensaje); }
    private static async Task Prohibido<T>(Func<Task<T>> accion)
    {
        try { await accion(); }
        catch (UnauthorizedAccessException) { return; }
        throw new Exception("Se permitió operar fuera del rol o ruta autorizada.");
    }
    private sealed class LogPrueba : IServicioLog
    {
        public void Debug(string p, string m = "", object? d = null, string metodo = "", int linea = 0) { }
        public void Info(string p, string m = "", object? d = null, string metodo = "", int linea = 0) { }
        public void Warn(string p, string m = "", object? d = null, string metodo = "", int linea = 0) { }
        public void Error(string p, string m = "", object? d = null, string metodo = "", int linea = 0) { }
        public void Error(string p, Exception ex, object? d = null, string metodo = "", int linea = 0) => Console.Error.WriteLine(ex.Message);
    }
}
