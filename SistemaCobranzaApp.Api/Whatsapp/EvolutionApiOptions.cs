namespace SistemaCobranzaApp.Api.Whatsapp;

/// <summary>
/// Configuración de conexión a Evolution API. Solo contiene SECRETOS de infraestructura,
/// que se enlazan desde la sección "Whatsapp" de la configuración (appsettings / variables
/// de entorno Docker con prefijo Whatsapp__...).
///
/// El comportamiento de negocio (prender/apagar envío, notificar venta/pago, plantillas,
/// purga) NO vive aquí: se administra en caliente desde la tabla parametrossistema.
/// Los ajustes técnicos del worker son constantes en <see cref="Defecto"/>.
/// </summary>
public sealed class EvolutionApiOptions
{
    public const string SeccionConfig = "Whatsapp";

    /// <summary>URL base de Evolution API (ej: https://evolution.midominio.com).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Nombre de la instancia configurada en Evolution API.</summary>
    public string Instancia { get; set; } = string.Empty;

    /// <summary>API key (header 'apikey') de Evolution API.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Secreto compartido que Evolution API envía en el header 'X-Webhook-Secret' al webhook
    /// de respuestas entrantes (BAJA). Si está vacío, el webhook queda deshabilitado.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>¿Hay datos de conexión suficientes para intentar enviar?</summary>
    public bool EstaConfigurado()
        => !string.IsNullOrWhiteSpace(BaseUrl)
           && !string.IsNullOrWhiteSpace(Instancia)
           && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Ajustes técnicos del worker (fijos; rara vez cambian).</summary>
    public static class Defecto
    {
        /// <summary>Reintentos por mensaje antes de marcarlo 'Fallido'.</summary>
        public const int MaxIntentos = 3;

        /// <summary>Mensajes pendientes que toma el worker por ciclo.</summary>
        public const int TamanoLote = 20;

        /// <summary>Pausa entre mensajes consecutivos (anti-baneo: simula ritmo humano).</summary>
        public const int SegundosEntreMensajes = 15;

        /// <summary>Retardo "escribiendo..." que Evolution API aplica antes de enviar (ms).</summary>
        public const int MsPresenciaEscribiendo = 1200;

        /// <summary>Polling de respaldo del worker de envío (segundos).</summary>
        public const int SegundosPollingRespaldo = 30;

        /// <summary>Timeout HTTP por petición a Evolution API (segundos).</summary>
        public const int TimeoutSegundos = 30;

        /// <summary>Intervalo entre ejecuciones del worker de limpieza (horas).</summary>
        public const int HorasIntervaloLimpieza = 12;
    }
}
