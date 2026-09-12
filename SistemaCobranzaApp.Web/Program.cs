using MudBlazor.Services;
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
builder.Services.AddMudServices();
// ==========================================

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(SistemaCobranzaApp.Shared._Imports).Assembly);

app.Run();