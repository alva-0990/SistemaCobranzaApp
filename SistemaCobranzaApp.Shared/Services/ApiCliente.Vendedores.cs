using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Vendedores.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activos), filtro opcional por zona ----
    public Task<(List<VendedorDto>? datos, string? error)> ListarVendedoresOperativosAsync(long? idZona = null, CancellationToken ct = default)
        => GetSobreListaAsync<VendedorDto>(idZona is { } z ? $"/api/vendedores?idZona={z}" : "/api/vendedores", ct);

    // ---- Administración ----
    public Task<(List<VendedorDto>? datos, string? error)> ListarVendedoresAdminAsync(
        bool incluirInactivos = false, long? idZona = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (incluirInactivos) qs.Add("incluirInactivos=true");
        if (idZona is { } z) qs.Add($"idZona={z}");
        var url = "/api/admin/vendedores" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return GetSobreListaAsync<VendedorDto>(url, ct);
    }

    public Task<(VendedorDto? datos, string? error)> ObtenerVendedorAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<VendedorDto>($"/api/admin/vendedores/{id}", ct);

    public Task<(VendedorDto? datos, string? error)> CrearVendedorAsync(string nombre, long? idZona,
        CancellationToken ct = default, long? idUsuario = null)
        => EnviarSobreAsync<VendedorDto>(HttpMethod.Post, "/api/admin/vendedores", new { Nombre = nombre, IdZona = idZona, IdUsuario = idUsuario }, ct);

    public Task<(VendedorDto? datos, string? error)> ActualizarVendedorAsync(long id, string nombre, long? idZona,
        bool activo, CancellationToken ct = default, long? idUsuario = null)
        => EnviarSobreAsync<VendedorDto>(HttpMethod.Put, $"/api/admin/vendedores/{id}", new { Nombre = nombre, IdZona = idZona, Activo = activo, IdUsuario = idUsuario }, ct);

    public Task<(VendedorDto? datos, string? error)> DarDeBajaVendedorAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<VendedorDto>(HttpMethod.Delete, $"/api/admin/vendedores/{id}", null, ct);
}
