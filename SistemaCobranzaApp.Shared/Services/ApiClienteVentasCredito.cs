using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el control administrativo de Ventas a Crédito y Recibos de Cobranza.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ===========================================================================
    // MÓDULO ADMINISTRATIVO DE VENTAS A CRÉDITO
    // ===========================================================================

    /// <summary>Registra administrativamente una venta a crédito con su plan de amortización en el servidor.</summary>
    public Task<(VentaDto? datos, string? error)> CrearVentaAdminAsync(CrearVentaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<VentaDto>(HttpMethod.Post, "/api/admin/ventas", req, ct);

    /// <summary>🚀 [NUEVO] Envía la cotización de una nueva venta absorbiendo y liquidando el saldo pendiente de una cuenta anterior.</summary>
    public Task<(VentaDto? datos, string? error)> RenovarVentaAdminAsync(long idVentaVieja, CrearVentaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<VentaDto>(HttpMethod.Post, $"/api/admin/ventas/{idVentaVieja}/renovar", req, ct);

    /// <summary>Edita administrativamente una venta sin abonos (reemplazo integral de detalles, planes y cuotas).</summary>
    public Task<(VentaDto? datos, string? error)> EditarVentaAdminAsync(long idVenta, ActualizarVentaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<VentaDto>(HttpMethod.Put, $"/api/admin/ventas/{idVenta}", req, ct);

    /// <summary>Obtiene la radiografía administrativa completa de una cuenta (Cabecera, Cuotas y Planes).</summary>
    public Task<(VentaDto? datos, string? error)> ObtenerVentaAdminAsync(long idVenta, CancellationToken ct = default)
        => GetSobreIndividualAsync<VentaDto>($"/api/admin/ventas/{idVenta}", ct);

    /// <summary>Cancela de forma lógica una venta activa directamente desde el panel de administración.</summary>
    public Task<(CancelarVentaResponse? datos, string? error)> CancelarVentaAdminAsync(long idVenta, CancellationToken ct = default)
        => EnviarSobreAsync<CancelarVentaResponse>(HttpMethod.Post, $"/api/admin/ventas/{idVenta}/cancelar", null, ct);

    /// <summary>Ejecuta una búsqueda de cuentas bajo el segmento administrativo de la API.</summary>
    public Task<(VentaDetalleDto? datos, string? error)> BuscarVentaCobranzaAdminAsync(string texto, CancellationToken ct = default)
        => GetSobreIndividualAsync<VentaDetalleDto>($"/api/admin/ventas/buscar?texto={System.Uri.EscapeDataString(texto)}", ct);

    /// <summary>Consulta el estado de cuenta histórico de un cliente para auditoría en el panel administrador.</summary>
    public Task<(EstadoCuentaResponse? datos, string? error)> ObtenerEstadoCuentaAdminAsync(long idCliente, CancellationToken ct = default)
        => GetSobreIndividualAsync<EstadoCuentaResponse>($"/api/admin/ventas/estado-cuenta/{idCliente}", ct);


    // ===========================================================================
    // MÓDULO ADMINISTRATIVO DE COBRANZA EN RUTA (PAGOS)
    // ===========================================================================

    /// <summary>Registra administrativamente un abono en efectivo impactando en cascada el plan de amortización.</summary>
    public Task<(RegistrarPagoResponse? datos, string? error)> RegistrarPagoAdminAsync(RegistrarPagoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<RegistrarPagoResponse>(HttpMethod.Post, "/api/admin/pagos", req, ct);

    /// <summary>
    /// Obsoleto. El DELETE sobre /api/admin/pagos/{id} ahora ejecuta la reversión en cascada
    /// (fn_revertir_abono_cascada). Usa <see cref="RevertirPagoAsync"/> en su lugar.
    /// </summary>
    [Obsolete("El endpoint de cancelación administrativa se unificó con la reversión en cascada. Usa RevertirPagoAsync.")]
    public Task<(CancelarPagoResponse? datos, string? error)> CancelarPagoAdminAsync(long idPago, CancellationToken ct = default)
        => EnviarSobreAsync<CancelarPagoResponse>(HttpMethod.Delete, $"/api/admin/pagos/{idPago}", null, ct);

    /// <summary>Consulta la tira completa de abonos históricos de una cuenta bajo el segmento de administración.</summary>
    public Task<(List<PagoHistoricoDto>? datos, string? error)> ObtenerHistorialPagosAdminAsync(long idVenta, CancellationToken ct = default)
        => GetSobreListaAsync<PagoHistoricoDto>($"/api/admin/pagos/historial/{idVenta}", ct);
}