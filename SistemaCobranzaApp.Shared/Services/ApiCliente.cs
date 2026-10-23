using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public partial class ApiCliente
{
    private readonly HttpClient _http;
    private readonly IServicioAlmacenamiento _almacenamiento;
    private const string TokenKey = "auth_token_cobranza";

    // 1. MODIFICADO: Constructor ahora recibe la abstracción del almacenamiento
    public ApiCliente(HttpClient http, IServicioAlmacenamiento almacenamiento)
    {
        _http = http;
        _almacenamiento = almacenamiento;
    }

    /// <summary>
    /// Consulta la cartera del cobrador. Devuelve (respuesta, error).
    /// </summary>
    public async Task<(CarteraResponse? datos, string? error)> ConsultarCarteraAsync(
        long? idCobrador = null, long? idZona = null, long? idRuta = null,
        bool soloConSaldo = true, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/cartera?soloConSaldo={soloConSaldo.ToString().ToLowerInvariant()}";
            if (idCobrador is { } c) url += $"&idCobrador={c}";
            if (idZona is { } z) url += $"&idZona={z}";
            if (idRuta is { } r) url += $"&idRuta={r}";

            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");

            var datos = await resp.Content.ReadFromJsonAsync<CarteraResponse>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, "No se pudo conectar con el servidor.\n" + ex.Message);
        }
        catch (TaskCanceledException)
        {
            return (null, "La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    /// <summary>Busca una venta por Id o CodigoVenta (V#######).</summary>
    public async Task<(VentaDetalleDto? venta, string? error)> BuscarVentaAsync(
        string texto, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/ventas/buscar?texto={Uri.EscapeDataString(texto)}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, "No se encontró la venta.");
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");

            var venta = await resp.Content.ReadFromJsonAsync<VentaDetalleDto>(cancellationToken: ct);
            return (venta, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    /// <summary>Cobradores activos, opcionalmente por zona.</summary>
    public async Task<(IReadOnlyList<PersonaZonaDto>? datos, string? error)> CobradoresAsync(
        long? idZona = null, CancellationToken ct = default)
    {
        try
        {
            var url = idZona is { } z ? $"/api/catalogos/cobradores?idZona={z}" : "/api/catalogos/cobradores";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<List<PersonaZonaDto>>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    /// <summary>
    /// Registra un pago. El PagoUuid se genera en el cliente (idempotencia).
    /// </summary>
    public async Task<(RegistrarPagoResponse? resultado, string? error)> RegistrarPagoAsync(
        RegistrarPagoRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/pagos", req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");

            if (!resp.IsSuccessStatusCode)
            {
                // El backend devuelve { error = "..." } en 400.
                try
                {
                    var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(cancellationToken: ct);
                    return (null, err?.Error ?? $"Error del servidor ({(int)resp.StatusCode}).");
                }
                catch
                {
                    return (null, $"Error del servidor ({(int)resp.StatusCode}).");
                }
            }

            var resultado = await resp.Content.ReadFromJsonAsync<RegistrarPagoResponse>(cancellationToken: ct);
            return (resultado, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    private record ErrorRespuesta(string? Error);

    // ===================== Clientes (administración) =====================

    // ===================== Catálogos de apoyo (para formularios) =====================

    public async Task<IReadOnlyList<RutaDto>> RutasAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/api/catalogos/rutas", ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<RutaDto>();
            return await resp.Content.ReadFromJsonAsync<List<RutaDto>>(cancellationToken: ct)
                ?? (IReadOnlyList<RutaDto>)Array.Empty<RutaDto>();
        }
        catch { return Array.Empty<RutaDto>(); }
    }

    public async Task<IReadOnlyList<PersonaZonaDto>> VendedoresAsync(long? idZona = null, CancellationToken ct = default)
    {
        try
        {
            var url = idZona is { } z ? $"/api/catalogos/vendedores?idZona={z}" : "/api/catalogos/vendedores";
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<PersonaZonaDto>();
            return await resp.Content.ReadFromJsonAsync<List<PersonaZonaDto>>(cancellationToken: ct)
                ?? (IReadOnlyList<PersonaZonaDto>)Array.Empty<PersonaZonaDto>();
        }
        catch { return Array.Empty<PersonaZonaDto>(); }
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> ClasificacionesAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/api/catalogos/clasificaciones", ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<CatalogoItemDto>();
            return await resp.Content.ReadFromJsonAsync<List<CatalogoItemDto>>(cancellationToken: ct)
                ?? (IReadOnlyList<CatalogoItemDto>)Array.Empty<CatalogoItemDto>();
        }
        catch { return Array.Empty<CatalogoItemDto>(); }
    }

    public async Task<IReadOnlyList<CatalogoItemDto>> ZonasAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/api/catalogos/zonas", ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<CatalogoItemDto>();
            return await resp.Content.ReadFromJsonAsync<List<CatalogoItemDto>>(cancellationToken: ct)
                ?? (IReadOnlyList<CatalogoItemDto>)Array.Empty<CatalogoItemDto>();
        }
        catch { return Array.Empty<CatalogoItemDto>(); }
    }

    // ===================== Inventario =====================

    public async Task<(IReadOnlyList<ExistenciaCentralDto>? datos, string? error)> ExistenciasCentralAsync(
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/api/inventario/central", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<List<ExistenciaCentralDto>>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(IReadOnlyList<ExistenciaZonaDto>? datos, string? error)> ExistenciasZonaAsync(
        long idZona, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/inventario/zona/{idZona}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<List<ExistenciaZonaDto>>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(MovimientoInventarioDto? mov, string? error)> MovimientoCentralAsync(
        MovimientoCentralRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/inventario/central/movimiento", req, ct);
            return await LeerMovimientoOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(MovimientoInventarioDto? mov, string? error)> MovimientoZonaAsync(
        MovimientoZonaRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/inventario/zona/movimiento", req, ct);
            return await LeerMovimientoOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    private async Task<(MovimientoInventarioDto?, string?)> LeerMovimientoOError(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(cancellationToken: ct);
                return (null, err?.Error ?? $"Error del servidor ({(int)resp.StatusCode}).");
            }
            catch { return (null, $"Error del servidor ({(int)resp.StatusCode})."); }
        }
        var mov = await resp.Content.ReadFromJsonAsync<MovimientoInventarioDto>(cancellationToken: ct);
        return (mov, null);
    }

    // ===================== Propuestas de envío =====================

    public async Task<(IReadOnlyList<PropuestaDto>? datos, string? error)> ListarPropuestasAsync(
        string? estatus = null, CancellationToken ct = default)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(estatus) ? "/api/propuestas" : $"/api/propuestas?estatus={Uri.EscapeDataString(estatus)}";
            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<List<PropuestaDto>>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }



    public async Task<(PropuestaDto? propuesta, string? error)> ConfirmarPropuestaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/propuestas/{id}/confirmar",
                new ResolverPropuestaRequest(Sesion?.IdUsuario), ct);
            return await LeerPropuestaOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(PropuestaDto? propuesta, string? error)> RechazarPropuestaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/propuestas/{id}/rechazar",
                new ResolverPropuestaRequest(Sesion?.IdUsuario), ct);
            return await LeerPropuestaOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    private async Task<(PropuestaDto?, string?)> LeerPropuestaOError(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(cancellationToken: ct);
                return (null, err?.Error ?? $"Error del servidor ({(int)resp.StatusCode}).");
            }
            catch { return (null, $"Error del servidor ({(int)resp.StatusCode})."); }
        }
        var p = await resp.Content.ReadFromJsonAsync<PropuestaDto>(cancellationToken: ct);
        return (p, null);
    }

    /// <summary>Lista simple de productos activos (para armar líneas de propuesta).</summary>
    public async Task<IReadOnlyList<ProductoDto>> ProductosActivosAsync(string? texto = null, CancellationToken ct = default)
    {
        var (datos, _) = await ListarProductosAsync(texto, incluirInactivos: false, pagina: 1, tamanoPagina: 200, ct);
        return datos?.Items ?? (IReadOnlyList<ProductoDto>)Array.Empty<ProductoDto>();
    }

    // ===================== Ventas (alta/detalle) =====================

    public async Task<IReadOnlyList<CatalogoItemDto>> CondicionesAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/api/catalogos/condiciones", ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<CatalogoItemDto>();
            return await resp.Content.ReadFromJsonAsync<List<CatalogoItemDto>>(cancellationToken: ct)
                ?? (IReadOnlyList<CatalogoItemDto>)Array.Empty<CatalogoItemDto>();
        }
        catch { return Array.Empty<CatalogoItemDto>(); }
    }
/*
    public async Task<(VentaCompletaDto? venta, string? error)> CrearVentaAsync(
        CrearVentaRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/ventas", req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(cancellationToken: ct);
                    return (null, err?.Error ?? $"Error del servidor ({(int)resp.StatusCode}).");
                }
                catch { return (null, $"Error del servidor ({(int)resp.StatusCode})."); }
            }
            var venta = await resp.Content.ReadFromJsonAsync<VentaCompletaDto>(cancellationToken: ct);
            return (venta, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }
*/
    // ===================== Catálogos administrables (zonas/rutas/cobradores/vendedores) =====================
    // Genérico: GET lista, POST crear, PUT actualizar, DELETE baja. TResp es el DTO devuelto.

    public async Task<(List<T>? datos, string? error)> AdminListarAsync<T>(
        string recurso, bool incluirInactivos = false, CancellationToken ct = default)
    {
        try
        {
            // 1. Mantenemos tu lógica exacta de construcción de parámetros de tu API original
            var inc = recurso == "zonas" ? "incluirInactivas" : "incluirInactivos";
            var url = $"/api/admin/{recurso}?{inc}={incluirInactivos.ToString().ToLowerInvariant()}";

            // 2. 🔥 CORRECCIÓN CRUCIAL: Creamos la petición inyectando el Bearer Token para Blazor Web
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");

            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");

            // Agregamos tolerancia a minúsculas por si la API responde en camelCase
            var opciones = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var datos = await resp.Content.ReadFromJsonAsync<List<T>>(opciones, cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) 
        { 
            return (null, "No se pudo conectar.\n" + ex.Message); 
        }
        catch (TaskCanceledException) 
        { 
            return (null, "La conexión tardó demasiado."); 
        }
    }

    private async Task<string?> ErrorSiFalla(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return null;
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return "Tu sesión expiró. Inicia sesión de nuevo.";
        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(cancellationToken: ct);
            return err?.Error ?? $"Error del servidor ({(int)resp.StatusCode}).";
        }
        catch { return $"Error del servidor ({(int)resp.StatusCode})."; }
    }

    /// <summary>Roles activos (para el combo del formulario de usuario).</summary>
    public async Task<IReadOnlyList<RolDto>> RolesAsync(CancellationToken ct = default)
    {
        var (datos, _) = await ListarRolesAdminAsync(incluirInactivos: false, ct);
        return datos ?? (IReadOnlyList<RolDto>)Array.Empty<RolDto>();
    }

    // ===================== Reportes =====================

    private async Task<(T? datos, string? error)> GetReporteAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (default, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (default, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (default, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (default, "La conexión tardó demasiado."); }
    }

    private static string Fecha(DateTimeOffset? d)
        => d is { } v ? Uri.EscapeDataString(v.ToString("O")) : "";

    public Task<(ReporteCarteraResponse? datos, string? error)> ReporteCarteraAsync(
        long? idZona = null, long? idRuta = null, long? idEstatusVenta = null, CancellationToken ct = default)
    {
        var url = "/api/reportes/cartera?_=1";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        if (idEstatusVenta is { } e) url += $"&idEstatusVenta={e}";
        return GetReporteAsync<ReporteCarteraResponse>(url, ct);
    }

    public Task<(ReportePagosResponse? datos, string? error)> ReportePagosAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idCobrador = null,
        long? idZona = null, long? idRuta = null, long? idMovimiento = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/pagos?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idCobrador is { } c) url += $"&idCobrador={c}";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        if (idMovimiento is { } m) url += $"&idMovimiento={m}";
        return GetReporteAsync<ReportePagosResponse>(url, ct);
    }

    public Task<(ReporteVentasResponse? datos, string? error)> ReporteVentasAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idVendedor = null,
        long? idZona = null, long? idRuta = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/ventas?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idVendedor is { } v) url += $"&idVendedor={v}";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        return GetReporteAsync<ReporteVentasResponse>(url, ct);
    }

    public Task<(ReporteRecuperacionResponse? datos, string? error)> ReporteRecuperacionAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta,
        long? idZona = null, long? idRuta = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/recuperacion?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        return GetReporteAsync<ReporteRecuperacionResponse>(url, ct);
    }

    public Task<(ReporteClientesResponse? datos, string? error)> ReporteClientesAsync(
        long? idRuta = null, long? idClasificacion = null, bool incluirInactivos = false,
        long? idZona = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/clientes?incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        if (idClasificacion is { } c) url += $"&idClasificacion={c}";
        if (idZona is { } z) url += $"&idZona={z}";
        return GetReporteAsync<ReporteClientesResponse>(url, ct);
    }

    public Task<(ReporteSeguimientoResponse? datos, string? error)> ReporteSeguimientoAsync(
        long? idZona = null, DateTimeOffset? altaDesde = null, DateTimeOffset? altaHasta = null, CancellationToken ct = default)
    {
        var url = "/api/reportes/seguimiento?_=1";
        if (idZona is { } z) url += $"&idZona={z}";
        if (altaDesde is { } d) url += $"&altaDesde={Fecha(d)}";
        if (altaHasta is { } h) url += $"&altaHasta={Fecha(h)}";
        return GetReporteAsync<ReporteSeguimientoResponse>(url, ct);
    }

    public Task<(DashboardResponse? datos, string? error)> DashboardAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idZona = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/dashboard?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idZona is { } z) url += $"&idZona={z}";
        return GetReporteAsync<DashboardResponse>(url, ct);
    }

    // ===================== Cancelaciones =====================

    public async Task<string?> CancelarPagoAsync(long idPago, CancellationToken ct = default)
    {
        try { return await ErrorSiFalla(await _http.DeleteAsync($"/api/pagos/{idPago}", ct), ct); }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    public async Task<string?> CancelarVentaAsync(long idVenta, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/ventas/{idVenta}/cancelar", content: null, ct);
            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }
    
    ////NUEVOS
    
    public async Task<string?> AdminBajaAsync(
        string recurso,
        long id,
        CancellationToken ct = default)
    {
        try
        {
            using var request = CrearPeticionAutenticada(
                HttpMethod.Delete,
                $"/api/admin/{recurso}/{id}");

            using var resp = await _http.SendAsync(request, ct);

            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return "No se pudo conectar.\n" + ex.Message;
        }
        catch (TaskCanceledException)
        {
            return "La conexión tardó demasiado.";
        }
    }
    public async Task<string?> AdminActualizarAsync(
        string recurso,
        long id,
        object body,
        CancellationToken ct = default)
    {
        try
        {
            using var request = CrearPeticionAutenticada(
                HttpMethod.Put,
                $"/api/admin/{recurso}/{id}");

            request.Content = JsonContent.Create(body);

            using var resp = await _http.SendAsync(request, ct);

            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return "No se pudo conectar.\n" + ex.Message;
        }
        catch (TaskCanceledException)
        {
            return "La conexión tardó demasiado.";
        }
    }
    
    public async Task<string?> AdminCrearAsync(
        string recurso,
        object body,
        CancellationToken ct = default)
    {
        try
        {
            using var request = CrearPeticionAutenticada(
                HttpMethod.Post,
                $"/api/admin/{recurso}");

            request.Content = JsonContent.Create(body);

            using var resp = await _http.SendAsync(request, ct);

            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return "No se pudo conectar.\n" + ex.Message;
        }
        catch (TaskCanceledException)
        {
            return "La conexión tardó demasiado.";
        }
    }
    
    
    
    // ===================== Cancelaciones =====================
    
  // =========================================================================
    // 🟢 MÉTODOS PÚBLICOS ESPECÍFICOS (Los que consumirá tu código de Blazor)
    // =========================================================================


    // 1. Instanciamos la opción Web de manera estática y correcta una sola vez
    private static readonly JsonSerializerOptions OpcionesWeb = new(JsonSerializerDefaults.Web);

    private async Task<(List<T>? datos, string? error)> GetListaGenéricaAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error); // 👈 Tipado correcto para tuplas con default

            // Usamos la propiedad estática OpcionesWeb corregida
            var datos = await resp.Content.ReadFromJsonAsync<List<T>>(OpcionesWeb, ct);
            return (datos ?? new(), null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada o expiró."); }
    }

    private async Task<(T? datos, string? error)> GetIndividualGenéricoAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound) return (default, null);

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var datos = await resp.Content.ReadFromJsonAsync<T>(OpcionesWeb, ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }

    private async Task<(TResponse? datos, string? error)> PostGenéricoAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Post, url);
            request.Content = JsonContent.Create(body);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.BadRequest)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(OpcionesWeb, ct);
                return (default, err?.Error ?? "Datos de petición inválidos.");
            }

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var datos = await resp.Content.ReadFromJsonAsync<TResponse>(OpcionesWeb, ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }

    private async Task<(TResponse? datos, string? error)> PutGenéricoAsync<TRequest, TResponse>(string url, TRequest body, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Put, url);
            request.Content = JsonContent.Create(body);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.BadRequest || resp.StatusCode == HttpStatusCode.NotFound)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(OpcionesWeb, ct);
                return (default, err?.Error ?? "No se pudo actualizar el registro.");
            }

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var datos = await resp.Content.ReadFromJsonAsync<TResponse>(OpcionesWeb, ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }

    private async Task<(T? datos, string? error)> DeleteGenéricoAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Delete, url);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorRespuesta>(OpcionesWeb, ct);
                return (default, err?.Error ?? "El registro no existe.");
            }

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var datos = await resp.Content.ReadFromJsonAsync<T>(OpcionesWeb, ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }


    private (bool exito, string? error) EvaluarEstatusComún(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            return (false, "Tu sesión expiró. Inicia sesión de nuevo.");
        if (resp.StatusCode == HttpStatusCode.Forbidden)
            return (false, "No tienes permisos para realizar esta acción.");
        if (!resp.IsSuccessStatusCode)
            return (false, $"Error del servidor ({(int)resp.StatusCode}).");return (true, null);
        
    }

    // =========================================================================
    // 🆕 Helpers para el sobre estándar ApiRespuesta<T>
    // Leen el envelope y extraen .Resultado; en error arman el mensaje a partir
    // de Mensaje + Detalles del propio sobre.
    // =========================================================================

    /// <summary>Extrae el mensaje de error legible de un sobre estándar de error.</summary>
    private static string MensajeDeSobre<T>(ApiRespuesta<T>? sobre, string porDefecto)
    {
        if (sobre is null) return porDefecto;
        var detalle = sobre.Detalles is { Count: > 0 }
            ? string.Join(" ", sobre.Detalles.Where(d => !string.IsNullOrWhiteSpace(d)))
            : null;
        return !string.IsNullOrWhiteSpace(detalle) ? detalle
            : !string.IsNullOrWhiteSpace(sobre.Mensaje) ? sobre.Mensaje
            : porDefecto;
    }

    private async Task<(List<T>? datos, string? error)> GetSobreListaAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<List<T>>>(OpcionesWeb, ct);
            return (sobre?.Resultado ?? new(), null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada o expiró."); }
    }

    private async Task<(T? datos, string? error)> GetSobreIndividualAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.NotFound) return (default, null);

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<T>>(OpcionesWeb, ct);
            return (sobre is not null ? sobre.Resultado : default, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }

    /// <summary>POST/PUT/DELETE que devuelven un sobre estándar con payload.</summary>
    private async Task<(T? datos, string? error)> EnviarSobreAsync<T>(
        HttpMethod metodo, string url, object? body, CancellationToken ct)
    {
        try
        {
            using var request = CrearPeticionAutenticada(metodo, url);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.NotFound
                or HttpStatusCode.Conflict)
            {
                var errSobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<T>>(OpcionesWeb, ct);
                return (default, MensajeDeSobre(errSobre, "No se pudo completar la operación."));
            }

            var (exito, error) = EvaluarEstatusComún(resp);
            if (!exito) return (default, error);

            var sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<T>>(OpcionesWeb, ct);
            return (sobre is not null ? sobre.Resultado : default, null);
        }
        catch (HttpRequestException ex) { return (default, $"Error de red: {ex.Message}"); }
        catch (OperationCanceledException) { return (default, "La operación fue cancelada."); }
    }
}
