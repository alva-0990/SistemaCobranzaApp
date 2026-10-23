using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Cobradores.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activos), filtro opcional por zona ----
    public Task<(List<CobradorDto>? datos, string? error)> ListarCobradoresOperativosAsync(long? idZona = null, CancellationToken ct = default)
        => GetSobreListaAsync<CobradorDto>(idZona is { } z ? $"/api/cobradores?idZona={z}" : "/api/cobradores", ct);

    // ---- Administración ----
    public Task<(List<CobradorDto>? datos, string? error)> ListarCobradoresAdminAsync(
        bool incluirInactivos = false, long? idZona = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (incluirInactivos) qs.Add("incluirInactivos=true");
        if (idZona is { } z) qs.Add($"idZona={z}");
        var url = "/api/admin/cobradores" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return GetSobreListaAsync<CobradorDto>(url, ct);
    }

    public Task<(CobradorDto? datos, string? error)> ObtenerCobradorAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CobradorDto>($"/api/admin/cobradores/{id}", ct);

    public Task<(CobradorDto? datos, string? error)> CrearCobradorAsync(string nombre, long? idZona,
        CancellationToken ct = default, long? idUsuario = null)
        => EnviarSobreAsync<CobradorDto>(HttpMethod.Post, "/api/admin/cobradores", new { Nombre = nombre, IdZona = idZona, IdUsuario = idUsuario }, ct);

    public Task<(CobradorDto? datos, string? error)> ActualizarCobradorAsync(long id, string nombre, long? idZona,
        bool activo, CancellationToken ct = default, long? idUsuario = null)
        => EnviarSobreAsync<CobradorDto>(HttpMethod.Put, $"/api/admin/cobradores/{id}", new { Nombre = nombre, IdZona = idZona, Activo = activo, IdUsuario = idUsuario }, ct);

    public Task<(CobradorDto? datos, string? error)> DarDeBajaCobradorAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<CobradorDto>(HttpMethod.Delete, $"/api/admin/cobradores/{id}", null, ct);
}
