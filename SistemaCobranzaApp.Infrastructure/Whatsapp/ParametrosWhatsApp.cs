namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Claves de la tabla parametrossistema que controlan las notificaciones de WhatsApp.
/// Se administran en caliente desde la pantalla de Parámetros (no requieren reinicio).
/// Los secretos de conexión (URL / instancia / apikey) NO están aquí: viven en
/// variables de entorno del contenedor por seguridad.
/// </summary>
public static class ClavesParametrosWhatsApp
{
    public const string EnvioActivo = "WHATSAPP_ENVIO_ACTIVO";       // texto SI/NO
    public const string NotificarVenta = "WHATSAPP_NOTIFICAR_VENTA"; // texto SI/NO
    public const string NotificarPago = "WHATSAPP_NOTIFICAR_PAGO";   // texto SI/NO
    public const string PlantillaVenta = "WHATSAPP_PLANTILLA_VENTA"; // texto plantilla
    public const string PlantillaPago = "WHATSAPP_PLANTILLA_PAGO";   // texto plantilla
    public const string PurgaActiva = "WHATSAPP_PURGA_ACTIVA";       // texto SI/NO
    public const string PurgaDias = "WHATSAPP_PURGA_DIAS";           // numérico (días)
}

/// <summary>
/// Vista inmutable de la configuración de WhatsApp resuelta desde parametrossistema.
/// Las plantillas ya traen un valor por defecto si la clave está vacía o ausente.
/// </summary>
public sealed record ConfiguracionWhatsApp(
    bool EnvioActivo,
    bool NotificarVenta,
    bool NotificarPago,
    string PlantillaVenta,
    string PlantillaPago,
    bool PurgaActiva,
    int PurgaDias);
