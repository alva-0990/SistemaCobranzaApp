using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para la administración de Usuarios.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
///
/// Las contraseñas viajan en los request de alta/edición pero NUNCA se registran
/// en logs (eso lo garantiza el servidor); el cliente solo las envía.
/// </summary>
public partial class ApiCliente
{
    public Task<(List<UsuarioDto>? datos, string? error)> ListarUsuariosAsync(
        bool incluirInactivos = false, CancellationToken ct = default)
        => GetSobreListaAsync<UsuarioDto>(
            incluirInactivos ? "/api/admin/usuarios?incluirInactivos=true" : "/api/admin/usuarios", ct);

    public Task<(UsuarioDto? datos, string? error)> ObtenerUsuarioAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<UsuarioDto>($"/api/admin/usuarios/{id}", ct);

    /// <summary>Crea un usuario. Devuelve el mensaje de error, o null si tuvo éxito.</summary>
    public async Task<string?> CrearUsuarioAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var (_, error) = await EnviarSobreAsync<UsuarioDto>(HttpMethod.Post, "/api/admin/usuarios", req, ct);
        return error;
    }

    /// <summary>Actualiza un usuario. Devuelve el mensaje de error, o null si tuvo éxito.</summary>
    public async Task<string?> ActualizarUsuarioAsync(long id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        var (_, error) = await EnviarSobreAsync<UsuarioDto>(HttpMethod.Put, $"/api/admin/usuarios/{id}", req, ct);
        return error;
    }

    /// <summary>Da de baja (lógica) un usuario. Devuelve el mensaje de error, o null si tuvo éxito.</summary>
    public async Task<string?> DarDeBajaUsuarioAsync(long id, CancellationToken ct = default)
    {
        var (_, error) = await EnviarSobreAsync<UsuarioDto>(HttpMethod.Delete, $"/api/admin/usuarios/{id}", null, ct);
        return error;
    }
}
