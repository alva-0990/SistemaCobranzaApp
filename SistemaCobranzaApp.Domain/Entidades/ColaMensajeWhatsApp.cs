namespace SistemaCobranzaApp.Domain.Entidades;

/// <summary>
/// Estados posibles de un mensaje en la cola de WhatsApp.
/// Los valores de texto coinciden con el CHECK de la tabla cola_mensajes_whatsapp.
/// </summary>
public static class EstadoMensajeWhatsApp
{
    public const string Pendiente = "Pendiente";
    public const string Procesando = "Procesando";
    public const string Enviado = "Enviado";
    public const string Fallido = "Fallido";
}

/// <summary>
/// Origen que generó el mensaje (trazabilidad). Coincide con el CHECK de tipo_origen.
/// </summary>
public static class TipoOrigenWhatsApp
{
    public const string Venta = "VENTA";
    public const string Pago = "PAGO";
    public const string Recordatorio = "RECORDATORIO";
}

/// <summary>
/// Fila de la cola de mensajes de WhatsApp (tabla de paso cola_mensajes_whatsapp).
/// El flujo de ventas/pagos encola aquí un mensaje dentro de su propia transacción;
/// un BackgroundService lo drena y lo envía a Evolution API con reintentos (máx 3).
/// La tabla se purga periódicamente, por eso id_venta / id_pago son referencias
/// informativas sin llave foránea dura.
/// </summary>
public class ColaMensajeWhatsApp
{
    public long Id { get; set; }

    /// <summary>Número con código de país (ej: '521234567890').</summary>
    public string Telefono { get; set; } = string.Empty;

    /// <summary>Texto dinámico del mensaje (venta o pago).</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>'Pendiente' | 'Procesando' | 'Enviado' | 'Fallido'.</summary>
    public string Estado { get; set; } = EstadoMensajeWhatsApp.Pendiente;

    /// <summary>Contador de reintentos automáticos (máximo 3).</summary>
    public int Intentos { get; set; }

    /// <summary>'VENTA' | 'PAGO' (informativo).</summary>
    public string? TipoOrigen { get; set; }

    /// <summary>
    /// Plantilla usada: 'VENTA_CREDITO' | 'VENTA_CONTADO' | 'PAGO'. Con la API oficial determina
    /// qué plantilla aprobada de Meta se envía. Null en filas anteriores a la parametrización.
    /// </summary>
    public string? Plantilla { get; set; }

    /// <summary>
    /// Variables posicionales del mensaje en JSON (arreglo de textos: {{1}}, {{2}}, ...), usadas por la
    /// API oficial. El proveedor se decide al enviar, por eso se guardan junto con el texto ya armado.
    /// </summary>
    public string? Variables { get; set; }

    /// <summary>Referencia informativa a la venta origen (sin FK dura).</summary>
    public long? IdVenta { get; set; }

    /// <summary>Referencia informativa al pago origen (sin FK dura).</summary>
    public long? IdPago { get; set; }

    /// <summary>Detalle técnico del último fallo de envío.</summary>
    public string? ErrorLog { get; set; }

    public DateTimeOffset FechaCreacion { get; set; }

    public DateTimeOffset? FechaEnvio { get; set; }
}
