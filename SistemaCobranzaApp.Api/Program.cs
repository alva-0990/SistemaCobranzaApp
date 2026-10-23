using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SistemaCobranzaApp.Api.Endpoints;
using SistemaCobranzaApp.Api.Seguridad;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Application.Seguridad;
using SistemaCobranzaApp.Infrastructure.CatalogoComisiones;
using SistemaCobranzaApp.Shared.Logging;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Infrastructure.Clientes;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Empresas;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Infrastructure.Pagos;
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

// La conexión usa la misma zona de negocio que las pantallas y filtros.
cadena = new Npgsql.NpgsqlConnectionStringBuilder(cadena)
{
    Timezone = SistemaCobranzaApp.Domain.Comun.FechaProyecto.IdZona
}.ConnectionString;

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

// --- Logging estructurado (traza por petición) ---
builder.Services.AddScoped<ContextoTraza>();
builder.Services.AddScoped<IServicioLog, ServicioLog>();

builder.Services.AddScoped<ServicioCobranza>();
builder.Services.AddScoped<ServicioCartera>();
builder.Services.AddScoped<ServicioSincronizacion>();
builder.Services.AddScoped<SistemaCobranzaApp.Infrastructure.Sincronizacion.ServicioJornada>();
builder.Services.AddScoped<ServicioCatalogos>();
builder.Services.AddScoped<ServicioBusqueda>();
builder.Services.AddScoped<ServicioBusquedaClientes>();
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
builder.Services.AddScoped<ServicioCatalogosProductosAdmin>();
builder.Services.AddScoped<ServicioUsuariosAdmin>();
builder.Services.AddScoped<ServicioRolesAdmin>();
builder.Services.AddScoped<ServicioReportes>();
builder.Services.AddScoped<ServicioClasificacionClienteAdmin>();
builder.Services.AddScoped<ServicioCondicionCreditoAdmin>();
builder.Services.AddScoped<ServicioEstatusVentaAdmin>();
builder.Services.AddScoped<ServicioMesesAdmin>();
builder.Services.AddScoped<ServicioMovimientosAdmin>();
builder.Services.AddScoped<ServicioProductosAdmin>();
builder.Services.AddScoped<ServicioFrecuenciasPagoAdmin>();
builder.Services.AddScoped<ServicioParametrosAdmin>();
builder.Services.AddScoped<ServicioEmpresaAdmin>();
builder.Services.AddScoped<ServicioCategoriasGastoAdmin>();
builder.Services.AddScoped<ServicioCuentasGastoAdmin>();
builder.Services.AddScoped<ServicioGastosAdmin>();
builder.Services.AddScoped<ServicioGastosPagosAdmin>();
builder.Services.AddScoped<ServicioMovimientosCuentaGasto>();
builder.Services.AddScoped<ServicioGastosComprobantesAdmin>();
builder.Services.AddScoped<ServicioReportesGasto>();
builder.Services.AddScoped<ServicioPagosAdmin>();
builder.Services.AddScoped<ServicioCatalogoComisionesAdmin>();
builder.Services.AddScoped<ServicioPagosComisionesAdmin>();

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

// En desarrollo la API se expone por HTTP plano (http://0.0.0.0:8081) para que
// dispositivos físicos en la LAN puedan conectarse. Redirigir a HTTPS sin un
// puerto HTTPS configurado rompe esas peticiones, así que solo redirigimos
// fuera de desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("PermitirBlazor");
app.UseAuthentication();
app.UseAuthorization();

// Asigna un id_traza por petición y lo propaga vía ContextoTraza (scoped).
app.UseMiddleware<MiddlewareTraza>();

// --- 🚀 REGISTRO AUTOMÁTICO DE TODOS LOS ENDPOINTS ---
var tipoModulo = typeof(IEndpointModule);
var modulos = typeof(Program).Assembly.GetTypes()
    .Where(p => tipoModulo.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

foreach (var modulo in modulos)
{
    var instancia = (IEndpointModule)Activator.CreateInstance(modulo)!;
    instancia.MapEndpoints(app);
}

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


// ===================== Búsquedas del módulo de cobro (protegidas) =====================

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



app.Run();

public partial class Program
{
}
