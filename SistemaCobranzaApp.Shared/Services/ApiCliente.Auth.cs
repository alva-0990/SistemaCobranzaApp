using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Autenticación y manejo de sesión del cliente:
/// login, cierre de sesión, carga de sesión persistida y creación de
/// peticiones autenticadas (inyección del token Bearer).
///
/// El login consume el sobre estándar <see cref="ApiRespuesta{LoginResponse}"/>.
/// </summary>
public partial class ApiCliente
{
    /// <summary>Sesión actual (token + datos del usuario) tras un login exitoso.</summary>
    public LoginResponse? Sesion { get; private set; }

    public bool Autenticado => Sesion is not null;

    /// <summary>
    /// Margen de seguridad para no dar por válido un token que está por expirar
    /// (evita que una operación iniciada justo en el límite llegue al servidor ya vencida).
    /// </summary>
    private static readonly TimeSpan MargenExpiracion = TimeSpan.FromSeconds(30);

    /// <summary>
    /// True si hay sesión y su token sigue vigente (no expirado). Base de la reanudación
    /// de sesión sin conexión (Opción A): permite entrar sin re-login mientras el JWT viva.
    /// </summary>
    public bool SesionVigente =>
        Sesion is { } s && s.Expira - MargenExpiracion > DateTimeOffset.UtcNow;

    /// <summary>
    /// Se dispara cada vez que cambia el estado de sesión (login, logout o carga desde
    /// almacenamiento). Los layouts/menús se suscriben para re-renderizarse y reflejar
    /// el estado autenticado — necesario en el WebView de Android, donde el layout no
    /// se recrea al navegar de /login a /.
    /// </summary>
    public event Action? SesionCambiada;

    private void NotificarSesionCambiada() => SesionCambiada?.Invoke();

    /// <summary>Carga la sesión persistida al iniciar la app (si existe).</summary>
    public async Task CargarSesionDesdeAlmacenamientoAsync()
    {
        try
        {
            var sesionGuardada = await _almacenamiento.ObtenerCadenaAsync(TokenKey);
            if (!string.IsNullOrEmpty(sesionGuardada))
                Sesion = JsonSerializer.Deserialize<LoginResponse>(sesionGuardada, OpcionesWeb);
        }
        catch
        {
            // Previene cierres inesperados si no hay sesión guardada aún.
        }
        NotificarSesionCambiada();
    }

    /// <summary>Cierra la sesión y limpia el almacenamiento local.</summary>
    public async Task CerrarSesionAsync()
    {
        Sesion = null;
        await _almacenamiento.EliminarCadenaAsync(TokenKey);
        NotificarSesionCambiada();
    }

    public Task<(UsuarioDto? datos, string? error)> ObtenerPerfilAsync(CancellationToken ct = default)
        => GetSobreIndividualAsync<UsuarioDto>("/api/auth/perfil", ct);

    public async Task<string?> ActualizarPerfilAsync(ActualizarPerfilRequest req, CancellationToken ct = default)
    {
        var (perfil, error) = await EnviarSobreAsync<UsuarioDto>(HttpMethod.Put, "/api/auth/perfil", req, ct);
        if (error is null && perfil is not null && Sesion is not null)
        {
            Sesion = Sesion with { Usuario = perfil.Usuario, NombreCompleto = perfil.NombreCompleto };
            await _almacenamiento.GuardarCadenaAsync(TokenKey, JsonSerializer.Serialize(Sesion));
            NotificarSesionCambiada();
        }
        return error;
    }

    public async Task<string?> ActualizarPasswordAsync(ActualizarPasswordRequest req, CancellationToken ct = default)
    {
        var (_, error) = await EnviarSobreAsync<UsuarioDto>(HttpMethod.Put, "/api/auth/password", req, ct);
        return error;
    }

    /// <summary>Crea una petición HTTP inyectando el token Bearer si hay sesión.</summary>
    private HttpRequestMessage CrearPeticionAutenticada(HttpMethod metodo, string url)
    {
        var request = new HttpRequestMessage(metodo, url);
        if (Sesion is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Sesion.Token);
        return request;
    }

    /// <summary>
    /// Inicia sesión. Devuelve (true, null) si es correcto, o (false, mensaje) si falla.
    /// Consume el sobre estándar ApiRespuesta&lt;LoginResponse&gt;.
    /// </summary>
    public async Task<(bool ok, string? error)> LoginAsync(string usuario, string password, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(usuario, password), ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var errSobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<LoginResponse>>(OpcionesWeb, ct);
                return (false, MensajeDeSobre(errSobre, "Usuario o contraseña incorrectos."));
            }

            if (!resp.IsSuccessStatusCode)
                return (false, $"Error del servidor ({(int)resp.StatusCode}).");

            var sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<LoginResponse>>(OpcionesWeb, ct);
            var login = sobre?.Resultado;
            if (login is null)
                return (false, "Respuesta inválida del servidor.");

            Sesion = login;

            // Persistimos la sesión en el almacenamiento local seguro.
            var jsonSesion = JsonSerializer.Serialize(login);
            await _almacenamiento.GuardarCadenaAsync(TokenKey, jsonSesion);

            NotificarSesionCambiada();
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

    /// <summary>
    /// Renueva el token de la sesión actual llamando al endpoint protegido /api/auth/refresh.
    /// Solo tiene efecto si hay una sesión con token aún vigente (el servidor exige un JWT
    /// válido). Se invoca automáticamente al recuperar señal para extender la ventana offline
    /// sin pedir credenciales. Silencioso: ante falta de red o error devuelve false sin tocar
    /// la sesión existente (los pendientes locales nunca se pierden por un refresh fallido).
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        // Sin sesión vigente no hay token válido que renovar; un token ya expirado sería
        // rechazado por el servidor (ValidateLifetime=true), así que ni lo intentamos.
        if (!SesionVigente) return false;

        try
        {
            using var request = CrearPeticionAutenticada(HttpMethod.Post, "/api/auth/refresh");
            using var resp = await _http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode) return false;

            var sobre = await resp.Content.ReadFromJsonAsync<ApiRespuesta<LoginResponse>>(OpcionesWeb, ct);
            var login = sobre?.Resultado;
            if (login is null) return false;

            Sesion = login;
            await _almacenamiento.GuardarCadenaAsync(TokenKey, JsonSerializer.Serialize(login));
            NotificarSesionCambiada();
            return true;
        }
        catch (HttpRequestException) { return false; }   // Sin conexión: conservar la sesión actual.
        catch (TaskCanceledException) { return false; }  // Timeout: idem.
    }
}
