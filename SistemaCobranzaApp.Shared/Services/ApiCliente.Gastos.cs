using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el módulo de Gastos (categorías, cuentas,
/// gastos, pagos, movimientos, comprobantes y reportes). Consumen el sobre
/// estándar <see cref="ApiRespuesta{T}"/> vía los helpers GetSobre*/EnviarSobre*.
/// </summary>
public partial class ApiCliente
{
    private static string FechaIso(DateTimeOffset? d)
        => d is { } v ? Uri.EscapeDataString(v.ToString("o")) : "";

    // ==========================================
    // CATEGORÍAS DE GASTO
    // ==========================================

    public Task<(List<CatalogoCategoriaGastoDto>? datos, string? error)> ListarCategoriasGastoAsync(
        bool incluirInactivas = false, CancellationToken ct = default)
        => GetSobreListaAsync<CatalogoCategoriaGastoDto>(
            $"/api/admin/categorias-gasto?incluirInactivas={incluirInactivas.ToString().ToLowerInvariant()}", ct);

    public Task<(CatalogoCategoriaGastoDto? datos, string? error)> ObtenerCategoriaGastoAsync(
        long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CatalogoCategoriaGastoDto>($"/api/admin/categorias-gasto/{id}", ct);

    public Task<(CatalogoCategoriaGastoDto? datos, string? error)> CrearCategoriaGastoAsync(
        CrearCatalogoCategoriaGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoCategoriaGastoDto>(HttpMethod.Post, "/api/admin/categorias-gasto", req, ct);

    public Task<(CatalogoCategoriaGastoDto? datos, string? error)> ActualizarCategoriaGastoAsync(
        long id, ActualizarCatalogoCategoriaGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoCategoriaGastoDto>(HttpMethod.Put, $"/api/admin/categorias-gasto/{id}", req, ct);

    public Task<(CatalogoCategoriaGastoDto? datos, string? error)> CambiarEstatusCategoriaGastoAsync(
        long id, bool activo, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoCategoriaGastoDto>(
            HttpMethod.Patch, $"/api/admin/categorias-gasto/{id}/estatus",
            new CambiarEstatusCategoriaGastoRequest(activo), ct);

    public Task<(CatalogoCategoriaGastoDto? datos, string? error)> DarDeBajaCategoriaGastoAsync(
        long id, CancellationToken ct = default)
        => EnviarSobreAsync<CatalogoCategoriaGastoDto>(HttpMethod.Delete, $"/api/admin/categorias-gasto/{id}", null, ct);

    // ==========================================
    // CUENTAS DE GASTO
    // ==========================================

    public Task<(List<CuentaGastoDto>? datos, string? error)> ListarCuentasGastoAsync(
        bool incluirInactivas = false, CancellationToken ct = default)
        => GetSobreListaAsync<CuentaGastoDto>(
            $"/api/admin/cuentas-gasto?incluirInactivas={incluirInactivas.ToString().ToLowerInvariant()}", ct);

    public Task<(CuentaGastoDto? datos, string? error)> ObtenerCuentaGastoAsync(
        long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<CuentaGastoDto>($"/api/admin/cuentas-gasto/{id}", ct);

    public Task<(CuentaGastoDto? datos, string? error)> CrearCuentaGastoAsync(
        CrearCuentaGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<CuentaGastoDto>(HttpMethod.Post, "/api/admin/cuentas-gasto", req, ct);

    public Task<(CuentaGastoDto? datos, string? error)> ActualizarCuentaGastoAsync(
        long id, ActualizarCuentaGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<CuentaGastoDto>(HttpMethod.Put, $"/api/admin/cuentas-gasto/{id}", req, ct);

    public Task<(CuentaGastoDto? datos, string? error)> CambiarEstatusCuentaGastoAsync(
        long id, bool activo, CancellationToken ct = default)
        => EnviarSobreAsync<CuentaGastoDto>(
            HttpMethod.Patch, $"/api/admin/cuentas-gasto/{id}/estatus",
            new CambiarEstatusCuentaGastoRequest(activo), ct);

    public Task<(List<MovimientoCuentaGastoDto>? datos, string? error)> ListarMovimientosCuentaGastoAsync(
        long idCuentaGasto, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken ct = default)
    {
        var qp = new List<string>();
        if (desde.HasValue) qp.Add($"desde={FechaIso(desde)}");
        if (hasta.HasValue) qp.Add($"hasta={FechaIso(hasta)}");
        var url = $"/api/admin/cuentas-gasto/{idCuentaGasto}/movimientos";
        if (qp.Count > 0) url += "?" + string.Join("&", qp);
        return GetSobreListaAsync<MovimientoCuentaGastoDto>(url, ct);
    }

    public Task<(MovimientoCuentaGastoDto? datos, string? error)> RegistrarMovimientoCuentaGastoAsync(
        long idCuentaGasto, CrearMovimientoCuentaGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<MovimientoCuentaGastoDto>(
            HttpMethod.Post, $"/api/admin/cuentas-gasto/{idCuentaGasto}/movimientos", req, ct);

    // ==========================================
    // GASTOS
    // ==========================================

    public Task<(List<GastoDto>? datos, string? error)> ListarGastosAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, string? estatus = null,
        long? idCategoria = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, string? folio = null, bool? soloCentrales = null,
        CancellationToken ct = default)
    {
        var qp = new List<string>();
        if (desde.HasValue) qp.Add($"desde={FechaIso(desde)}");
        if (hasta.HasValue) qp.Add($"hasta={FechaIso(hasta)}");
        if (!string.IsNullOrWhiteSpace(estatus)) qp.Add($"estatus={Uri.EscapeDataString(estatus)}");
        if (idCategoria.HasValue) qp.Add($"idCategoria={idCategoria.Value}");
        if (idZona.HasValue) qp.Add($"idZona={idZona.Value}");
        if (idCobrador.HasValue) qp.Add($"idCobrador={idCobrador.Value}");
        if (idVendedor.HasValue) qp.Add($"idVendedor={idVendedor.Value}");
        if (!string.IsNullOrWhiteSpace(folio)) qp.Add($"folio={Uri.EscapeDataString(folio)}");
        if (soloCentrales is { } sc) qp.Add($"soloCentrales={sc.ToString().ToLowerInvariant()}");

        var url = "/api/admin/gastos";
        if (qp.Count > 0) url += "?" + string.Join("&", qp);
        return GetSobreListaAsync<GastoDto>(url, ct);
    }

    public Task<(GastoDto? datos, string? error)> ObtenerGastoAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<GastoDto>($"/api/admin/gastos/{id}", ct);

    public Task<(GastoDto? datos, string? error)> CrearGastoAsync(
        CrearGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<GastoDto>(HttpMethod.Post, "/api/admin/gastos", req, ct);

    public Task<(GastoDto? datos, string? error)> ActualizarGastoAsync(
        long id, ActualizarGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<GastoDto>(HttpMethod.Put, $"/api/admin/gastos/{id}", req, ct);

    public Task<(GastoDto? datos, string? error)> AutorizarGastoAsync(long id, CancellationToken ct = default)
        => EnviarSobreAsync<GastoDto>(HttpMethod.Post, $"/api/admin/gastos/{id}/autorizar", null, ct);

    public Task<(GastoDto? datos, string? error)> CancelarGastoAsync(
        long id, CancelarGastoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<GastoDto>(HttpMethod.Post, $"/api/admin/gastos/{id}/cancelar", req, ct);

    // ---- Pagos de un gasto ----

    public Task<(List<GastoPagoDto>? datos, string? error)> ListarPagosGastoAsync(
        long idGasto, CancellationToken ct = default)
        => GetSobreListaAsync<GastoPagoDto>($"/api/admin/gastos/{idGasto}/pagos", ct);

    public Task<(GastoPagoDto? datos, string? error)> RegistrarPagoGastoAsync(
        long idGasto, RegistrarGastoPagoRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<GastoPagoDto>(HttpMethod.Post, $"/api/admin/gastos/{idGasto}/pagos", req, ct);

    // ---- Comprobantes de un gasto ----

    public Task<(List<GastoComprobanteDto>? datos, string? error)> ListarComprobantesGastoAsync(
        long idGasto, CancellationToken ct = default)
        => GetSobreListaAsync<GastoComprobanteDto>($"/api/admin/gastos/{idGasto}/comprobantes", ct);

    public Task<(GastoComprobanteDto? datos, string? error)> RegistrarComprobanteGastoAsync(
        long idGasto, CrearGastoComprobanteRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<GastoComprobanteDto>(
            HttpMethod.Post, $"/api/admin/gastos/{idGasto}/comprobantes", req, ct);

    public Task<(object? datos, string? error)> EliminarComprobanteGastoAsync(
        long idGasto, long idComprobante, CancellationToken ct = default)
        => EnviarSobreAsync<object>(
            HttpMethod.Delete, $"/api/admin/gastos/{idGasto}/comprobantes/{idComprobante}", null, ct);

    // ==========================================
    // REPORTES DE GASTOS
    // ==========================================

    private string QueryReporteGastos(
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idCategoria, string? estatus,
        long? idZona, long? idCobrador, long? idVendedor, bool? soloCentrales)
    {
        var qp = new List<string>();
        if (desde.HasValue) qp.Add($"desde={FechaIso(desde)}");
        if (hasta.HasValue) qp.Add($"hasta={FechaIso(hasta)}");
        if (idCategoria.HasValue) qp.Add($"idCategoria={idCategoria.Value}");
        if (!string.IsNullOrWhiteSpace(estatus)) qp.Add($"estatus={Uri.EscapeDataString(estatus)}");
        if (idZona.HasValue) qp.Add($"idZona={idZona.Value}");
        if (idCobrador.HasValue) qp.Add($"idCobrador={idCobrador.Value}");
        if (idVendedor.HasValue) qp.Add($"idVendedor={idVendedor.Value}");
        if (soloCentrales is { } sc) qp.Add($"soloCentrales={sc.ToString().ToLowerInvariant()}");
        return qp.Count > 0 ? "?" + string.Join("&", qp) : "";
    }

    public Task<(ReporteGastosResponse? datos, string? error)> ReporteGastosDetalleAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosResponse>(
            "/api/admin/reportes/gastos" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteGastosAgrupadoResponse? datos, string? error)> ReporteGastosPorCategoriaAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosAgrupadoResponse>(
            "/api/admin/reportes/gastos/por-categoria" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteGastosAgrupadoResponse? datos, string? error)> ReporteGastosPorZonaAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosAgrupadoResponse>(
            "/api/admin/reportes/gastos/por-zona" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteGastosAgrupadoResponse? datos, string? error)> ReporteGastosPorCobradorAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosAgrupadoResponse>(
            "/api/admin/reportes/gastos/por-cobrador" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteGastosAgrupadoResponse? datos, string? error)> ReporteGastosPorVendedorAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosAgrupadoResponse>(
            "/api/admin/reportes/gastos/por-vendedor" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteGastosAgrupadoResponse? datos, string? error)> ReporteGastosPorEstatusAsync(
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, long? idCategoria = null,
        string? estatus = null, long? idZona = null, long? idCobrador = null,
        long? idVendedor = null, bool? soloCentrales = null, CancellationToken ct = default)
        => GetSobreIndividualAsync<ReporteGastosAgrupadoResponse>(
            "/api/admin/reportes/gastos/por-estatus" + QueryReporteGastos(desde, hasta, idCategoria, estatus, idZona, idCobrador, idVendedor, soloCentrales), ct);

    public Task<(ReporteEstadoCuentaGastoResponse? datos, string? error)> ReporteEstadoCuentaGastoAsync(
        long idCuentaGasto, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, CancellationToken ct = default)
    {
        var qp = new List<string>();
        if (desde.HasValue) qp.Add($"desde={FechaIso(desde)}");
        if (hasta.HasValue) qp.Add($"hasta={FechaIso(hasta)}");
        var url = $"/api/admin/reportes/gastos/cuentas/{idCuentaGasto}/estado-cuenta";
        if (qp.Count > 0) url += "?" + string.Join("&", qp);
        return GetSobreIndividualAsync<ReporteEstadoCuentaGastoResponse>(url, ct);
    }
}
