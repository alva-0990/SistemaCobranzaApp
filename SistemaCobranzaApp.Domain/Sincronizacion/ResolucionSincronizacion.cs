using SistemaCobranzaApp.Domain.Cobranza;

namespace SistemaCobranzaApp.Domain.Sincronizacion;

/// <summary>
/// Decisión de qué hacer con un pago que llega desde la cola offline del móvil.
/// Lógica pura (sin base de datos) para poder probarla de forma aislada.
/// </summary>
public static class ResolucionSincronizacion
{
    public enum Decision
    {
        /// <summary>El pago ya existe (misma UUID): idempotente, no duplicar.</summary>
        Duplicado,
        /// <summary>Datos inválidos (monto &lt;= 0).</summary>
        Error,
        /// <summary>La venta ya estaba liquidada en el servidor.</summary>
        ConflictoYaLiquidada,
        /// <summary>El abono excede el saldo real del servidor (otro cobró antes).</summary>
        ConflictoExcedeSaldo,
        /// <summary>Se puede aplicar.</summary>
        Aplicar
    }

    /// <summary>
    /// Resuelve la decisión para un pago entrante.
    /// </summary>
    /// <param name="yaExiste">True si el PagoUuid ya está registrado en el servidor.</param>
    /// <param name="montoAbona">Monto del abono entrante.</param>
    /// <param name="saldoRealServidor">Saldo pendiente autoritativo calculado en el servidor.</param>
    public static Decision Resolver(bool yaExiste, decimal montoAbona, decimal saldoRealServidor)
    {
        if (yaExiste)
            return Decision.Duplicado;

        var validacion = CalculoCobranza.ValidarAbono(montoAbona, saldoRealServidor);
        return validacion switch
        {
            CalculoCobranza.ResultadoValidacion.MontoInvalido => Decision.Error,
            CalculoCobranza.ResultadoValidacion.YaLiquidada => Decision.ConflictoYaLiquidada,
            CalculoCobranza.ResultadoValidacion.AbonoExcedeSaldo => Decision.ConflictoExcedeSaldo,
            CalculoCobranza.ResultadoValidacion.Ok => Decision.Aplicar,
            _ => Decision.Error
        };
    }
}
