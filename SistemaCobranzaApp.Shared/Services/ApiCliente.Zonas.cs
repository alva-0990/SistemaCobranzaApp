using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Zonas.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<ZonaDto>? datos, string? error)> ListarZonasOperativasAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ZonaDto>("/api/zonas", ct);

    // ---- Administración ----
    public Task<(List<ZonaDto>? datos, string? error)> ListarZonasAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ZonaDto>("/api/admin/zonas", ct);

    public Task<(ZonaDto? datos, string? error)> ObtenerZonaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<ZonaDto>($"/api/admin/zonas/{id}", ct);

    public Task<(ZonaDto? datos, string? error)> CrearZonaAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<ZonaDto>(HttpMethod.Post, "/api/admin/zonas", new { Descripcion = descripcion }, ct);

    public Task<(ZonaDto? datos, string? error)> ActualizarZonaAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<ZonaDto>(HttpMethod.Put, $"/api/admin/zonas/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(ZonaDto? datos, string? error)> DarDeBajaZonaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<ZonaDto>(HttpMethod.Delete, $"/api/admin/zonas/{id}", null, ct);
}
