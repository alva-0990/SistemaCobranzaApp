using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<CondicionCreditoDto>? datos, string? error)> ListarCondicionesCreditoOperativasAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CondicionCreditoDto>("/api/condicion/credito", ct);

    // ---- Administración ----
    public Task<(List<CondicionCreditoDto>? datos, string? error)> ListarCondicionesCreditoAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CondicionCreditoDto>("/api/admin/condicion/credito", ct);

    public Task<(CondicionCreditoDto? datos, string? error)> ObtenerCondicionCreditoAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CondicionCreditoDto>($"/api/admin/condicion/credito/{id}", ct);

    public Task<(CondicionCreditoDto? datos, string? error)> CrearCondicionCreditoAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<CondicionCreditoDto>(HttpMethod.Post, "/api/admin/condicion/credito", new { Descripcion = descripcion }, ct);

    public Task<(CondicionCreditoDto? datos, string? error)> ActualizarCondicionCreditoAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<CondicionCreditoDto>(HttpMethod.Put, $"/api/admin/condicion/credito/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(CondicionCreditoDto? datos, string? error)> DarDeBajaCondicionCreditoAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<CondicionCreditoDto>(HttpMethod.Delete, $"/api/admin/condicion/credito/{id}", null, ct);
}