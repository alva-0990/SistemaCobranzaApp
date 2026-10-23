namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Modo de registro de pago elegido en el diálogo.</summary>
public enum ModoPagoDialogo
{
    /// <summary>Abono normal en efectivo (IdMovimiento = 1).</summary>
    Abono,

    /// <summary>
    /// Liquidación anticipada por un plan de VentaPlan: el trigger detecta la marca
    /// "LIQUIDACION PRONTO PAGO" y condona la diferencia automáticamente.
    /// </summary>
    LiquidacionPlan,

    /// <summary>
    /// Liquidación por acuerdo sin plan: se abona el monto acordado y se condona el saldo
    /// restante con un movimiento de ajuste, dejando la cuenta en cero.
    /// </summary>
    LiquidacionAcuerdo
}

/// <summary>
/// Intención capturada en el diálogo de registro de pago. La vista decide qué llamadas
/// a la API ejecutar según el modo.
/// </summary>
public record ResultadoDialogoPago(
    ModoPagoDialogo Modo,
    decimal Monto,
    long? IdCobrador,
    string? Observaciones,
    decimal SaldoPendiente);
