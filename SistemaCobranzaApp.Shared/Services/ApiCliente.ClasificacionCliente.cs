using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<ClasificaionClienteDto>? datos, string? error)> ListarClasificacionesClienteOperativasAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ClasificaionClienteDto>("/api/clasificacion/cliente", ct);

    // ---- Administración ----
    public Task<(List<ClasificaionClienteDto>? datos, string? error)> ListarClasificacionesClienteAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ClasificaionClienteDto>("/api/admin/clasificacion/cliente", ct);

    public Task<(ClasificaionClienteDto? datos, string? error)> ObtenerClasificacionClienteAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<ClasificaionClienteDto>($"/api/admin/clasificacion/cliente/{id}", ct);

    public Task<(ClasificaionClienteDto? datos, string? error)> CrearClasificacionClienteAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<ClasificaionClienteDto>(HttpMethod.Post, "/api/admin/clasificacion/cliente", new { Descripcion = descripcion }, ct);

    public Task<(ClasificaionClienteDto? datos, string? error)> ActualizarClasificacionClienteAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<ClasificaionClienteDto>(HttpMethod.Put, $"/api/admin/clasificacion/cliente/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(ClasificaionClienteDto? datos, string? error)> DarDeBajaClasificacionClienteAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<ClasificaionClienteDto>(HttpMethod.Delete, $"/api/admin/clasificacion/cliente/{id}", null, ct);
}
