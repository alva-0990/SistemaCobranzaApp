using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

if (args is ["--postgres", var basePrueba])
{
    await PruebasPostgres.EjecutarAsync(basePrueba);
    return;
}

static void Comprobar(bool condicion, string caso)
{
    if (!condicion) throw new Exception(caso);
}

var almacen = new AlmacenPrueba();
var servidor = new ServidorPrueba();
var api = new ApiCliente(new HttpClient(servidor) { BaseAddress = new Uri("https://prueba.local") }, almacen);
Comprobar((await api.LoginAsync("cobrador", "prueba")).ok, "Login");
using var local = new ServicioJornadaLocal(api, almacen, new EstadoEmpresaGlobal());
await local.CargarAsync();
await local.DescargarAsync(10);
almacen.ConInternet = false;
Comprobar(local.BuscarClientes("Cliente prueba").Count == 1 && local.BuscarClientes("555").Count == 1
    && local.BuscarClientes("ruta Zona prueba").Count == 1 && local.BuscarClientes("inexistente").Count == 0,
    "Buscar clientes descargados por nombre, teléfono, ruta y zona");
var pantallaPagos = new PantallaPagosPrueba(api, local);
await pantallaPagos.BuscarCliente("Cliente prueba");
Comprobar(pantallaPagos.ClienteActual == 1 && pantallaPagos.VentaActual == 100, "Misma pantalla de pagos: buscar cliente sin internet");
await pantallaPagos.BuscarVenta("V100");
Comprobar(pantallaPagos.ClienteActual == 1 && pantallaPagos.VentaActual == 100, "Misma pantalla de pagos: buscar venta o código QR sin internet");
Console.WriteLine("OK: buscadores de clientes y pantalla de pagos consultan la jornada sin internet.");
var uuid = await local.GuardarPagoAsync(100, 25, "Captura sin señal");
Comprobar(local.TicketPago(uuid).FolioPago.StartsWith("C8-") && local.TicketPago(uuid).MontoPagado == 25,
    "Ticket de pago local con folio de cobrador (C{idCobrador}-...) y monto capturado");
Comprobar(local.Pendientes == 1 && servidor.Pagos.Count == 0, "No debe enviar sin internet");
Comprobar(local.SaldoDisponible(servidor.Descarga.Ventas[0]) == 75, "Descontar el abono local del saldo");
try { await local.GuardarPagoAsync(100, 76, null); throw new Exception("Permitió exceder saldo"); }
catch (InvalidOperationException) { }
try { await local.DescargarAsync(11); throw new Exception("Permitió reemplazar pendientes"); }
catch (InvalidOperationException) { }

// Simular cierre completo: otra instancia carga exclusivamente el contenido persistido.
using var reiniciada = new ServicioJornadaLocal(api, almacen, new EstadoEmpresaGlobal());
await reiniciada.CargarAsync();
Comprobar(reiniciada.TicketPago(uuid).FolioPago == local.TicketPago(uuid).FolioPago, "Ticket disponible después del cierre");
Comprobar(reiniciada.Estado.Operaciones.Single().Uuid == uuid, "El UUID debe sobrevivir al reinicio");
Comprobar(reiniciada.SaldoDisponible(servidor.Descarga.Ventas[0]) == 75, "El saldo local debe sobrevivir al reinicio");

// Central aplicó el pago, pero la respuesta se perdió. Conservar la cola y reintentar el mismo UUID.
servidor.PerderSiguienteRespuesta = true;
almacen.ConInternet = true;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 1 && servidor.Pagos.Count == 1, "Respuesta perdida: conservar pendiente");
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 0 && servidor.Pagos.Count == 1, "El reintento no debe duplicar el pago");
Comprobar(reiniciada.TicketPago(uuid).FolioPago.StartsWith("ABO-"), "Reimpresión con el folio confirmado");
Comprobar(reiniciada.Estado.Operaciones.Single().PagoCentral is not null, "Conservar el folio central");
Comprobar(reiniciada.SaldoDisponible(reiniciada.Estado.Descarga!.Ventas.Single()) == 75, "No descontar dos veces un pago ya descargado");
Console.WriteLine("OK: reinicio, respuesta perdida, idempotencia, saldo y bloqueo de reemplazo de pendientes.");

// Un fallo de disco al capturar nunca debe mostrarse como captura exitosa.
almacen.ConInternet = false;
almacen.FallarEscritura = true;
try { await reiniciada.GuardarPagoAsync(100, 5, null); throw new Exception("Ignoró el error de disco"); }
catch (IOException) { }
Comprobar(reiniciada.Estado.Operaciones.Count == 1, "No publicar capturas no persistidas");
almacen.FallarEscritura = false;

// Fallo al persistir la confirmación: recuperar el mismo pago sin cobrarlo otra vez.
var segundo = await reiniciada.GuardarPagoAsync(100, 5, null);
almacen.ConInternet = true;
almacen.FallarEscritura = true;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 1 && servidor.Pagos.Count == 2, "Fallo de disco durante confirmación");
almacen.FallarEscritura = false;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 0 && servidor.Pagos.Count == 2, "Reintento después de fallo de disco");
Console.WriteLine("OK: errores de disco al capturar y al confirmar no pierden operaciones.");

// Aislamiento por usuario y rol.
await api.CerrarSesionAsync();
await api.LoginAsync("vendedor", "prueba");
await reiniciada.CargarAsync();
Comprobar(reiniciada.Estado.Descarga is null && reiniciada.Estado.Operaciones.Count == 0, "No mostrar datos de otro usuario");
servidor.PrepararVendedor();
await reiniciada.DescargarAsync(10);
almacen.ConInternet = false;
var solicitud = new CrearVentaRequest(1, 9, null, 1, 40, 0, 40, 40, null, null, null, null, null,
    null, [new CrearDetalleVentaRequest(7, 2, 20)], null);
var ventaUuid = await reiniciada.GuardarVentaAsync(solicitud);
Comprobar(reiniciada.InventarioDisponible().Single().Cantidad == 1, "Reservar inventario para la venta offline");
try { await reiniciada.GuardarVentaAsync(solicitud); throw new Exception("Permitió sobreventa local"); }
catch (InvalidOperationException) { }
try { await reiniciada.GuardarPagoAsync(100, 1, null); throw new Exception("Un vendedor pudo cobrar"); }
catch (InvalidOperationException) { }
servidor.PerderSiguienteRespuesta = true;
almacen.ConInternet = true;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 1 && servidor.Ventas.Count == 1, "Conservar venta si no hay confirmación");
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 0 && servidor.Ventas.Count == 1, "No duplicar ventas al reintentar");
Comprobar(reiniciada.InventarioDisponible().Single().Cantidad == 1, "No reservar dos veces después de descargar central");
Console.WriteLine("OK: aislamiento de usuarios, permisos, inventario offline e idempotencia de ventas.");

// Rechazo de central: conservar el registro y el motivo para conciliación, sin descartarlo.
almacen.ConInternet = false;
await reiniciada.GuardarVentaAsync(solicitud with { Detalles = [new CrearDetalleVentaRequest(7, 1, 20)], TotalFinal = 20 });
servidor.RechazarSiguiente = true;
almacen.ConInternet = true;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 1 && reiniciada.Estado.Operaciones.Last().Error is not null, "Conservar conflicto visible");
Console.WriteLine("OK: conflictos de central permanecen visibles y guardados.");

// El mismo usuario fue cambiado de zona en central: no reenviar su cola con el nuevo alcance.
servidor.ZonaLogin = 2;
await api.LoginAsync("vendedor", "prueba");
var antes = servidor.Ventas.Count;
await reiniciada.SincronizarAsync();
Comprobar(reiniciada.Pendientes == 1 && servidor.Ventas.Count == antes && reiniciada.UltimoError!.Contains("zona cambió"),
    "Cambio de zona: conservar pendientes y bloquear envío");
Console.WriteLine("OK: cambio de zona conserva la cola anterior y bloquea su envío.");


// La descarga debe distinguir un endpoint ausente de una jornada vacía o rechazada.
var respuestaJornada = new RespuestaJornadaPrueba();
var apiErrores = new ApiCliente(new HttpClient(respuestaJornada) { BaseAddress = new Uri("https://prueba.local") }, almacen);
var ausente = await apiErrores.DescargarJornadaAsync(0);
Comprobar(ausente.datos is null && ausente.error!.Contains("404") && ausente.error.Contains("/api/jornada/descarga"), "Mostrar endpoint ausente");
respuestaJornada.Estado = HttpStatusCode.OK;
respuestaJornada.Contenido = "{\"codigo\":\"200\",\"mensaje\":\"Descargada\",\"resultado\":null}";
var vacia = await apiErrores.DescargarJornadaAsync(0);
Comprobar(vacia.datos is null && vacia.error!.Contains("sin resultado"), "No aceptar sobres vacíos");
respuestaJornada.Estado = HttpStatusCode.Forbidden;
respuestaJornada.Contenido = "{\"codigo\":\"403\",\"mensaje\":\"El usuario no tiene zona activa.\"}";
var prohibida = await apiErrores.DescargarJornadaAsync(0);
Comprobar(prohibida.error == "El usuario no tiene zona activa.", "Conservar motivo de rechazo del servidor");

// Recuperar y persistir la zona en una sesión antigua que no contenía esos campos.
await almacen.GuardarCadenaAsync("auth_token_cobranza", JsonSerializer.Serialize(new
{
    token = "sesion-antigua", expira = DateTimeOffset.UtcNow.AddHours(1), idUsuario = 3,
    usuario = "vendedor", nombreCompleto = "Vendedor", idRol = 3, rol = "Vendedor"
}));
await apiErrores.CargarSesionDesdeAlmacenamientoAsync();
Comprobar(apiErrores.Sesion?.IdUsuario == 3 && apiErrores.Sesion.IdZona is null, "Leer sesión antigua sin zona");
respuestaJornada.Estado = HttpStatusCode.OK;
respuestaJornada.Contenido = JsonSerializer.Serialize(new ApiRespuesta<RutaJornadaDto>
{
    Codigo = "200", Mensaje = "Zona", Resultado = new(3, 0, "YEHUALTEPEC", 1, 9, null)
}, new JsonSerializerOptions(JsonSerializerDefaults.Web));
Comprobar(await apiErrores.ActualizarZonaJornadaAsync() is null && apiErrores.Sesion?.Zona == "YEHUALTEPEC", "Recuperar zona del servidor");
var restaurada = new ApiCliente(new HttpClient(respuestaJornada), almacen);
await restaurada.CargarSesionDesdeAlmacenamientoAsync();
Comprobar(restaurada.Sesion?.IdZona == 1 && restaurada.Sesion.Zona == "YEHUALTEPEC", "Persistir zona al reiniciar Android");
Console.WriteLine("OK: errores 404/403, resultado ausente y recuperación persistente de la zona.");

// Alta de cliente, venta dependiente y tickets sobreviven a reinicios y respuestas perdidas.
var almacenAltas = new AlmacenPrueba();
var servidorAltas = new ServidorPrueba();
var apiAltas = new ApiCliente(new HttpClient(servidorAltas) { BaseAddress = new Uri("https://prueba.local") }, almacenAltas);
await apiAltas.LoginAsync("vendedor", "prueba");
servidorAltas.PrepararVendedor();
using var altas = new ServicioJornadaLocal(apiAltas, almacenAltas, new EstadoEmpresaGlobal());
await altas.DescargarAsync(10);
almacenAltas.ConInternet = false;
var alta = new CrearClienteRequest("Ana", "Pérez", "López", "555123", null, null, "Domicilio nuevo", null, null,
    null, null, 10, 9, null, null, 3);
almacenAltas.FallarEscritura = true;
try { await altas.GuardarClienteAsync(alta); throw new Exception("Alta confirmada sin disco"); }
catch (IOException) { }
Comprobar(altas.Pendientes == 0, "No publicar clientes sin persistir");
almacenAltas.FallarEscritura = false;
var clienteNuevo = await altas.GuardarClienteAsync(alta);
var referenciaFormulario = SistemaCobranzaApp.Domain.Ventas.CalculoVenta.GenerarCodigoVenta(9);
var ventaNueva = await altas.GuardarVentaAsync(solicitud with { IdCliente = clienteNuevo.IdCliente, CodigoVenta = referenciaFormulario,
    Detalles = [new CrearDetalleVentaRequest(7, 1, 20)], TotalFinal = 20 });
Comprobar(clienteNuevo.IdCliente < 0 && altas.BuscarClientes("Ana Pérez").Count == 1 && altas.VentasDisponibles().Any(v => v.IdCliente == clienteNuevo.IdCliente),
    "Cliente y venta nuevos visibles sin señal");
var ticketVenta = altas.TicketVenta(ventaNueva);
Comprobar(ticketVenta.CodigoVenta == referenciaFormulario, "Conservar en el ticket la referencia mostrada al abrir el formulario");
Comprobar(ticketVenta.Cliente.NombreCompleto == "Ana Pérez López" && ticketVenta.Total == 20 && System.Text.RegularExpressions.Regex.IsMatch(ticketVenta.CodigoVenta, @"^V9-\d{6}-\d{6}-[A-Z0-9]{3}$"), "Ticket de la venta local");
using var altasReinicio = new ServicioJornadaLocal(apiAltas, almacenAltas, new EstadoEmpresaGlobal());
await altasReinicio.CargarAsync();
Comprobar(altasReinicio.TicketVenta(ventaNueva) == ticketVenta || altasReinicio.TicketVenta(ventaNueva).CodigoVenta == ticketVenta.CodigoVenta,
    "Ticket de venta conservado en SQLite");
servidorAltas.PerderSiguienteRespuesta = true;
almacenAltas.ConInternet = true;
await altasReinicio.SincronizarAsync();
Comprobar(altasReinicio.Pendientes == 2 && servidorAltas.Clientes.Count == 1 && servidorAltas.Ventas.Count == 0, "No enviar venta antes de confirmar su cliente");
await altasReinicio.SincronizarAsync();
Comprobar(altasReinicio.Pendientes == 0 && servidorAltas.Clientes.Count == 1 && servidorAltas.Ventas.Count == 1,
    "Reintentos de alta y venta sin duplicados");
Comprobar(servidorAltas.Ventas.Values.Single().IdCliente == servidorAltas.Clientes.Values.Single().IdCliente,
    "Venta enviada con el ID central del nuevo cliente");
Comprobar(servidorAltas.Ventas.Values.Single().CodigoVenta == ticketVenta.CodigoVenta, "Central conserva la referencia impresa offline");
Comprobar(altasReinicio.BuscarClientes("Ana").Count == 1 && altasReinicio.TicketVenta(ventaNueva).Total == 20,
    "Actualizar corte sin duplicar clientes ni perder tickets");
Console.WriteLine("OK: cliente y venta dependiente, tickets persistentes, fallo de disco y respuesta perdida sin duplicados.");

// XLSX real: cantidades numéricas, texto con acentos, varias filas y contenido literal seguro.
var excel = ExportadorExcel.Crear(["Producto", "Total", "Observación"], new object?[][]
{
    ["Refrigerador & estufa", 1250.75m, "=SUM(A1:A2)"],
    ["Artículo con acento", 20m, null]
});
using (var paquete = new System.IO.Compression.ZipArchive(new MemoryStream(excel)))
{
    Comprobar(paquete.GetEntry("[Content_Types].xml") is not null && paquete.GetEntry("xl/workbook.xml") is not null, "Estructura XLSX");
    using var hoja = paquete.GetEntry("xl/worksheets/sheet1.xml")!.Open();
    var doc = System.Xml.Linq.XDocument.Load(hoja);
    System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    var celdas = doc.Descendants(ns + "c").ToDictionary(c => c.Attribute("r")!.Value);
    Comprobar(doc.Descendants(ns + "row").Count() == 3 && celdas["A2"].Value == "Refrigerador & estufa", "Todas las filas y texto del reporte");
    Comprobar(celdas["B2"].Element(ns + "v")!.Value == "1250.75" && celdas["B2"].Attribute("t") is null, "Importes numéricos independientes de la cultura");
    Comprobar(celdas["C2"].Attribute("t")!.Value == "inlineStr" && !doc.Descendants(ns + "f").Any(), "Datos de texto no se ejecutan como fórmulas");
}
Console.WriteLine("OK: archivo XLSX, todas las filas, importes numéricos y texto seguro.");

sealed class RespuestaJornadaPrueba : HttpMessageHandler
{
    public HttpStatusCode Estado { get; set; } = HttpStatusCode.NotFound;
    public string Contenido { get; set; } = "";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(Estado)
        {
            Content = new StringContent(Contenido, System.Text.Encoding.UTF8, "application/json")
        });
}

sealed class AlmacenPrueba : IAlmacenJornada, IServicioAlmacenamiento
{
    private readonly Dictionary<long, string> _jornadas = new();
    private readonly Dictionary<string, string> _sesion = new();
    public bool Disponible => true;
    public bool ConInternet { get; set; } = true;
    public bool FallarEscritura { get; set; }
    public event Action? ConexionRecuperada { add { } remove { } }
    public Task<string?> LeerAsync(long idUsuario) => Task.FromResult(_jornadas.GetValueOrDefault(idUsuario));
    public Task GuardarAsync(long idUsuario, string json)
    {
        if (FallarEscritura) throw new IOException("Disco lleno");
        _jornadas[idUsuario] = json;
        return Task.CompletedTask;
    }
    public Task GuardarCadenaAsync(string clave, string valor) { _sesion[clave] = valor; return Task.CompletedTask; }
    public Task<string?> ObtenerCadenaAsync(string clave) => Task.FromResult(_sesion.GetValueOrDefault(clave));
    public Task EliminarCadenaAsync(string clave) { _sesion.Remove(clave); return Task.CompletedTask; }
}

sealed class ServidorPrueba : HttpMessageHandler
{
    public Dictionary<Guid, RegistrarPagoResponse> Pagos { get; } = new();
    public Dictionary<Guid, VentaDto> Ventas { get; } = new();
    public Dictionary<Guid, ClienteDto> Clientes { get; } = new();
    public bool PerderSiguienteRespuesta { get; set; }
    public bool RechazarSiguiente { get; set; }
    public long ZonaLogin { get; set; } = 1;
    public JornadaDescargaDto Descarga { get; private set; } = CrearDescarga();
    private long _usuario = 4;
    public void PrepararVendedor() => Descarga = CrearDescarga() with
    {
        IdUsuario = 3,
        Asignacion = new(3, 10, "Ruta prueba", 1, 9, null),
        Inventario = [new(1, 7, "P7", "Producto", 3, null)]
    };
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/api/auth/login")
        {
            var login = await request.Content!.ReadFromJsonAsync<LoginRequest>(ct);
            _usuario = login!.Usuario == "vendedor" ? 3 : 4;
            return Ok(new LoginResponse("token-prueba", DateTimeOffset.UtcNow.AddHours(8), _usuario,
                login.Usuario, "Usuario prueba", _usuario, login.Usuario, ZonaLogin, "Zona prueba"));
        }
        if (request.Headers.Authorization?.Scheme != "Bearer") return new(HttpStatusCode.Unauthorized);
        if (path.EndsWith("/descarga")) return Ok(Descarga);
        if (path.EndsWith("/clientes"))
        {
            var req = (await request.Content!.ReadFromJsonAsync<ClienteJornadaRequest>(ct))!;
            var duplicado = Clientes.TryGetValue(req.ClienteUuid, out var cliente);
            if (!duplicado)
            {
                cliente = Descarga.Clientes[0] with { IdCliente = 500 + Clientes.Count, Nombre = req.Cliente.Nombre,
                    ApPaterno = req.Cliente.ApPaterno, ApMaterno = req.Cliente.ApMaterno,
                    NombreCompleto = $"{req.Cliente.Nombre} {req.Cliente.ApPaterno} {req.Cliente.ApMaterno}" };
                Clientes.Add(req.ClienteUuid, cliente);
                Descarga = Descarga with { Clientes = [.. Descarga.Clientes, cliente] };
            }
            PerderRespuesta();
            return Ok(new ResultadoOperacionJornada(true, duplicado, null, Cliente: cliente));
        }
        if (path.EndsWith("/pagos"))
        {
            var pago = (await request.Content!.ReadFromJsonAsync<PagoJornadaRequest>(ct))!.Pago;
            var duplicado = Pagos.TryGetValue(pago.PagoUuid, out var resultado);
            if (!duplicado)
            {
                var v = Descarga.Ventas.Single();
                resultado = new(Pagos.Count + 1, pago.PagoUuid, pago.IdVenta, $"ABO-{Pagos.Count + 1}",
                    Pagos.Count + 1, v.SaldoPendiente, v.SaldoPendiente - pago.MontoAbona, pago.MontoAbona, false);
                Pagos.Add(pago.PagoUuid, resultado);
                Descarga = Descarga with { Ventas = [v with { SaldoPendiente = resultado.SaldoActual }],
                    Pagos = [.. Descarga.Pagos, new(resultado.IdPago, pago.PagoUuid, pago.IdVenta, 1, pago.MontoAbona,
                        resultado.SaldoAnterior, resultado.SaldoActual, resultado.FolioPago, resultado.ConsecutivoPago, 8,
                        pago.FechaPago!.Value, DateTimeOffset.UtcNow)] };
            }
            PerderRespuesta();
            return Ok(new ResultadoOperacionJornada(true, duplicado, null, Pago: resultado));
        }
        if (path.EndsWith("/ventas"))
        {
            if (RechazarSiguiente) { RechazarSiguiente = false; return Ok(new ResultadoOperacionJornada(false, false, "Stock insuficiente en central")); }
            var req = (await request.Content!.ReadFromJsonAsync<VentaJornadaRequest>(ct))!;
            var duplicado = Ventas.TryGetValue(req.VentaUuid, out var venta);
            if (!duplicado)
            {
                venta = VentaBase() with { IdVenta = 200 + Ventas.Count, IdCliente = req.Venta.IdCliente, CodigoVenta = req.Venta.CodigoVenta, TotalFinal = req.Venta.TotalFinal };
                Ventas.Add(req.VentaUuid, venta);
                Descarga = Descarga with { Ventas = [.. Descarga.Ventas, venta],
                    Inventario = Descarga.Inventario.Select(i => i with { Cantidad = i.Cantidad - req.Venta.Detalles.Where(l => l.IdProducto == i.IdProducto).Sum(l => l.Cantidad) }).ToList() };
            }
            PerderRespuesta();
            return Ok(new ResultadoOperacionJornada(true, duplicado, null, venta));
        }
        throw new Exception($"Petición inesperada: {path}");
    }
    private void PerderRespuesta()
    {
        if (PerderSiguienteRespuesta) { PerderSiguienteRespuesta = false; throw new HttpRequestException("Respuesta perdida después del commit"); }
    }
    private static HttpResponseMessage Ok<T>(T resultado) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new ApiRespuesta<T> { Codigo = "200", Mensaje = "OK", Resultado = resultado }) };
    private static VentaDto VentaBase() => new(100, 1, "Cliente prueba", 9, "Vendedor", "V100", DateTimeOffset.UtcNow,
        2, "Crédito", 100, 0, 100, 0, 100, 1, "Semanal", 10, 10, 0, DateTimeOffset.UtcNow.AddDays(7),
        0, 100, null, 1, "Pendiente", null);
    private static JornadaDescargaDto CrearDescarga() => new(4, DateTimeOffset.UtcNow,
        new(4, 10, "Ruta prueba", 1, null, 8),
        [new(1, "Cliente", null, null, "Cliente prueba", "555", null, null, "Domicilio", null, null,
            null, null, 10, "Ruta prueba", 1, 9, "Vendedor", null, null, 1, null, DateTimeOffset.UtcNow)],
        [VentaBase()], new(), new(), new(), [new(1, "Contado", true)], new(), new());
}

sealed class PantallaPagosPrueba : SistemaCobranzaApp.Shared.Pages.PagosOffline
{
    public PantallaPagosPrueba(ApiCliente api, ServicioJornadaLocal jornada) { Api = api; Jornada = jornada; }
    public long? ClienteActual => Cliente?.IdCliente;
    public long? VentaActual => Venta?.IdVenta;
    public Task BuscarCliente(string texto) { BusquedaCliente = texto; return BuscarClienteAsync(); }
    public Task BuscarVenta(string texto) { BusquedaVenta = texto; return BuscarVentaAsync(); }
}
