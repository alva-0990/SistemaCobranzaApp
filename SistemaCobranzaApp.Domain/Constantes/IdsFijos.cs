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
    public const long Descuento = 4;
    public const long Ajuste = 5;
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
        MovimientoIds.Descuento => "DES",
        MovimientoIds.Ajuste => "AJU",
        _ => "MOV"
    };
}
