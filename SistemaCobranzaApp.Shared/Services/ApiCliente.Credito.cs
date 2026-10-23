using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<MovimientoDto>? datos, string? error)> ListarMovimientosOperativosAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MovimientoDto>("/api/movimientos", ct);

    // ---- Administration ----
    public Task<(List<MovimientoDto>? datos, string? error)> ListarMovimientosAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<MovimientoDto>("/api/admin/movimientos", ct);

    public Task<(MovimientoDto? datos, string? error)> ObtenerMovimientoAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<MovimientoDto>($"/api/admin/movimientos/{id}", ct);

    public Task<(MovimientoDto? datos, string? error)> CrearMovimientoAsync(string descripcion, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoDto>(HttpMethod.Post, "/api/admin/movimientos", new { Descripcion = descripcion }, ct);

    public Task<(MovimientoDto? datos, string? error)> ActualizarMovimientoAsync(long id, string descripcion, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoDto>(HttpMethod.Put, $"/api/admin/movimientos/{id}", new { Descripcion = descripcion, Activo = activo }, ct);

    public Task<(MovimientoDto? datos, string? error)> DarDeBajaMovimientoAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoDto>(HttpMethod.Delete, $"/api/admin/movimientos/{id}", null, ct);
}