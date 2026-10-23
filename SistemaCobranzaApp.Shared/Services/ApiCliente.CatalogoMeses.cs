using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<MesDto>? datos, string? error)> ListarMesesOperativosAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MesDto>("/api/meses", ct);

    // ---- Administración ----
    public Task<(List<MesDto>? datos, string? error)> ListarMesesAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MesDto>("/api/admin/meses", ct);

    public Task<(MesDto? datos, string? error)> ObtenerMesAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<MesDto>($"/api/admin/meses/{id}", ct);

    public Task<(MesDto? datos, string? error)> CrearMesAsync(int numMeses, string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<MesDto>(HttpMethod.Post, "/api/admin/meses", new { NumMeses = numMeses, Descripcion = descripcion }, ct);

    public Task<(MesDto? datos, string? error)> ActualizarMesAsync(long id, int numMeses, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<MesDto>(HttpMethod.Put, $"/api/admin/meses/{id}", new { NumMeses = numMeses, Descripcion = descripcion, Activo = activo }, ct);

    public Task<(MesDto? datos, string? error)> DarDeBajaMesAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<MesDto>(HttpMethod.Delete, $"/api/admin/meses/{id}", null, ct);
}
