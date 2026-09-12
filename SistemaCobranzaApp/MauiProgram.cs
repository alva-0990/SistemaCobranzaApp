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

        // 1. CORREGIDO: Creamos el manejador nativo para saltar el error TLS -1200 en el emulador
        #if ANDROID
        var handler = new Xamarin.Android.Net.AndroidMessageHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            // Confiamos ciegamente en el servidor local de desarrollo de tu PC
            if (message.RequestUri?.Host == "10.0.2.2" || message.RequestUri?.Host == "localhost") 
                return true;
                
            return errors == System.Net.Security.SslPolicyErrors.None;
        };
        #else
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        #endif

        // 2. CORREGIDO: Inyectamos el HttpClient forzando HTTPS limpio en ambos entornos
// Inyección de HttpClient en tu MauiProgram.cs
        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(DeviceInfo.Platform == DevicePlatform.Android 
                ? "http://10.0.2.2:8081" // <-- Cambiado a HTTP plano para hacer match exacto
                : "http://localhost:8081")
        });
        

        // 3. Registrar el almacenamiento nativo usando el contrato (interfaz) de Shared
        builder.Services.AddSingleton<IServicioAlmacenamiento, AlmacenamientoSeguroMaui>();

        // 4. Registrar el cliente de la API (El "back del front" compartido)
        builder.Services.AddSingleton<ApiCliente>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
