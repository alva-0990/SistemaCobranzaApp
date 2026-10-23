using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el módulo de Inventario y Propuestas de Envío.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ==========================================
    // 📦 SECCIÓN: INVENTARIOS (ADMINISTRACIÓN)
    // ==========================================

    /// <summary> Obtiene el listado completo de existencias en la bodega central. </summary>
    public Task<(List<ExistenciaCentralDto>? datos, string? error)> ObtenerExistenciasCentralAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ExistenciaCentralDto>("/api/admin/inventario/central", ct);

    /// <summary> Obtiene las existencias operativas asignadas a una zona o ruta en específico. </summary>
    public Task<(List<ExistenciaZonaDto>? datos, string? error)> ObtenerExistenciasZonaAsync(long idZona, CancellationToken ct = default)
        => GetSobreListaAsync<ExistenciaZonaDto>($"/api/admin/inventario/zona/{idZona}", ct);

    /// <summary> Registra una entrada o ajuste físico en el inventario de la bodega central. </summary>
    public Task<(MovimientoInventarioDto? datos, string? error)> RegistrarMovimientoCentralAsync(MovimientoCentralRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoInventarioDto>(HttpMethod.Post, "/api/admin/inventario/central/movimiento", req, ct);

    /// <summary> Registra una entrada o ajuste físico directo en el inventario de una zona/ruta. </summary>
    public Task<(MovimientoInventarioDto? datos, string? error)> RegistrarMovimientoZonaAsync(MovimientoZonaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoInventarioDto>(HttpMethod.Post, "/api/admin/inventario/zona/movimiento", req, ct);

    /// <summary> Recupera el historial histórico e inmutable de movimientos de un producto (Kárdex). </summary>
    public Task<(List<MovimientoInventarioDto>? datos, string? error)> ObtenerHistorialProductoAsync(long idProducto, int? max = null, CancellationToken ct = default)
    {
        var url = $"/api/admin/inventario/producto/{idProducto}/movimientos";
        if (max.HasValue) url += $"?max={max.Value}";
        return GetSobreListaAsync<MovimientoInventarioDto>(url, ct);
    }

    /// <summary>
    /// Historial global de movimientos de inventario con filtros opcionales:
    /// tipos (lista de TipoMovimiento), zona afectada y rango de fechas.
    /// </summary>
    public Task<(List<MovimientoHistorialDto>? datos, string? error)> ObtenerHistorialGlobalAsync(
        IEnumerable<string>? tipos = null, long? idZona = null,
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, int? max = null,
        CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (tipos is not null)
        {
            var csv = string.Join(",", tipos.Where(t => !string.IsNullOrWhiteSpace(t)));
            if (!string.IsNullOrWhiteSpace(csv)) queryParams.Add($"tipos={Uri.EscapeDataString(csv)}");
        }
        if (idZona.HasValue) queryParams.Add($"idZona={idZona.Value}");
        if (desde.HasValue) queryParams.Add($"desde={Uri.EscapeDataString(desde.Value.ToString("o"))}");
        if (hasta.HasValue) queryParams.Add($"hasta={Uri.EscapeDataString(hasta.Value.ToString("o"))}");
        if (max.HasValue) queryParams.Add($"max={max.Value}");

        var url = "/api/admin/inventario/movimientos";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return GetSobreListaAsync<MovimientoHistorialDto>(url, ct);
    }

    // ==========================================
    // 📋 SECCIÓN: PROPUESTAS DE ENVÍO (ADMINISTRACIÓN)
    // ==========================================

    /// <summary> Consulta las propuestas de envío generadas con filtros opcionales de estatus y zona. </summary>
    public Task<(List<PropuestaDto>? datos, string? error)> ListarPropuestasAsync(string? estatus = null, long? idZona = null, CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(estatus)) queryParams.Add($"estatus={Uri.EscapeDataString(estatus)}");
        if (idZona.HasValue) queryParams.Add($"idZona={idZona.Value}");

        var url = "/api/admin/propuestas";
        if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

        return GetSobreListaAsync<PropuestaDto>(url, ct);
    }

    /// <summary> Obtiene el detalle completo y artículos de una propuesta de envío mediante su ID. </summary>
    public Task<(PropuestaDto? datos, string? error)> ObtenerPropuestaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<PropuestaDto>($"/api/admin/propuestas/{id}", ct);

    /// <summary> Genera una nueva propuesta de envío en estado PENDIENTE desde la central hacia una ruta. </summary>
    public Task<(PropuestaDto? datos, string? error)> CrearPropuestaAsync(CrearPropuestaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<PropuestaDto>(HttpMethod.Post, "/api/admin/propuestas", req, ct);

    /// <summary> Confirma y aplica de manera transaccional una propuesta de envío cargando el inventario a la ruta. </summary>
    public Task<(PropuestaDto? datos, string? error)> ConfirmarPropuestaAsync(long idPropuesta, ResolverPropuestaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<PropuestaDto>(HttpMethod.Post, $"/api/admin/propuestas/{idPropuesta}/confirmar", req, ct);

    /// <summary> Rechaza una propuesta de envío (la marca RECHAZADA sin tocar el inventario). </summary>
    public Task<(PropuestaDto? datos, string? error)> RechazarPropuestaAdminAsync(long idPropuesta, ResolverPropuestaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<PropuestaDto>(HttpMethod.Post, $"/api/admin/propuestas/{idPropuesta}/rechazar", req, ct);
}
