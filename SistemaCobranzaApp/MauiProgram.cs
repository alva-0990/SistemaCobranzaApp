using Microsoft.Extensions.Logging;
using SistemaCobranzaApp.Shared.Services;
using SistemaCobranzaApp.Services;

namespace SistemaCobranzaApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        // Add device-specific services used by the SistemaCobranzaApp.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddMauiBlazorWebView();

        // 1. Configurar HttpClient (10.0.2.2 es la IP para apuntar al localhost de tu PC desde el emulador de Android)
        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(DeviceInfo.Platform == DevicePlatform.Android 
                ? "http://10.0.2.2:8081" // Cambia el puerto por el que use tu backend (HTTP es más fácil para desarrollo local en Android)
                : "https://localhost:8081")
        });

        // 2. Registrar el almacenamiento nativo usando el contrato (interfaz) de Shared
        builder.Services.AddSingleton<IServicioAlmacenamiento, AlmacenamientoSeguroMaui>();

        // 3. Registrar el cliente de la API y los ViewModels compartidos
        builder.Services.AddSingleton<ApiCliente>();


#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}