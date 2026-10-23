namespace SistemaCobranzaApp.Domain.Constantes;

/// <summary>
/// IDs fijos del catálogo de estatus de venta.
/// Portado de VistaPagoAgregar.EstatusVentaIds (app WPF).
/// </summary>
public static class EstatusVentaIds
{
    public const long Pendiente = 1;
    public const long Abonando = 2;
    public const long Pagada = 3;
    public const long Cancelada = 4;
}

/// <summary>
/// IDs fijos del catálogo de movimientos.
/// Portado de VistaPagoAgregar.MovimientoIds (app WPF).
/// </summary>
public static class MovimientoIds
{
    public const long Abono = 1;
    public const long Enganche = 2;
    public const long PagoCompleto = 3;
    public const long BonificacionProntoPago = 4;
    public const long Ajuste = 5;
    public const long BonificacionPromocion = 6;
    public const long LiquidacionRenovacion = 7;
}

/// <summary>
/// IDs fijos del catálogo de clasificación de clientes.
/// </summary>
public static class ClasificacionClienteIds
{
    public const long Nuevo = 1;
    public const long Bueno = 2;
    public const long Regular = 3;
    public const long Malo = 4;
    public const long Moroso = 5;
}

/// <summary>
/// Nivel de riesgo de un cliente para la asignación de ventas, derivado de su clasificación.
/// </summary>
public enum NivelRiesgoCliente
{
    /// <summary>NUEVO/BUENO (o sin clasificación): venta permitida, sin advertencia.</summary>
    Ninguno = 0,

    /// <summary>REGULAR: se advierte "tener cuidado", pero la venta sigue permitida.</summary>
    Precaucion = 1,

    /// <summary>MALO/MOROSO: se advierte y se BLOQUEA la asignación de ventas.</summary>
    Bloqueado = 2,
}

/// <summary>
/// Reglas puras (sin base de datos) para evaluar el riesgo de un cliente a partir de su
/// <c>IdClasificacion</c>. Compartidas por las vistas online y offline.
/// </summary>
public static class RiesgoClienteRegla
{
    /// <summary>Determina el nivel de riesgo a partir del id de clasificación del cliente.</summary>
    public static NivelRiesgoCliente Evaluar(long? idClasificacion) => idClasificacion switch
    {
        ClasificacionClienteIds.Regular => NivelRiesgoCliente.Precaucion,
        ClasificacionClienteIds.Malo or ClasificacionClienteIds.Moroso => NivelRiesgoCliente.Bloqueado,
        _ => NivelRiesgoCliente.Ninguno,
    };

    /// <summary>¿La clasificación bloquea la asignación de ventas? (MALO / MOROSO)</summary>
    public static bool BloqueaVenta(long? idClasificacion)
        => Evaluar(idClasificacion) == NivelRiesgoCliente.Bloqueado;

    /// <summary>¿La clasificación debe resaltarse en rojo? (REGULAR / MALO / MOROSO)</summary>
    public static bool Resaltar(long? idClasificacion)
        => Evaluar(idClasificacion) != NivelRiesgoCliente.Ninguno;

    /// <summary>Mensaje informativo para el modal, o null si no hay advertencia que mostrar.</summary>
    public static string? MensajeAdvertencia(long? idClasificacion, string? nombreClasificacion)
    {
        var etiqueta = string.IsNullOrWhiteSpace(nombreClasificacion) ? "sin clasificación" : nombreClasificacion!;
        return Evaluar(idClasificacion) switch
        {
            NivelRiesgoCliente.Precaucion =>
                $"Este cliente pertenece a un segmento complicado (clasificación \"{etiqueta}\"). " +
                "Puedes continuar con la venta, pero hazlo con precaución.",
            NivelRiesgoCliente.Bloqueado =>
                $"Este cliente se encuentra en situación de atraso o bloqueo por deudas con la institución " +
                $"(clasificación \"{etiqueta}\"). No es posible asignarle nuevas ventas.",
            _ => null,
        };
    }
}

/// <summary>
/// Prefijos de folio por tipo de movimiento.
/// Portado de UtilidadesSistema.ObtenerPrefijoMovimiento (app WPF).
/// </summary>
public static class PrefijoMovimiento
{
    public static string Obtener(long idMovimiento) => idMovimiento switch
    {
        MovimientoIds.Abono => "ABO",
        MovimientoIds.Enganche => "ENG",
        MovimientoIds.PagoCompleto => "PAG",
        MovimientoIds.Ajuste => "AJU",
        MovimientoIds.BonificacionProntoPago => "BPAG",
        MovimientoIds.BonificacionPromocion => "BPRO",
        MovimientoIds.LiquidacionRenovacion => "LREN",
        _ => "MOV"
    };
}
