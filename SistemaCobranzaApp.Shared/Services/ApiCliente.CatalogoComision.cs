using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Esquemas de Comisiones.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activos para selectores) ----
    public Task<(List<CatalogoComisionDto>? datos, string? error)> ListarComisionesOperativasAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CatalogoComisionDto>("/api/comisiones", ct);

    // ---- Panel de Administración (CRUD Completo) ----
    public Task<(List<CatalogoComisionDto>? datos, string? error)> ListarComisionesAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CatalogoComisionDto>("/api/admin/comisiones", ct);

    public Task<(CatalogoComisionDto? datos, string? error)> ObtenerComisionAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CatalogoComisionDto>($"/api/admin/comisiones/{id}", ct);

    public Task<(CatalogoComisionDto? datos, string? error)> CrearComisionAsync(CrearCatalogoComisionRequest request, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoComisionDto>(HttpMethod.Post, "/api/admin/comisiones", request, ct);

    public Task<(CatalogoComisionDto? datos, string? error)> ActualizarComisionAsync(long id, ActualizarCatalogoComisionRequest request, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoComisionDto>(HttpMethod.Put, $"/api/admin/comisiones/{id}", request, ct);

    public Task<(CatalogoComisionDto? datos, string? error)> DarDeBajaComisionAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoComisionDto>(HttpMethod.Delete, $"/api/admin/comisiones/{id}", null, ct);
}