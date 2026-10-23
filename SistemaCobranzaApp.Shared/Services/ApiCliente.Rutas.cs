using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Rutas.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activas), filtro opcional por zona ----
    public Task<(List<RutaAdminDto>? datos, string? error)> ListarRutasOperativasAsync(long? idZona = null, CancellationToken ct = default)
        => GetSobreListaAsync<RutaAdminDto>(idZona is { } z ? $"/api/rutas?idZona={z}" : "/api/rutas", ct);

    // ---- Administración ----
    public Task<(List<RutaAdminDto>? datos, string? error)> ListarRutasAdminAsync(
        bool incluirInactivas = false, long? idZona = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (incluirInactivas) qs.Add("incluirInactivas=true");
        if (idZona is { } z) qs.Add($"idZona={z}");
        var url = "/api/admin/rutas" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return GetSobreListaAsync<RutaAdminDto>(url, ct);
    }

    public Task<(RutaAdminDto? datos, string? error)> ObtenerRutaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<RutaAdminDto>($"/api/admin/rutas/{id}", ct);

    public Task<(RutaAdminDto? datos, string? error)> CrearRutaAsync(string descripcion, long? idZona, CancellationToken ct = default)
        => EnviarSobreAsync<RutaAdminDto>(HttpMethod.Post, "/api/admin/rutas", new { Descripcion = descripcion, IdZona = idZona }, ct);

    public Task<(RutaAdminDto? datos, string? error)> ActualizarRutaAsync(long id, string descripcion, long? idZona, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<RutaAdminDto>(HttpMethod.Put, $"/api/admin/rutas/{id}", new { Descripcion = descripcion, IdZona = idZona, Activo = activo }, ct);

    public Task<(RutaAdminDto? datos, string? error)> DarDeBajaRutaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<RutaAdminDto>(HttpMethod.Delete, $"/api/admin/rutas/{id}", null, ct);
}
