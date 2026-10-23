using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<EstatusVentaDto>? datos, string? error)> ListarEstatusVentaOperativosAsync(CancellationToken ct = default)
        => GetSobreListaAsync<EstatusVentaDto>("/api/estatus/venta", ct);

    // ---- Administración ----
    public Task<(List<EstatusVentaDto>? datos, string? error)> ListarEstatusVentaAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<EstatusVentaDto>("/api/admin/estatus/venta", ct);

    public Task<(EstatusVentaDto? datos, string? error)> ObtenerEstatusVentaAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<EstatusVentaDto>($"/api/admin/estatus/venta/{id}", ct);

    public Task<(EstatusVentaDto? datos, string? error)> CrearEstatusVentaAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<EstatusVentaDto>(HttpMethod.Post, "/api/admin/estatus/venta", new { Descripcion = descripcion }, ct);

    public Task<(EstatusVentaDto? datos, string? error)> ActualizarEstatusVentaAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<EstatusVentaDto>(HttpMethod.Put, $"/api/admin/estatus/venta/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(EstatusVentaDto? datos, string? error)> DarDeBajaEstatusVentaAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<EstatusVentaDto>(HttpMethod.Delete, $"/api/admin/estatus/venta/{id}", null, ct);
}