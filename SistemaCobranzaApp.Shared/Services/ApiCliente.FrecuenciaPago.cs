using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo de Frecuencias de Pago.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (solo activas) ----
    public Task<(List<CatalogoFrecuenciaPagoDto>? datos, string? error)> ListarFrecuenciasPagoOperativasAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CatalogoFrecuenciaPagoDto>("/api/frecuencias-pago", ct);

    // ---- Administración ----
    public Task<(List<CatalogoFrecuenciaPagoDto>? datos, string? error)> ListarFrecuenciasPagoAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<CatalogoFrecuenciaPagoDto>("/api/admin/frecuencias-pago", ct);

    public Task<(CatalogoFrecuenciaPagoDto? datos, string? error)> ObtenerFrecuenciaPagoAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CatalogoFrecuenciaPagoDto>($"/api/admin/frecuencias-pago/{id}", ct);

    public Task<(CatalogoFrecuenciaPagoDto? datos, string? error)> CrearFrecuenciaPagoAsync(string descripcion, int diasPeriodo, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoFrecuenciaPagoDto>(HttpMethod.Post, "/api/admin/frecuencias-pago", new { Descripcion = descripcion, DiasPeriodo = diasPeriodo }, ct);

    public Task<(CatalogoFrecuenciaPagoDto? datos, string? error)> ActualizarFrecuenciaPagoAsync(long id, string descripcion, int diasPeriodo, bool activo = true, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoFrecuenciaPagoDto>(HttpMethod.Put, $"/api/admin/frecuencias-pago/{id}", new { Descripcion = descripcion, DiasPeriodo = diasPeriodo, Activo = activo }, ct);

    public Task<(CatalogoFrecuenciaPagoDto? datos, string? error)> DarDeBajaFrecuenciaPagoAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoFrecuenciaPagoDto>(HttpMethod.Delete, $"/api/admin/frecuencias-pago/{id}", null, ct);
}
