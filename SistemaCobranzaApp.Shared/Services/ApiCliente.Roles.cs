using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Roles.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Administración ----
    public Task<(List<RolDto>? datos, string? error)> ListarRolesAdminAsync(
        bool incluirInactivos = false, CancellationToken ct = default)
        => GetSobreListaAsync<RolDto>(
            incluirInactivos ? "/api/admin/roles?incluirInactivos=true" : "/api/admin/roles", ct);

    public Task<(RolDto? datos, string? error)> ObtenerRolAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<RolDto>($"/api/admin/roles/{id}", ct);

    public Task<(RolDto? datos, string? error)> CrearRolAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<RolDto>(HttpMethod.Post, "/api/admin/roles", new { Descripcion = descripcion }, ct);

    public Task<(RolDto? datos, string? error)> ActualizarRolAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<RolDto>(HttpMethod.Put, $"/api/admin/roles/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(RolDto? datos, string? error)> DarDeBajaRolAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<RolDto>(HttpMethod.Delete, $"/api/admin/roles/{id}", null, ct);
}
