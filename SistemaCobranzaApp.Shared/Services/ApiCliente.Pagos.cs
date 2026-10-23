namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    /// <summary>
    /// Solicita la eliminación y reversión total de un abono físico en caja.
    /// El motor de PostgreSQL se encargará de limpiar bonificaciones y reajustar las cuotas.
    /// </summary>
    /// <param name="idPago">Identificador del pago físico principal (IdMovimiento = 1)</param>
    public Task<(bool datos, string? error)> RevertirPagoAsync(long idPago, CancellationToken ct = default)
        => EnviarSobreAsync<bool>(HttpMethod.Delete, $"/api/admin/pagos/{idPago}", null, ct); // ✏️ Corregido: (bool datos) en lugar de (bool? datos)
}