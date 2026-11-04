namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Detonante en memoria entre el flujo que encola mensajes (ventas / pagos) y el
/// BackgroundService que los envía. Se registra como SINGLETON.
///
/// Diseño robusto (no depende de CurrentCount == 0):
///   * <see cref="Avisar"/> libera el semáforo como mucho hasta 1 permiso pendiente,
///     de modo que N encolados seguidos no inflan el contador: el worker drena TODA
///     la cola en cada ciclo, así que una sola señal basta para procesar el lote.
///   * <see cref="EsperarAsync"/> despierta con la señal O cuando vence el timeout
///     de respaldo (polling), por lo que los mensajes insertados por otra vía
///     (p. ej. la sincronización offline) nunca se quedan atascados.
/// </summary>
public sealed class SenalColaWhatsApp
{
    private readonly SemaphoreSlim _semaforo = new(0, 1);

    /// <summary>
    /// Despierta al worker. Idempotente: varias llamadas seguidas sin que el worker
    /// consuma la señal dejan a lo sumo un permiso pendiente.
    /// </summary>
    public void Avisar()
    {
        // Release lanza SemaphoreFullException si ya está en el máximo (1). Ese caso
        // significa "ya hay una señal pendiente", así que lo ignoramos a propósito.
        try
        {
            _semaforo.Release();
        }
        catch (SemaphoreFullException)
        {
            // Señal ya pendiente; no hace falta acumular más.
        }
    }

    /// <summary>
    /// Espera una señal o a que venza <paramref name="respaldo"/> (polling de respaldo).
    /// Devuelve siempre el control al worker para que vuelva a inspeccionar la cola.
    /// </summary>
    public async Task EsperarAsync(TimeSpan respaldo, CancellationToken ct)
    {
        try
        {
            await _semaforo.WaitAsync(respaldo, ct);
        }
        catch (OperationCanceledException)
        {
            // Apagado del host: propagamos la cancelación al worker.
            throw;
        }
    }
}
