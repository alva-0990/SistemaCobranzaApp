namespace SistemaCobranzaApp.Api.Whatsapp;

/// <summary>
/// Configuración de conexión a la API OFICIAL de WhatsApp (WhatsApp Cloud API de Meta).
/// Solo contiene SECRETOS / datos de infraestructura, enlazados desde la sección "WhatsappOficial"
/// de la configuración (appsettings / variables de entorno con prefijo WhatsappOficial__...).
///
/// El flujo activo (EVOLUTION u OFICIAL), los interruptores y los nombres de las plantillas aprobadas
/// se administran en caliente desde la tabla parametrossistema.
/// </summary>
public sealed class WhatsAppOficialOptions
{
    public const string SeccionConfig = "WhatsappOficial";
    public const string HttpClientNombre = "WhatsAppOficial";

    /// <summary>URL base de la Graph API de Meta.</summary>
    public string BaseUrl { get; set; } = "https://graph.facebook.com";

    /// <summary>Versión de la Graph API (ej: v21.0).</summary>
    public string VersionApi { get; set; } = "v21.0";

    /// <summary>Id del número de teléfono de WhatsApp Business (Phone number ID).</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Token de acceso permanente (usuario del sistema) con permiso whatsapp_business_messaging.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>¿Hay datos suficientes para intentar enviar por la API oficial?</summary>
    public bool EstaConfigurado()
        => !string.IsNullOrWhiteSpace(BaseUrl)
           && !string.IsNullOrWhiteSpace(PhoneNumberId)
           && !string.IsNullOrWhiteSpace(AccessToken);
}
