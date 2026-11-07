namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Claves de la tabla parametrossistema que controlan las notificaciones de WhatsApp.
/// Se administran en caliente desde la pantalla de Parámetros (no requieren reinicio).
/// Los secretos de conexión (URL / instancia / apikey / token de Meta) NO están aquí: viven en
/// variables de entorno del contenedor por seguridad.
/// </summary>
public static class ClavesParametrosWhatsApp
{
    public const string EnvioActivo = "WHATSAPP_ENVIO_ACTIVO";       // texto SI/NO
    public const string NotificarVenta = "WHATSAPP_NOTIFICAR_VENTA"; // texto SI/NO
    public const string NotificarPago = "WHATSAPP_NOTIFICAR_PAGO";   // texto SI/NO

    /// <summary>Flujo de envío: 'EVOLUTION' (API externa ya implementada) u 'OFICIAL' (WhatsApp Cloud API de Meta).</summary>
    public const string Proveedor = "WHATSAPP_PROVEEDOR";

    // El mensaje de Evolution es el comprobante completo definido en MensajesWhatsApp (no se parametriza).
    // El mensaje CORTO del flujo OFICIAL vive en la plantilla aprobada en Meta; aquí solo se guarda su nombre.
    public const string OficialPlantillaVentaCredito = "WHATSAPP_OFICIAL_PLANTILLA_VENTA_CREDITO";
    public const string OficialPlantillaVentaContado = "WHATSAPP_OFICIAL_PLANTILLA_VENTA_CONTADO";
    public const string OficialPlantillaPago = "WHATSAPP_OFICIAL_PLANTILLA_PAGO";
    public const string OficialIdioma = "WHATSAPP_OFICIAL_IDIOMA"; // código de idioma de la plantilla (ej. es_MX)

    public const string PurgaActiva = "WHATSAPP_PURGA_ACTIVA";       // texto SI/NO
    public const string PurgaDias = "WHATSAPP_PURGA_DIAS";           // numérico (días)

    // ---- Recordatorios de cobranza (envío manual confirmado por el usuario) ----
    public const string NotificarRecordatorio = "WHATSAPP_NOTIFICAR_RECORDATORIO"; // texto SI/NO
    public const string RecordatorioEtapa1DiasHasta = "RECORDATORIO_ETAPA1_DIAS_HASTA"; // numérico
    public const string RecordatorioEtapa2DiasHasta = "RECORDATORIO_ETAPA2_DIAS_HASTA"; // numérico
    public const string RecordatorioMsgEtapa1 = "RECORDATORIO_MSG_ETAPA1"; // texto (plantilla)
    public const string RecordatorioMsgEtapa2 = "RECORDATORIO_MSG_ETAPA2"; // texto (plantilla)
    public const string RecordatorioMsgEtapa3 = "RECORDATORIO_MSG_ETAPA3"; // texto (plantilla)
}

/// <summary>Valores válidos de <see cref="ClavesParametrosWhatsApp.Proveedor"/>.</summary>
public static class ProveedorWhatsApp
{
    public const string Evolution = "EVOLUTION";
    public const string Oficial = "OFICIAL";
}

/// <summary>Plantillas lógicas del mensaje (también guardadas en la cola para el flujo oficial).</summary>
public static class PlantillaWhatsApp
{
    public const string VentaCredito = "VENTA_CREDITO";
    public const string VentaContado = "VENTA_CONTADO";
    public const string Pago = "PAGO";
    public const string Recordatorio = "RECORDATORIO";
}

/// <summary>
/// Vista inmutable de la configuración de WhatsApp resuelta desde parametrossistema.
/// Las plantillas ya traen un valor por defecto si la clave está vacía o ausente.
/// </summary>
public sealed record ConfiguracionWhatsApp(
    bool EnvioActivo,
    bool NotificarVenta,
    bool NotificarPago,
    string Proveedor,
    string OficialPlantillaVentaCredito,
    string OficialPlantillaVentaContado,
    string OficialPlantillaPago,
    string OficialIdioma,
    bool PurgaActiva,
    int PurgaDias)
{
    /// <summary>Nombre de la plantilla aprobada en Meta que corresponde a la plantilla lógica indicada.</summary>
    public string NombrePlantillaOficial(string? plantilla) => plantilla switch
    {
        PlantillaWhatsApp.VentaCredito => OficialPlantillaVentaCredito,
        PlantillaWhatsApp.VentaContado => OficialPlantillaVentaContado,
        PlantillaWhatsApp.Pago => OficialPlantillaPago,
        _ => string.Empty
    };
}
