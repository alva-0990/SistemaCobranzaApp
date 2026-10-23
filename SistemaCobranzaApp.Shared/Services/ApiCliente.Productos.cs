using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para la administración de Productos.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// El listado es paginado (<see cref="ProductosPaginaResponse"/>).
/// </summary>
public partial class ApiCliente
{
    public Task<(ProductosPaginaResponse? datos, string? error)> ListarProductosAsync(
        string? texto = null, bool incluirInactivos = false, int pagina = 1, int tamanoPagina = 50,
        CancellationToken ct = default)
    {
        var url = $"/api/productos?pagina={pagina}&tamanoPagina={tamanoPagina}" +
                  $"&incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(texto)) url += $"&texto={Uri.EscapeDataString(texto)}";
        return GetSobreIndividualAsync<ProductosPaginaResponse>(url, ct);
    }

    public Task<(ProductoDto? datos, string? error)> ObtenerProductoAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<ProductoDto>($"/api/productos/{id}", ct);

    public Task<(ProductoDto? producto, string? error)> CrearProductoAsync(
        CrearProductoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ProductoDto>(HttpMethod.Post, "/api/productos", req, ct);

    public Task<(ProductoDto? producto, string? error)> ActualizarProductoAsync(
        long id, ActualizarProductoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ProductoDto>(HttpMethod.Put, $"/api/productos/{id}", req, ct);

    /// <summary>Da de baja (lógica) un producto. Devuelve (true, null) si tuvo éxito.</summary>
    public async Task<(bool ok, string? error)> DarDeBajaProductoAsync(long id, CancellationToken ct = default)
    {
        var (_, error) = await EnviarSobreAsync<ProductoDto>(HttpMethod.Delete, $"/api/productos/{id}", null, ct);
        return (error is null, error);
    }

    // ---- Combos Operativos (Solo activos para selectores rápidos) ----
    public Task<(List<CategoriaDto>? datos, string? error)> ListarCategoriasComboAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CategoriaDto>("/api/catalogos-productos/categorias", ct);

    public Task<(List<MarcaDto>? datos, string? error)> ListarMarcasComboAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MarcaDto>("/api/catalogos-productos/marcas", ct);

    public Task<(List<ColorDto>? datos, string? error)> ListarColoresComboAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ColorDto>("/api/catalogos-productos/colores", ct);

    public Task<(List<TallaDto>? datos, string? error)> ListarTallasComboAsync(CancellationToken ct = default)
        => GetSobreListaAsync<TallaDto>("/api/catalogos-productos/tallas", ct);


    // ---- 1. Administración de Categorías ----
    public Task<(List<CategoriaDto>? datos, string? error)> ListarCategoriasAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CategoriaDto>("/api/admin/catalogos-productos/categorias", ct);

    public Task<(CategoriaDto? datos, string? error)> ObtenerCategoriaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CategoriaDto>($"/api/admin/catalogos-productos/categorias/{id}", ct);

    public Task<(CategoriaDto? datos, string? error)> CrearCategoriaAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<CategoriaDto>(HttpMethod.Post, "/api/admin/catalogos-productos/categorias", new { Descripcion = descripcion }, ct);

    public Task<(CategoriaDto? datos, string? error)> ActualizarCategoriaAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<CategoriaDto>(HttpMethod.Put, $"/api/admin/catalogos-productos/categorias/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(CategoriaDto? datos, string? error)> DarDeBajaCategoriaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<CategoriaDto>(HttpMethod.Delete, $"/api/admin/catalogos-productos/categorias/{id}", null, ct);


    // ---- 2. Administración de Marcas ----
    public Task<(List<MarcaDto>? datos, string? error)> ListarMarcasAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MarcaDto>("/api/admin/catalogos-productos/marcas", ct);

    public Task<(MarcaDto? datos, string? error)> ObtenerMarcaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<MarcaDto>($"/api/admin/catalogos-productos/marcas/{id}", ct);

    public Task<(MarcaDto? datos, string? error)> CrearMarcaAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<MarcaDto>(HttpMethod.Post, "/api/admin/catalogos-productos/marcas", new { Descripcion = descripcion }, ct);

    public Task<(MarcaDto? datos, string? error)> ActualizarMarcaAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<MarcaDto>(HttpMethod.Put, $"/api/admin/catalogos-productos/marcas/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(MarcaDto? datos, string? error)> DarDeBajaMarcaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<MarcaDto>(HttpMethod.Delete, $"/api/admin/catalogos-productos/marcas/{id}", null, ct);

    // ---- 3. Administración de Colores ----
    public Task<(List<ColorDto>? datos, string? error)> ListarColoresAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ColorDto>("/api/admin/catalogos-productos/colores", ct);

    public Task<(ColorDto? datos, string? error)> ObtenerColorAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<ColorDto>($"/api/admin/catalogos-productos/colores/{id}", ct);

    public Task<(ColorDto? datos, string? error)> CrearColorAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<ColorDto>(HttpMethod.Post, "/api/admin/catalogos-productos/colores", new { Descripcion = descripcion }, ct);

    public Task<(ColorDto? datos, string? error)> ActualizarColorAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<ColorDto>(HttpMethod.Put, $"/api/admin/catalogos-productos/colores/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(ColorDto? datos, string? error)> DarDeBajaColorAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<ColorDto>(HttpMethod.Delete, $"/api/admin/catalogos-productos/colores/{id}", null, ct);


    // ---- 4. Administración de Tallas ----
    public Task<(List<TallaDto>? datos, string? error)> ListarTallasAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<TallaDto>("/api/admin/catalogos-productos/tallas", ct);

    public Task<(TallaDto? datos, string? error)> ObtenerTallaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<TallaDto>($"/api/admin/catalogos-productos/tallas/{id}", ct);

    public Task<(TallaDto? datos, string? error)> CrearTallaAsync(string descripcion, string tipo, CancellationToken ct = default)
        => EnviarSobreAsync<TallaDto>(HttpMethod.Post, "/api/admin/catalogos-productos/tallas", new { Descripcion = descripcion, Tipo = tipo }, ct);

    public Task<(TallaDto? datos, string? error)> ActualizarTallaAsync(long id, string descripcion, string tipo, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<TallaDto>(HttpMethod.Put, $"/api/admin/catalogos-productos/tallas/{id}", new { Descripcion = descripcion, Tipo = tipo, Activo = activo }, ct);

    public Task<(TallaDto? datos, string? error)> DarDeBajaTallaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<TallaDto>(HttpMethod.Delete, $"/api/admin/catalogos-productos/tallas/{id}", null, ct);
}
