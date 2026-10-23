using SistemaCobranzaApp.Shared.Dtos;
using System.Text.Json;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el módulo de Parámetros del Sistema (promociones y configuración).
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers GetSobre*/EnviarSobre*.
///   - Operativo (/api/parametros):        lectura (resolver promociones en pantalla).
///   - Administración (/api/admin/parametros): CRUD.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (lectura) ----

    /// <summary>Lista todos los parámetros del sistema (lectura).</summary>
    public Task<(List<ParametroSistemaDto>? datos, string? error)> ListarParametrosAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ParametroSistemaDto>("/api/parametros", ct);

    /// <summary>Obtiene un parámetro por su clave (lectura). Devuelve (null, null) si no existe.</summary>
    public Task<(ParametroSistemaDto? datos, string? error)> ObtenerParametroAsync(string clave, CancellationToken ct = default)
        => GetSobreIndividualAsync<ParametroSistemaDto>($"/api/parametros/{Uri.EscapeDataString(clave)}", ct);

    // ---- Administración (CRUD) ----

    /// <summary>Lista todos los parámetros para administración.</summary>
    public Task<(List<ParametroSistemaDto>? datos, string? error)> ListarParametrosAdminAsync(CancellationToken ct = default)
        => GetSobreListaAsync<ParametroSistemaDto>("/api/admin/parametros", ct);

    /// <summary>Obtiene un parámetro por su clave (administración).</summary>
    public Task<(ParametroSistemaDto? datos, string? error)> ObtenerParametroAdminAsync(string clave, CancellationToken ct = default)
        => GetSobreIndividualAsync<ParametroSistemaDto>($"/api/admin/parametros/{Uri.EscapeDataString(clave)}", ct);

    /// <summary>Crea un parámetro del sistema.</summary>
    public Task<(ParametroSistemaDto? datos, string? error)> CrearParametroAsync(CrearParametroSistemaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ParametroSistemaDto>(HttpMethod.Post, "/api/admin/parametros", req, ct);

    /// <summary>Actualiza un parámetro existente (la clave no cambia).</summary>
    public Task<(ParametroSistemaDto? datos, string? error)> ActualizarParametroAsync(string clave, ActualizarParametroSistemaRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ParametroSistemaDto>(HttpMethod.Put, $"/api/admin/parametros/{Uri.EscapeDataString(clave)}", req, ct);

    /// <summary>Elimina un parámetro del sistema (borrado físico).</summary>
    public Task<(ParametroSistemaDto? datos, string? error)> EliminarParametroAsync(string clave, CancellationToken ct = default)
        => EnviarSobreAsync<ParametroSistemaDto>(HttpMethod.Delete, $"/api/admin/parametros/{Uri.EscapeDataString(clave)}", null, ct);

    // ---- Formato del comprobante de pago (TICKET_PAGO_COMPACTO) ----
    // Clave de almacenamiento local para que el valor sobreviva reinicios sin internet.
    private const string TicketPagoCompactoKey = "ticket_pago_compacto";

    /// <summary>
    /// Lee el parámetro TICKET_PAGO_COMPACTO de la API y lo persiste localmente. Devuelve el valor
    /// resuelto (true = compacto). Si no hay conexión, conserva el último valor persistido.
    /// </summary>
    public async Task<bool> RefrescarFormatoTicketPagoAsync(CancellationToken ct = default)
    {
        var (param, error) = await ObtenerParametroAsync("TICKET_PAGO_COMPACTO", ct);
        if (error is not null)
            return await CargarFormatoTicketPagoAsync(); // sin red: usar el último guardado
        var compacto = string.Equals(param?.ValorTexto?.Trim(), "SI", StringComparison.OrdinalIgnoreCase);
        try { await _almacenamiento.GuardarCadenaAsync(TicketPagoCompactoKey, compacto ? "SI" : "NO"); }
        catch { /* la caché es opcional */ }
        return compacto;
    }

    /// <summary>Devuelve el último valor persistido del formato de ticket de pago (true = compacto).</summary>
    public async Task<bool> CargarFormatoTicketPagoAsync()
    {
        try
        {
            var v = await _almacenamiento.ObtenerCadenaAsync(TicketPagoCompactoKey);
            return string.Equals(v?.Trim(), "SI", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
