using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para la administración y pago manual de Comisiones de Vendedores.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Métodos de Pago Manual de Comisiones ----
    public Task<(List<ComisionPendientePagoDto>? datos, string? error)> ListarComisionesPendientesPagoAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        var parametros = new List<string>();

        // Solo añadimos los parámetros que tienen un valor asignado real
        if (filtros.IdVendedor.HasValue) 
            parametros.Add($"idVendedor={filtros.IdVendedor.Value}");

        if (filtros.IdZona.HasValue) 
            parametros.Add($"idZona={filtros.IdZona.Value}");

        if (filtros.FechaDesde.HasValue) 
            parametros.Add($"fechaDesde={filtros.FechaDesde.Value:yyyy-MM-dd}");

        if (filtros.FechaHasta.HasValue) 
            parametros.Add($"fechaHasta={filtros.FechaHasta.Value:yyyy-MM-dd}");

        // Si la lista está vacía, no se genera QueryString. Si contiene elementos, se unen con '&'
        var queryString = parametros.Any() ? "?" + string.Join("&", parametros) : string.Empty;
        var url = $"/api/admin/pagos-comisiones{queryString}";

        return GetSobreListaAsync<ComisionPendientePagoDto>(url, ct);
    }

    public Task<(int datos, string? error)> ProcesarPagoComisionesAsync(ProcesarPagoComisionesRequest request, CancellationToken ct = default)
        => EnviarSobreAsync<int>(HttpMethod.Post, "/api/admin/pagos-comisiones/procesar", request, ct);
    
    public Task<(List<ComisionPendientePagoDto>? datos, string? error)> ListarComisionesPendientesLiberarAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        var parametros = new List<string>();

        if (filtros.IdVendedor.HasValue) 
            parametros.Add($"idVendedor={filtros.IdVendedor.Value}");

        if (filtros.IdZona.HasValue) 
            parametros.Add($"idZona={filtros.IdZona.Value}");

        if (filtros.FechaDesde.HasValue) 
            parametros.Add($"fechaDesde={filtros.FechaDesde.Value:yyyy-MM-dd}");

        if (filtros.FechaHasta.HasValue) 
            parametros.Add($"fechaHasta={filtros.FechaHasta.Value:yyyy-MM-dd}");

        var queryString = parametros.Any() ? "?" + string.Join("&", parametros) : string.Empty;
        var url = $"/api/admin/pagos-comisiones/pendientes-liberar{queryString}";

        return GetSobreListaAsync<ComisionPendientePagoDto>(url, ct);
    }

    public Task<(int datos, string? error)> ProcesarLiberacionComisionesAsync(ProcesarPagoComisionesRequest request, CancellationToken ct = default)
        => EnviarSobreAsync<int>(HttpMethod.Post, "/api/admin/pagos-comisiones/liberar", request, ct);
    
    // =========================================================================
    // ---- PESTAÑA 3: HISTORIAL DE COMISIONES LIQUIDADAS ----
    // =========================================================================
    
    /// <summary>
    /// Consulta la bitácora histórica de las comisiones que ya fueron pagadas en efectivo.
    /// Envía los filtros avanzados limpios en la QueryString para evitar fallos en Android y Web.
    /// </summary>
    public Task<(List<ComisionPendientePagoDto>? datos, string? error)> ListarHistorialComisionesPagadasAsync(FiltroComisionesRequest filtros, CancellationToken ct = default)
    {
        var parametros = new List<string>();

        if (filtros.IdVendedor.HasValue) 
            parametros.Add($"idVendedor={filtros.IdVendedor.Value}");

        if (filtros.IdZona.HasValue) 
            parametros.Add($"idZona={filtros.IdZona.Value}");

        // CORREGIDO: Se cambiaron los nombres incorrectos por FechaDesde y FechaHasta
        if (filtros.FechaDesde.HasValue) 
            parametros.Add($"fechaDesde={filtros.FechaDesde.Value:yyyy-MM-dd}");

        if (filtros.FechaHasta.HasValue) 
            parametros.Add($"fechaHasta={filtros.FechaHasta.Value:yyyy-MM-dd}");

        var queryString = parametros.Any() ? "?" + string.Join("&", parametros) : string.Empty;
        var url = $"/api/admin/pagos-comisiones/historial{queryString}";

        return GetSobreListaAsync<ComisionPendientePagoDto>(url, ct);
    }
}
