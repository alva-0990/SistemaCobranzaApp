using System.Net.Http.Headers;
using System.Net.Http.Json;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public class ApiCliente
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

    /// <summary>Sesión actual (token + datos del usuario) tras un login exitoso.</summary>
    public LoginResponse? Sesion { get; private set; }

    public bool Autenticado => Sesion is not null;

    // 2. NUEVO MÉTODO: Carga la sesión automáticamente al iniciar la app
    public async Task CargarSesionDesdeAlmacenamientoAsync()
    {
        try
        {
            var sesionGuardada = await _almacenamiento.ObtenerCadenaAsync(TokenKey);
            if (!string.IsNullOrEmpty(sesionGuardada))
            {
                Sesion = System.Text.Json.JsonSerializer.Deserialize<LoginResponse>(sesionGuardada);
            }
        }
        catch 
        { 
            // Previene cierres inesperados si no hay sesión guardada aún
        }
    }

    // 3. NUEVO MÉTODO INTERNO: Crea peticiones seguras e inyecta el Token de forma aislada
    private HttpRequestMessage CrearPeticionAutenticada(HttpMethod metodo, string url)
    {
        var request = new HttpRequestMessage(metodo, url);
        if (Sesion is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Sesion.Token);
        }
        return request;
    }

    /// <summary>
    /// Inicia sesión. Devuelve (true, respuesta) si es correcto, o (false, mensaje) si falla.
    /// </summary>
    public async Task<(bool ok, string? error)> LoginAsync(string usuario, string password, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(usuario, password), ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Usuario o contraseña incorrectos.");

            if (!resp.IsSuccessStatusCode)
                return (false, $"Error del servidor ({(int)resp.StatusCode}).");

            var login = await resp.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
            if (login is null)
                return (false, "Respuesta inválida del servidor.");

            Sesion = login;
            
            // MODIFICADO: Persistimos la sesión en el almacenamiento local seguro
            var jsonSesion = System.Text.Json.JsonSerializer.Serialize(login);
            await _almacenamiento.GuardarCadenaAsync(TokenKey, jsonSesion);

            return (true, null);
        }
        catch (HttpRequestException ex)
        {
            return (false, "No se pudo conectar con el servidor.\n" + ex.Message);
        }
        catch (TaskCanceledException)
        {
            return (false, "La conexión tardó demasiado. Intenta de nuevo.");
        }
    }

    // MODIFICADO: Ahora es asíncrono para limpiar de forma segura el almacenamiento
    public async Task CerrarSesionAsync()
    {
        Sesion = null;
        await _almacenamiento.EliminarCadenaAsync(TokenKey);
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

    // ===================== Productos =====================

    public async Task<(ProductosPaginaResponse? datos, string? error)> ListarProductosAsync(
        string? texto = null, bool incluirInactivos = false, int pagina = 1, int tamanoPagina = 50,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/productos?pagina={pagina}&tamanoPagina={tamanoPagina}" +
                      $"&incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
            if (!string.IsNullOrWhiteSpace(texto)) url += $"&texto={Uri.EscapeDataString(texto)}";

            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");

            var datos = await resp.Content.ReadFromJsonAsync<ProductosPaginaResponse>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(ProductoDto? producto, string? error)> CrearProductoAsync(
        CrearProductoRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/productos", req, ct);
            return await LeerProductoOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(ProductoDto? producto, string? error)> ActualizarProductoAsync(
        long id, ActualizarProductoRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"/api/productos/{id}", req, ct);
            return await LeerProductoOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(bool ok, string? error)> DarDeBajaProductoAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/productos/{id}", ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Tu sesión expiró. Inicia sesión de nuevo.");
            return (false, $"Error del servidor ({(int)resp.StatusCode}).");
        }
        catch (HttpRequestException ex) { return (false, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (false, "La conexión tardó demasiado."); }
    }

    private async Task<(ProductoDto?, string?)> LeerProductoOError(HttpResponseMessage resp, CancellationToken ct)
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
        var producto = await resp.Content.ReadFromJsonAsync<ProductoDto>(cancellationToken: ct);
        return (producto, null);
    }

    // ===================== Clientes (administración) =====================

    public async Task<(ClientesPaginaResponse? datos, string? error)> ListarClientesAsync(
        string? texto = null, long? idRuta = null, bool incluirInactivos = false,
        int pagina = 1, int tamanoPagina = 50, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/clientes?pagina={pagina}&tamanoPagina={tamanoPagina}" +
                      $"&incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
            if (!string.IsNullOrWhiteSpace(texto)) url += $"&texto={Uri.EscapeDataString(texto)}";
            if (idRuta is { } r) url += $"&idRuta={r}";

            var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");

            var datos = await resp.Content.ReadFromJsonAsync<ClientesPaginaResponse>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(ClienteDto? cliente, string? error)> CrearClienteAsync(
        CrearClienteRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/clientes", req, ct);
            return await LeerClienteOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(ClienteDto? cliente, string? error)> ActualizarClienteAsync(
        long id, ActualizarClienteRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"/api/clientes/{id}", req, ct);
            return await LeerClienteOError(resp, ct);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(bool ok, string? error)> DarDeBajaClienteAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/clientes/{id}", ct);
            if (resp.IsSuccessStatusCode) return (true, null);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Tu sesión expiró. Inicia sesión de nuevo.");
            return (false, $"Error del servidor ({(int)resp.StatusCode}).");
        }
        catch (HttpRequestException ex) { return (false, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (false, "La conexión tardó demasiado."); }
    }

    private async Task<(ClienteDto?, string?)> LeerClienteOError(HttpResponseMessage resp, CancellationToken ct)
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
        var cliente = await resp.Content.ReadFromJsonAsync<ClienteDto>(cancellationToken: ct);
        return (cliente, null);
    }

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

    public async Task<(PropuestaDto? propuesta, string? error)> ObtenerPropuestaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/propuestas/{id}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return (null, "No se encontró la propuesta.");
            if (!resp.IsSuccessStatusCode) return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var p = await resp.Content.ReadFromJsonAsync<PropuestaDto>(cancellationToken: ct);
            return (p, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<(PropuestaDto? propuesta, string? error)> CrearPropuestaAsync(
        CrearPropuestaRequest req, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/propuestas", req, ct);
            return await LeerPropuestaOError(resp, ct);
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

    // ===================== Catálogos administrables (zonas/rutas/cobradores/vendedores) =====================
    // Genérico: GET lista, POST crear, PUT actualizar, DELETE baja. TResp es el DTO devuelto.

    public async Task<(List<T>? datos, string? error)> AdminListarAsync<T>(
        string recurso, bool incluirInactivos = false, CancellationToken ct = default)
    {
        try
        {
            // 1. Mantenemos tu lógica exacta de construcción de parámetros de tu API original
            var inc = recurso == "zonas" ? "incluirInactivas" : "incluirInactivos";
            var url = $"api/admin/{recurso}?{inc}={incluirInactivos.ToString().ToLowerInvariant()}";

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


    public async Task<string?> AdminCrearAsync(string recurso, object body, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/admin/{recurso}", body, ct);
            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    public async Task<string?> AdminActualizarAsync(string recurso, long id, object body, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync($"/api/admin/{recurso}/{id}", body, ct);
            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    public async Task<string?> AdminBajaAsync(string recurso, long id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/admin/{recurso}/{id}", ct);
            return await ErrorSiFalla(resp, ct);
        }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
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

    // ===================== Usuarios =====================

    public async Task<(IReadOnlyList<UsuarioDto>? datos, string? error)> ListarUsuariosAsync(
        bool incluirInactivos = false, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/admin/usuarios?incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<List<UsuarioDto>>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
    }

    public async Task<string?> CrearUsuarioAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        try { return await ErrorSiFalla(await _http.PostAsJsonAsync("/api/admin/usuarios", req, ct), ct); }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    public async Task<string?> ActualizarUsuarioAsync(long id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        try { return await ErrorSiFalla(await _http.PutAsJsonAsync($"/api/admin/usuarios/{id}", req, ct), ct); }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    public async Task<string?> DarDeBajaUsuarioAsync(long id, CancellationToken ct = default)
    {
        try { return await ErrorSiFalla(await _http.DeleteAsync($"/api/admin/usuarios/{id}", ct), ct); }
        catch (HttpRequestException ex) { return "No se pudo conectar.\n" + ex.Message; }
        catch (TaskCanceledException) { return "La conexión tardó demasiado."; }
    }

    /// <summary>Roles activos (para el combo del formulario de usuario).</summary>
    public async Task<IReadOnlyList<RolDto>> RolesAsync(CancellationToken ct = default)
    {
        var (datos, _) = await AdminListarAsync<RolDto>("roles");
        return datos ?? (IReadOnlyList<RolDto>)Array.Empty<RolDto>();
    }

    // ===================== Reportes =====================

    private async Task<(T? datos, string? error)> GetReporteAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync(url, ct);
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

    private static string Fecha(DateTimeOffset? d) => d is { } v ? v.ToString("yyyy-MM-dd") : "";

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
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idCobrador = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/pagos?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idCobrador is { } c) url += $"&idCobrador={c}";
        return GetReporteAsync<ReportePagosResponse>(url, ct);
    }

    public Task<(ReporteVentasResponse? datos, string? error)> ReporteVentasAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, long? idVendedor = null, CancellationToken ct = default)
    {
        var url = $"/api/reportes/ventas?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
        if (idVendedor is { } v) url += $"&idVendedor={v}";
        return GetReporteAsync<ReporteVentasResponse>(url, ct);
    }

    public Task<(ReporteRecuperacionResponse? datos, string? error)> ReporteRecuperacionAsync(
        DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct = default)
        => GetReporteAsync<ReporteRecuperacionResponse>(
            $"/api/reportes/recuperacion?desde={Fecha(desde)}&hasta={Fecha(hasta)}", ct);

    public Task<(ReporteClientesResponse? datos, string? error)> ReporteClientesAsync(
        long? idRuta = null, long? idClasificacion = null, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var url = $"/api/reportes/clientes?incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        if (idClasificacion is { } c) url += $"&idClasificacion={c}";
        return GetReporteAsync<ReporteClientesResponse>(url, ct);
    }

    // ===================== Estado de cuenta =====================

    public async Task<(EstadoCuentaResponse? datos, string? error)> EstadoCuentaAsync(long idCliente, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"/api/clientes/{idCliente}/estado-cuenta", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (null, "No se encontró el cliente.");
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Tu sesión expiró. Inicia sesión de nuevo.");
            if (!resp.IsSuccessStatusCode)
                return (null, $"Error del servidor ({(int)resp.StatusCode}).");
            var datos = await resp.Content.ReadFromJsonAsync<EstadoCuentaResponse>(cancellationToken: ct);
            return (datos, null);
        }
        catch (HttpRequestException ex) { return (null, "No se pudo conectar.\n" + ex.Message); }
        catch (TaskCanceledException) { return (null, "La conexión tardó demasiado."); }
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
    
    
    
    
}