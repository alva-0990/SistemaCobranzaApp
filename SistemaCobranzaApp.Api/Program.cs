using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SistemaCobranzaApp.Api.Seguridad;
using SistemaCobranzaApp.Application.Seguridad;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Infrastructure.Clientes;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Reportes;
using SistemaCobranzaApp.Infrastructure.Seguridad;
using SistemaCobranzaApp.Infrastructure.Sincronizacion;
using SistemaCobranzaApp.Infrastructure.Ventas;
using SistemaCobranzaApp.Shared.Dtos;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Permitir que la app Web lea las respuestas de la API
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirBlazor", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader(); // Crucial para que acepte la cabecera 'Authorization'
    });
});


// --- Base de datos (PostgreSQL / Npgsql) ---
var cadena = builder.Configuration.GetConnectionString("Cobranza")
    ?? "Host=localhost;Port=5432;Database=sistema_cobranza;Username=cobranza;Password=cobranza_dev";

// REGISTRO ESTÁNDAR LIMPIO (Sin DataSourceBuilder ni MapEnum)
builder.Services.AddDbContext<CobranzaDbContext>(opt =>
    opt.UseNpgsql(cadena));

// --- Opciones JWT ---
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection("Jwt").Bind(jwtOptions);
if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    // Clave de desarrollo por defecto. En producción DEBE venir de configuración/secreto.
    jwtOptions.Key = "clave-de-desarrollo-cambiar-en-produccion-min-32-bytes!!";
}
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

// --- Servicios de aplicación ---
builder.Services.AddScoped<ServicioAutenticacion>();
builder.Services.AddScoped<ServicioCobranza>();
builder.Services.AddScoped<ServicioCartera>();
builder.Services.AddScoped<ServicioSincronizacion>();
builder.Services.AddScoped<ServicioCatalogos>();
builder.Services.AddScoped<ServicioBusqueda>();
builder.Services.AddScoped<ServicioClientesAdmin>();
builder.Services.AddScoped<ServicioEstadoCuenta>();
builder.Services.AddScoped<ServicioCancelaciones>();
builder.Services.AddScoped<ServicioZonasAdmin>();
builder.Services.AddScoped<ServicioRutasAdmin>();
builder.Services.AddScoped<ServicioCobradoresAdmin>();
builder.Services.AddScoped<ServicioVendedoresAdmin>();
builder.Services.AddScoped<ServicioVentasAdmin>();
builder.Services.AddScoped<ServicioProductosAdmin>();
builder.Services.AddScoped<ServicioInventario>();
builder.Services.AddScoped<ServicioPropuestas>();
builder.Services.AddScoped<ServicioUsuariosAdmin>();
builder.Services.AddScoped<ServicioRolesAdmin>();
builder.Services.AddScoped<ServicioReportes>();

// --- Autenticación JWT ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Siembra del usuario administrador por defecto (idempotente).
// Tolerante a que la base no esté disponible: la API arranca igualmente.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<CobranzaDbContext>();
        await SeedInicial.SembrarAdminAsync(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SeedInicial");
        logger.LogWarning(ex,
            "No se pudo ejecutar la siembra inicial (¿base de datos no disponible?). " +
            "La API continuará sin sembrar el usuario admin.");
    }
}

app.UseHttpsRedirection();
app.UseCors("PermitirBlazor");
app.UseAuthentication();
app.UseAuthorization();

// --- Endpoint de login ---
app.MapPost("/api/auth/login", async (LoginRequest req, ServicioAutenticacion auth, CancellationToken ct) =>
{
    var resultado = await auth.LoginAsync(req, ct);
    return resultado.Exito
        ? Results.Ok(resultado.Respuesta)
        : Results.Unauthorized();
})
.WithName("Login");

// --- Endpoint de registro de pago (protegido) ---
app.MapPost("/api/pagos", async (RegistrarPagoRequest req, ServicioCobranza cobranza, CancellationToken ct) =>
{
    var resultado = await cobranza.RegistrarPagoAsync(req, ct);
    return resultado.Exito
        ? Results.Ok(resultado.Respuesta)
        : Results.BadRequest(new { error = resultado.Error });
})
.RequireAuthorization()
.WithName("RegistrarPago");

// --- Consulta de cartera del cobrador (protegido) ---
app.MapGet("/api/cartera", async (
    long? idCobrador, long? idZona, long? idRuta, bool? soloConSaldo,
    ServicioCartera cartera, CancellationToken ct) =>
{
    var filtro = new CarteraFiltro(idCobrador, idZona, idRuta, soloConSaldo ?? true);
    var resultado = await cartera.ConsultarAsync(filtro, ct);
    return Results.Ok(resultado);
})
.RequireAuthorization()
.WithName("ConsultarCartera");

// --- Sincronización: subir cola de pagos offline (protegido) ---
app.MapPost("/api/sync/pagos", async (PushPagosRequest req, ServicioSincronizacion sync, CancellationToken ct) =>
{
    var resultado = await sync.PushPagosAsync(req, ct);
    return Results.Ok(resultado);
})
.RequireAuthorization()
.WithName("SyncPushPagos");

// --- Sincronización: bajar cambios incrementales (protegido) ---
app.MapPost("/api/sync/cambios", async (PullCambiosRequest req, ServicioSincronizacion sync, CancellationToken ct) =>
{
    var resultado = await sync.PullCambiosAsync(req, ct);
    return Results.Ok(resultado);
})
.RequireAuthorization()
.WithName("SyncPullCambios");

// ===================== Catálogos de apoyo (protegidos) =====================
app.MapGet("/api/catalogos/zonas", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.ZonasAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/condiciones", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.CondicionesAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/clasificaciones", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.ClasificacionesAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/movimientos", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.MovimientosAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/estatus-venta", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.EstatusVentaAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/rutas", async (ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.RutasAsync(ct))).RequireAuthorization();

app.MapGet("/api/catalogos/vendedores", async (long? idZona, ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.VendedoresAsync(idZona, ct))).RequireAuthorization();

// Cobradores activos, opcionalmente por zona.
app.MapGet("/api/catalogos/cobradores", async (long? idZona, ServicioCatalogos s, CancellationToken ct) =>
    Results.Ok(await s.CobradoresAsync(idZona, ct))).RequireAuthorization();

// ===================== Búsquedas del módulo de cobro (protegidas) =====================

// Búsqueda de clientes por nombre/apellidos.
app.MapGet("/api/clientes/buscar", async (
    string? nombre, string? apPaterno, string? apMaterno,
    ServicioBusqueda s, CancellationToken ct) =>
    Results.Ok(await s.BuscarClientesAsync(new ClienteBusquedaFiltro(nombre, apPaterno, apMaterno), ct)))
    .RequireAuthorization().WithName("BuscarClientes");

// Búsqueda/detalle de venta por Id o CodigoVenta (V#######).
app.MapGet("/api/ventas/buscar", async (string texto, ServicioBusqueda s, CancellationToken ct) =>
{
    var venta = await s.BuscarVentaAsync(texto, ct);
    return venta is null ? Results.NotFound() : Results.Ok(venta);
})
.RequireAuthorization().WithName("BuscarVenta");

// Historial de pagos de una venta.
app.MapGet("/api/ventas/{idVenta:long}/pagos", async (long idVenta, ServicioBusqueda s, CancellationToken ct) =>
    Results.Ok(await s.HistorialPagosAsync(idVenta, ct)))
    .RequireAuthorization().WithName("HistorialPagosVenta");

// ===================== Alta y detalle de ventas (protegidos) =====================

// Crear venta con detalles.
app.MapPost("/api/ventas", async (CrearVentaRequest req, ServicioVentasAdmin s, CancellationToken ct) =>
{
    var r = await s.CrearAsync(req, ct);
    return r.Exito
        ? Results.Created($"/api/ventas/{r.Venta!.IdVenta}", r.Venta)
        : Results.BadRequest(new { error = r.Error });
})
.RequireAuthorization().WithName("CrearVenta");

// Detalle completo de una venta (con líneas).
app.MapGet("/api/ventas/{id:long}", async (long id, ServicioVentasAdmin s, CancellationToken ct) =>
{
    var v = await s.ObtenerAsync(id, ct);
    return v is null ? Results.NotFound() : Results.Ok(v);
})
.RequireAuthorization().WithName("ObtenerVenta");

// ===================== Productos (protegidos) =====================
app.MapGet("/api/productos", async (string? texto, bool? incluirInactivos, int? pagina, int? tamanoPagina,
    ServicioProductosAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(texto, incluirInactivos ?? false, pagina ?? 1, tamanoPagina ?? 50, ct)))
    .RequireAuthorization();
app.MapGet("/api/productos/{id:long}", async (long id, ServicioProductosAdmin s, CancellationToken ct) =>
{ var p = await s.ObtenerAsync(id, ct); return p is null ? Results.NotFound() : Results.Ok(p); }).RequireAuthorization();
app.MapPost("/api/productos", async (CrearProductoRequest req, ServicioProductosAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req, ct); return r.Exito ? Results.Created($"/api/productos/{r.Producto!.IdProducto}", r.Producto) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/productos/{id:long}", async (long id, ActualizarProductoRequest req, ServicioProductosAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req, ct); return r.Exito ? Results.Ok(r.Producto) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/productos/{id:long}", async (long id, ServicioProductosAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Producto) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ===================== Inventario (protegidos) =====================

// Existencias central.
app.MapGet("/api/inventario/central", async (ServicioInventario s, CancellationToken ct) =>
    Results.Ok(await s.ExistenciasCentralAsync(ct))).RequireAuthorization();

// Existencias de una zona.
app.MapGet("/api/inventario/zona/{idZona:long}", async (long idZona, ServicioInventario s, CancellationToken ct) =>
    Results.Ok(await s.ExistenciasZonaAsync(idZona, ct))).RequireAuthorization();

// Entrada/ajuste central.
app.MapPost("/api/inventario/central/movimiento", async (MovimientoCentralRequest req, ServicioInventario s, CancellationToken ct) =>
{ var r = await s.MovimientoCentralAsync(req, ct); return r.Exito ? Results.Ok(r.Movimiento) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();

// Entrada/ajuste zona.
app.MapPost("/api/inventario/zona/movimiento", async (MovimientoZonaRequest req, ServicioInventario s, CancellationToken ct) =>
{ var r = await s.MovimientoZonaAsync(req, ct); return r.Exito ? Results.Ok(r.Movimiento) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();

// Historial de movimientos de un producto.
app.MapGet("/api/inventario/producto/{idProducto:long}/movimientos", async (long idProducto, int? max, ServicioInventario s, CancellationToken ct) =>
    Results.Ok(await s.HistorialAsync(idProducto, max ?? 100, ct))).RequireAuthorization();

// ===================== Propuestas de envío central→zona (protegidos) =====================

// Listar (filtros opcionales estatus/idZona).
app.MapGet("/api/propuestas", async (string? estatus, long? idZona, ServicioPropuestas s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(estatus, idZona, ct))).RequireAuthorization();

// Detalle.
app.MapGet("/api/propuestas/{id:long}", async (long id, ServicioPropuestas s, CancellationToken ct) =>
{ var p = await s.ObtenerAsync(id, ct); return p is null ? Results.NotFound() : Results.Ok(p); }).RequireAuthorization();

// Crear (nace PENDIENTE).
app.MapPost("/api/propuestas", async (CrearPropuestaRequest req, ServicioPropuestas s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req, ct); return r.Exito ? Results.Created($"/api/propuestas/{r.Propuesta!.IdPropuesta}", r.Propuesta) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();

// Confirmar (descuenta central + suma zona, transaccional).
app.MapPost("/api/propuestas/{id:long}/confirmar", async (long id, ResolverPropuestaRequest req, ServicioPropuestas s, CancellationToken ct) =>
{ var r = await s.ConfirmarAsync(id, req.IdUsuarioConfirma, ct); return r.Exito ? Results.Ok(r.Propuesta) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();

// Rechazar (no toca inventario).
app.MapPost("/api/propuestas/{id:long}/rechazar", async (long id, ResolverPropuestaRequest req, ServicioPropuestas s, CancellationToken ct) =>
{ var r = await s.RechazarAsync(id, req.IdUsuarioConfirma, ct); return r.Exito ? Results.Ok(r.Propuesta) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();

// ===================== Administración de usuarios (protegidos) =====================
app.MapGet("/api/admin/usuarios", async (bool? incluirInactivos, ServicioUsuariosAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivos ?? false, ct))).RequireAuthorization();
app.MapGet("/api/admin/usuarios/{id:long}", async (long id, ServicioUsuariosAdmin s, CancellationToken ct) =>
{ var u = await s.ObtenerAsync(id, ct); return u is null ? Results.NotFound() : Results.Ok(u); }).RequireAuthorization();
app.MapPost("/api/admin/usuarios", async (CrearUsuarioRequest req, ServicioUsuariosAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req, ct); return r.Exito ? Results.Created($"/api/admin/usuarios/{r.Usuario!.IdUsuario}", r.Usuario) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/usuarios/{id:long}", async (long id, ActualizarUsuarioRequest req, ServicioUsuariosAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req, ct); return r.Exito ? Results.Ok(r.Usuario) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/usuarios/{id:long}", async (long id, ServicioUsuariosAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Usuario) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ===================== Administración de roles (protegidos) =====================
app.MapGet("/api/admin/roles", async (bool? incluirInactivos, ServicioRolesAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivos ?? false, ct))).RequireAuthorization();
app.MapGet("/api/admin/roles/{id:long}", async (long id, ServicioRolesAdmin s, CancellationToken ct) =>
{ var r = await s.ObtenerAsync(id, ct); return r is null ? Results.NotFound() : Results.Ok(r); }).RequireAuthorization();
app.MapPost("/api/admin/roles", async (CrearRolRequest req, ServicioRolesAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req.Descripcion, ct); return r.Exito ? Results.Created($"/api/admin/roles/{r.Rol!.IdRol}", r.Rol) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/roles/{id:long}", async (long id, ActualizarRolRequest req, ServicioRolesAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req.Descripcion, ct); return r.Exito ? Results.Ok(r.Rol) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/roles/{id:long}", async (long id, ServicioRolesAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Rol) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ===================== Reportes (solo lectura, protegidos) =====================
app.MapGet("/api/reportes/cartera", async (long? idZona, long? idRuta, long? idEstatusVenta, ServicioReportes s, CancellationToken ct) =>
    Results.Ok(await s.CarteraAsync(new ReporteCarteraFiltro(idZona, idRuta, idEstatusVenta), ct))).RequireAuthorization();

app.MapGet("/api/reportes/pagos", async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idCobrador, ServicioReportes s, CancellationToken ct) =>
    Results.Ok(await s.PagosAsync(new ReportePagosFiltro(desde, hasta, idCobrador), ct))).RequireAuthorization();

app.MapGet("/api/reportes/ventas", async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idVendedor, ServicioReportes s, CancellationToken ct) =>
    Results.Ok(await s.VentasAsync(new ReporteVentasFiltro(desde, hasta, idVendedor), ct))).RequireAuthorization();

app.MapGet("/api/reportes/recuperacion", async (DateTimeOffset? desde, DateTimeOffset? hasta, ServicioReportes s, CancellationToken ct) =>
    Results.Ok(await s.RecuperacionAsync(new ReporteRecuperacionFiltro(desde, hasta), ct))).RequireAuthorization();

app.MapGet("/api/reportes/clientes", async (long? idRuta, long? idClasificacion, bool? incluirInactivos, ServicioReportes s, CancellationToken ct) =>
    Results.Ok(await s.ClientesAsync(new ReporteClientesFiltro(idRuta, idClasificacion, incluirInactivos ?? false), ct))).RequireAuthorization();

// ===================== Administración de clientes (protegidos) =====================

// Listado paginado con filtros.
app.MapGet("/api/clientes", async (
    string? texto, long? idRuta, bool? incluirInactivos, int? pagina, int? tamanoPagina,
    ServicioClientesAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(texto, idRuta, incluirInactivos ?? false,
        pagina ?? 1, tamanoPagina ?? 50, ct)))
    .RequireAuthorization().WithName("ListarClientes");

// Obtener por id.
app.MapGet("/api/clientes/{id:long}", async (long id, ServicioClientesAdmin s, CancellationToken ct) =>
{
    var c = await s.ObtenerPorIdAsync(id, ct);
    return c is null ? Results.NotFound() : Results.Ok(c);
})
.RequireAuthorization().WithName("ObtenerCliente");

// Crear.
app.MapPost("/api/clientes", async (CrearClienteRequest req, ServicioClientesAdmin s, CancellationToken ct) =>
{
    var r = await s.CrearAsync(req, ct);
    return r.Exito
        ? Results.Created($"/api/clientes/{r.Cliente!.IdCliente}", r.Cliente)
        : Results.BadRequest(new { error = r.Error });
})
.RequireAuthorization().WithName("CrearCliente");

// Actualizar.
app.MapPut("/api/clientes/{id:long}", async (long id, ActualizarClienteRequest req, ServicioClientesAdmin s, CancellationToken ct) =>
{
    var r = await s.ActualizarAsync(id, req, ct);
    if (r.Exito) return Results.Ok(r.Cliente);
    return r.Error!.Contains("No se encontró")
        ? Results.NotFound(new { error = r.Error })
        : Results.BadRequest(new { error = r.Error });
})
.RequireAuthorization().WithName("ActualizarCliente");

// Baja lógica.
app.MapDelete("/api/clientes/{id:long}", async (long id, ServicioClientesAdmin s, CancellationToken ct) =>
{
    var r = await s.DarDeBajaAsync(id, ct);
    return r.Exito ? Results.Ok(r.Cliente) : Results.NotFound(new { error = r.Error });
})
.RequireAuthorization().WithName("DarDeBajaCliente");

// Estado de cuenta consolidado del cliente.
app.MapGet("/api/clientes/{id:long}/estado-cuenta", async (long id, ServicioEstadoCuenta s, CancellationToken ct) =>
{
    var ec = await s.ObtenerAsync(id, ct);
    return ec is null ? Results.NotFound() : Results.Ok(ec);
})
.RequireAuthorization().WithName("EstadoCuentaCliente");

// Cancelar un pago (revierte su efecto en el saldo de la venta).
app.MapDelete("/api/pagos/{id:long}", async (long id, ServicioCancelaciones s, CancellationToken ct) =>
{
    var r = await s.CancelarPagoAsync(id, ct);
    if (r.Exito) return Results.Ok(r.Respuesta);
    return r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error });
})
.RequireAuthorization().WithName("CancelarPago");

// Cancelar una venta (la marca como CANCELADA).
app.MapPost("/api/ventas/{id:long}/cancelar", async (long id, ServicioCancelaciones s, CancellationToken ct) =>
{
    var r = await s.CancelarVentaAsync(id, ct);
    if (r.Exito) return Results.Ok(r.Respuesta);
    return r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error });
})
.RequireAuthorization().WithName("CancelarVenta");

// ===================== Administración de catálogos (protegidos) =====================

// ---- Zonas ----
app.MapGet("/api/admin/zonas", async (bool? incluirInactivas, ServicioZonasAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivas ?? false, ct))).RequireAuthorization();
app.MapGet("/api/admin/zonas/{id:long}", async (long id, ServicioZonasAdmin s, CancellationToken ct) =>
{ var z = await s.ObtenerAsync(id, ct); return z is null ? Results.NotFound() : Results.Ok(z); }).RequireAuthorization();
app.MapPost("/api/admin/zonas", async (CrearZonaRequest req, ServicioZonasAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req.Descripcion, ct); return r.Exito ? Results.Created($"/api/admin/zonas/{r.Zona!.IdZona}", r.Zona) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/zonas/{id:long}", async (long id, ActualizarZonaRequest req, ServicioZonasAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req.Descripcion, ct); return r.Exito ? Results.Ok(r.Zona) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/zonas/{id:long}", async (long id, ServicioZonasAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Zona) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ---- Rutas ----
app.MapGet("/api/admin/rutas", async (bool? incluirInactivas, long? idZona, ServicioRutasAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivas ?? false, idZona, ct))).RequireAuthorization();
app.MapGet("/api/admin/rutas/{id:long}", async (long id, ServicioRutasAdmin s, CancellationToken ct) =>
{ var x = await s.ObtenerAsync(id, ct); return x is null ? Results.NotFound() : Results.Ok(x); }).RequireAuthorization();
app.MapPost("/api/admin/rutas", async (CrearRutaRequest req, ServicioRutasAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req.Descripcion, req.IdZona, ct); return r.Exito ? Results.Created($"/api/admin/rutas/{r.Ruta!.IdRuta}", r.Ruta) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/rutas/{id:long}", async (long id, ActualizarRutaRequest req, ServicioRutasAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req.Descripcion, req.IdZona, ct); return r.Exito ? Results.Ok(r.Ruta) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/rutas/{id:long}", async (long id, ServicioRutasAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Ruta) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ---- Cobradores ----
app.MapGet("/api/admin/cobradores", async (bool? incluirInactivos, long? idZona, ServicioCobradoresAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivos ?? false, idZona, ct))).RequireAuthorization();
app.MapGet("/api/admin/cobradores/{id:long}", async (long id, ServicioCobradoresAdmin s, CancellationToken ct) =>
{ var x = await s.ObtenerAsync(id, ct); return x is null ? Results.NotFound() : Results.Ok(x); }).RequireAuthorization();
app.MapPost("/api/admin/cobradores", async (CrearCobradorRequest req, ServicioCobradoresAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req.Nombre, req.IdZona, ct); return r.Exito ? Results.Created($"/api/admin/cobradores/{r.Cobrador!.IdCobrador}", r.Cobrador) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/cobradores/{id:long}", async (long id, ActualizarCobradorRequest req, ServicioCobradoresAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req.Nombre, req.IdZona, ct); return r.Exito ? Results.Ok(r.Cobrador) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/cobradores/{id:long}", async (long id, ServicioCobradoresAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Cobrador) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

// ---- Vendedores ----
app.MapGet("/api/admin/vendedores", async (bool? incluirInactivos, long? idZona, ServicioVendedoresAdmin s, CancellationToken ct) =>
    Results.Ok(await s.ListarAsync(incluirInactivos ?? false, idZona, ct))).RequireAuthorization();

app.MapGet("/api/admin/vendedores/{id:long}", async (long id, ServicioVendedoresAdmin s, CancellationToken ct) =>
{ var x = await s.ObtenerAsync(id, ct); return x is null ? Results.NotFound() : Results.Ok(x); }).RequireAuthorization();
app.MapPost("/api/admin/vendedores", async (CrearVendedorRequest req, ServicioVendedoresAdmin s, CancellationToken ct) =>
{ var r = await s.CrearAsync(req.Nombre, req.IdZona, ct); return r.Exito ? Results.Created($"/api/admin/vendedores/{r.Vendedor!.IdVendedor}", r.Vendedor) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapPut("/api/admin/vendedores/{id:long}", async (long id, ActualizarVendedorRequest req, ServicioVendedoresAdmin s, CancellationToken ct) =>
{ var r = await s.ActualizarAsync(id, req.Nombre, req.IdZona, ct); return r.Exito ? Results.Ok(r.Vendedor) : r.NoEncontrado ? Results.NotFound(new { error = r.Error }) : Results.BadRequest(new { error = r.Error }); }).RequireAuthorization();
app.MapDelete("/api/admin/vendedores/{id:long}", async (long id, ServicioVendedoresAdmin s, CancellationToken ct) =>
{ var r = await s.DarDeBajaAsync(id, ct); return r.Exito ? Results.Ok(r.Vendedor) : Results.NotFound(new { error = r.Error }); }).RequireAuthorization();

app.Run();

public partial class Program { }
