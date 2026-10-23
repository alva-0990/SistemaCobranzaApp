using System.Text;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using SistemaCobranzaApp.Shared.Services;
using SistemaCobranzaApp.Services;

// 1. CORREGIDO: El using de Android debe ser condicional
#if ANDROID
using SistemaCobranzaApp.Servicios;
#endif

namespace SistemaCobranzaApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        // Registra las code pages heredadas (p.ej. CP437) que las impresoras
        // térmicas ESC/POS requieren. En .NET moderno no vienen por defecto;
        // sin esto, Encoding.GetEncoding(437) lanza
        // "No data is available for encoding 437".
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Add device-specific services used by the SistemaCobranzaApp.Shared project
        builder.Services.AddSingleton<IFormFactor, FormFactor>();

        builder.Services.AddMauiBlazorWebView();

        // MudBlazor
        builder.Services.AddMudServices();

        // 2. CORREGIDO: Separación limpia de Handlers por plataforma
#if ANDROID
        var handler = new Xamarin.Android.Net.AndroidMessageHandler();

        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) =>
            {
                if (message.RequestUri?.Host == "10.0.2.2" ||
                    message.RequestUri?.Host == "localhost")
                {
                    return true;
                }

                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            
        // Inyectamos la impresora nativa solo en Android
        builder.Services.AddSingleton<IImpresoraTicket, ImpresoraTicketAndroid>();
        // Permiso de cámara en runtime (para el escaneo de QR).
        builder.Services.AddSingleton<IPermisosCamara, PermisosCamaraServicio>();
#else
        var handler = new HttpClientHandler();

        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) => true;

        // Permiso de cámara: implementación permisiva en plataformas MAUI no-Android.
        builder.Services.AddSingleton<IPermisosCamara, SistemaCobranzaApp.Services.PermisosCamaraPermisivo>();
#endif

        // 3. CORREGIDO: El servicio global se registra para todas las plataformas fuera del #if anterior
        builder.Services.AddSingleton<ServicioImpresionTicket>();

        // 4. Inyección de HttpClient
        //
        // URL base de la API según dónde se ejecute la app:
        //   - Emulador Android : "http://10.0.2.2:8081"   (alias del localhost del host)
        //   - Dispositivo FÍSICO Android : "http://<IP-de-tu-PC-en-la-red>:8081"
        //       (10.0.2.2 NO existe en un teléfono real; debe apuntar a la IP LAN de la máquina
        //        donde corre la API, p.ej. 192.168.3.17, en la MISMA red Wi-Fi)
        //   - Windows/Mac (escritorio) : "http://localhost:8081"
        //
        // Producción: la API se expone por Cloudflare Tunnel en el dominio propio.
        // Para pruebas locales con dispositivo físico, reemplaza por la IP LAN de tu máquina
        // (p. ej. "http://192.168.3.17:8081") o un túnel temporal de desarrollo.
        const string ApiBaseUrlDispositivoFisico = "https://api.cobragil.com";
        const string ApiBaseUrlEmuladorAndroid   = "http://10.0.2.2:8081";
        const string ApiBaseUrlEscritorio         = "http://localhost:8081";

        var apiBaseUrl = DeviceInfo.Platform == DevicePlatform.Android
            ? (DeviceInfo.DeviceType == DeviceType.Virtual
                ? ApiBaseUrlEmuladorAndroid       // emulador
                : ApiBaseUrlDispositivoFisico)    // teléfono/tablet físico
            : ApiBaseUrlEscritorio;

        builder.Services.AddSingleton(sp => new HttpClient(handler)
        {
            BaseAddress = new Uri(apiBaseUrl)
        });
        

        // Registrar el almacenamiento nativo usando el contrato (interfaz) de Shared
        builder.Services.AddSingleton<IServicioAlmacenamiento, AlmacenamientoSeguroMaui>();

        // Registrar el cliente de la API (El "back del front" compartido)
        builder.Services.AddSingleton<ApiCliente>();
#if ANDROID
        builder.Services.AddSingleton<IAlmacenJornada, AlmacenJornadaAndroid>();
#else
        builder.Services.AddSingleton<IAlmacenJornada, AlmacenJornadaNoDisponible>();
#endif
        builder.Services.AddSingleton<ServicioJornadaLocal>();
        builder.Services.AddSingleton<IDescargaArchivo, SistemaCobranzaApp.Services.DescargaArchivoMaui>();

        // Estado global de la empresa (lo consumen Login, layouts y menús). Debe registrarse
        // también en MAUI: sin esto, inyectar EstadoEmpresaGlobal en Login.razor lanza una
        // excepción de DI en Android y el login nunca completa, por lo que el menú no aparece.
        builder.Services.AddSingleton<EstadoEmpresaGlobal>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
