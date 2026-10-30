using MudBlazor.Services;
using Microsoft.AspNetCore.HttpOverrides;
using SistemaCobranzaApp.Web.Components;
using SistemaCobranzaApp.Shared.Services;
using SistemaCobranzaApp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the SistemaCobranzaApp.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// ==========================================
// 🔥 NUEVOS REGISTROS REQUERIDOS AGREGADOS:
// ==========================================

// 1. Registrar HttpClient apuntando a tu API (Ajusta la URL y puerto si es necesario)
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7193") 
});

// 2. Registrar el almacenamiento Web usando el contrato compartido
builder.Services.AddScoped<IServicioAlmacenamiento, AlmacenamientoWeb>();

// 3. Registrar el cliente de la API (Esto resolverá el error en Routes.razor)
builder.Services.AddScoped<ApiCliente>();
builder.Services.AddScoped<IAlmacenJornada, AlmacenJornadaNoDisponible>();
builder.Services.AddScoped<ServicioJornadaLocal>();
builder.Services.AddMudServices();

// 4. Registrar la impresora de tickets (stub Web) y el servicio de impresión
builder.Services.AddScoped<IImpresoraTicket, ImpresoraTicketWeb>();
builder.Services.AddScoped<ServicioImpresionTicket>();
builder.Services.AddScoped<IPermisosCamara, PermisosCamaraWeb>();
builder.Services.AddScoped<EstadoEmpresaGlobal>();
builder.Services.AddScoped<IEnlaceExterno, EnlaceExternoWeb>();// ==========================================

builder.Services.AddScoped<IDescargaArchivo, DescargaArchivoWeb>();

// Detrás de Cloudflare Tunnel el tráfico llega como HTTP a Kestrel, pero el cliente usó HTTPS.
// Confiamos en las cabeceras X-Forwarded-* para que la app sepa el esquema/host reales y no
// intente redirigir a HTTPS (lo que provocaría un bucle de redirección).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Cloudflare es el proxy de confianza; en este despliegue no filtramos por red/proxy conocido.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Debe ir al inicio del pipeline para que el resto respete el esquema/host originales.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// NOTA: no usamos UseHttpsRedirection() porque Cloudflare Tunnel ya entrega HTTPS al cliente
// y reenvía HTTP a Kestrel; redirigir aquí causaría un bucle. Cloudflare fuerza HTTPS de borde.

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(SistemaCobranzaApp.Shared._Imports).Assembly);

app.Run();