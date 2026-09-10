using SistemaCobranzaApp.Domain.Constantes;

namespace SistemaCobranzaApp.Domain.Cobranza;

/// <summary>
/// Reglas de negocio puras de cobranza, portadas de VistaPagoAgregar
/// (app WPF). No dependen de base de datos, por lo que son testeables
/// de forma aislada.
/// </summary>
public static class CalculoCobranza
{
    /// <summary>
    /// Saldo pendiente antes de aplicar un nuevo abono.
    /// saldoAnterior = totalFinal - (enganche + totalAbonos), con piso en 0.
    /// </summary>
    public static decimal CalcularSaldoAnterior(decimal totalFinal, decimal enganche, decimal totalAbonos)
    {
        var saldo = totalFinal - (enganche + totalAbonos);
        return saldo < 0 ? 0m : saldo;
    }

    /// <summary>
    /// Saldo pendiente tras aplicar el nuevo abono, con piso en 0.
    /// </summary>
    public static decimal CalcularNuevoSaldo(decimal totalFinal, decimal enganche, decimal nuevoTotalAbonos)
    {
        var saldo = totalFinal - (enganche + nuevoTotalAbonos);
        return saldo < 0 ? 0m : saldo;
    }

    /// <summary>
    /// Determina el estatus de venta resultante según el nuevo saldo.
    /// </summary>
    public static long DeterminarEstatusVenta(decimal nuevoSaldo) =>
        nuevoSaldo <= 0 ? EstatusVentaIds.Pagada : EstatusVentaIds.Abonando;

    /// <summary>
    /// Formatea el folio: {PREFIJO}-{consecutivo:D3}.
    /// Portado de UtilidadesSistema.GenerarFolioMovimiento.
    /// </summary>
    public static string FormatearFolio(long idMovimiento, int consecutivo)
    {
        var prefijo = PrefijoMovimiento.Obtener(idMovimiento);
        return $"{prefijo}-{consecutivo:D3}";
    }

    public enum ResultadoValidacion
    {
        Ok,
        YaLiquidada,
        AbonoExcedeSaldo,
        MontoInvalido
    }

    /// <summary>
    /// Valida el abono contra el saldo anterior, replicando las reglas de
    /// VistaPagoAgregar: saldo debe ser > 0 y el abono no puede exceder el saldo.
    /// </summary>
    public static ResultadoValidacion ValidarAbono(decimal nuevoAbono, decimal saldoAnterior)
    {
        if (nuevoAbono <= 0)
            return ResultadoValidacion.MontoInvalido;

        if (saldoAnterior <= 0)
            return ResultadoValidacion.YaLiquidada;

        if (nuevoAbono > saldoAnterior)
            return ResultadoValidacion.AbonoExcedeSaldo;

        return ResultadoValidacion.Ok;
    }

    /// <summary>
    /// Nuevo total de abonos tras revertir (cancelar) un pago: totalAbonos - montoRevertido,
    /// con piso en 0 (no puede quedar negativo).
    /// </summary>
    public static decimal RevertirTotalAbonos(decimal totalAbonos, decimal montoRevertido)
    {
        var nuevo = totalAbonos - montoRevertido;
        return nuevo < 0 ? 0m : nuevo;
    }
}
