using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el módulo de Clientes.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
///   - Operativo  (/api/clientes):        búsqueda, consultas por ruta/zona, estado de cuenta.
///   - Administración (/api/admin/clientes): CRUD.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (consulta) ----

    /// <summary>Busca clientes por texto (nombre, apellidos, teléfono, email, dirección, CP, ruta y zona).</summary>
    public Task<(List<ClienteDto>? datos, string? error)> BuscarClientesAsync(
        string? nombre = null, string? apPaterno = null, string? apMaterno = null, string? telefono = null,
        string? email = null, string? direccion = null, string? codigoPostal = null,
        string? ruta = null, string? zona = null, CancellationToken ct = default)
    {
        var qs = new List<string>();
        void Add(string clave, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
                qs.Add($"{clave}={Uri.EscapeDataString(valor)}");
        }

        Add("nombre", nombre);
        Add("apPaterno", apPaterno);
        Add("apMaterno", apMaterno);
        Add("telefono", telefono);
        Add("email", email);
        Add("direccion", direccion);
        Add("codigoPostal", codigoPostal);
        Add("ruta", ruta);
        Add("zona", zona);

        var url = "/api/clientes/buscar" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return GetSobreListaAsync<ClienteDto>(url, ct);
    }

    /// <summary>Clientes de una ruta (por Id de ruta).</summary>
    public Task<(ClientesPorRutaDto? datos, string? error)> ClientesPorRutaAsync(long idRuta, CancellationToken ct = default)
        => GetSobreIndividualAsync<ClientesPorRutaDto>($"/api/clientes/por-ruta/{idRuta}", ct);

    /// <summary>Clientes de una zona, agrupados por sus rutas (por Id de zona).</summary>
    public Task<(ClientesPorZonaDto? datos, string? error)> ClientesPorZonaAsync(long idZona, CancellationToken ct = default)
        => GetSobreIndividualAsync<ClientesPorZonaDto>($"/api/clientes/por-zona/{idZona}", ct);

    /// <summary>Estado de cuenta consolidado del cliente.</summary>
    public Task<(EstadoCuentaResponse? datos, string? error)> EstadoCuentaAsync(long idCliente, CancellationToken ct = default)
        => GetSobreIndividualAsync<EstadoCuentaResponse>($"/api/clientes/{idCliente}/estado-cuenta", ct);

    // ---- Administración (CRUD) ----

    /// <summary>Listado paginado con filtros.</summary>
    public Task<(ClientesPaginaResponse? datos, string? error)> ListarClientesAsync(
        string? texto = null, long? idRuta = null, bool incluirInactivos = false,
        int pagina = 1, int tamanoPagina = 50, CancellationToken ct = default)
    {
        var url = $"/api/admin/clientes?pagina={pagina}&tamanoPagina={tamanoPagina}" +
                  $"&incluirInactivos={incluirInactivos.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(texto)) url += $"&texto={Uri.EscapeDataString(texto)}";
        if (idRuta is { } r) url += $"&idRuta={r}";

        return GetSobreIndividualAsync<ClientesPaginaResponse>(url, ct);
    }

    /// <summary>Obtener cliente por Id.</summary>
    public Task<(ClienteDto? datos, string? error)> ObtenerClienteAsync(long id, CancellationToken ct = default)
        => GetSobreIndividualAsync<ClienteDto>($"/api/admin/clientes/{id}", ct);

    /// <summary>Crear cliente.</summary>
    public Task<(ClienteDto? datos, string? error)> CrearClienteAsync(CrearClienteRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ClienteDto>(HttpMethod.Post, "/api/admin/clientes", req, ct);

    /// <summary>Actualizar cliente.</summary>
    public Task<(ClienteDto? datos, string? error)> ActualizarClienteAsync(long id, ActualizarClienteRequest req, CancellationToken ct = default)
        => EnviarSobreAsync<ClienteDto>(HttpMethod.Put, $"/api/admin/clientes/{id}", req, ct);

    /// <summary>Actualizar la clasificación (y opcionalmente observaciones) de un cliente (reporte de seguimiento).</summary>
    public Task<(ClienteDto? datos, string? error)> ActualizarClasificacionClienteAsync(long id, long idClasificacion, string? observaciones = null, CancellationToken ct = default)
        => EnviarSobreAsync<ClienteDto>(HttpMethod.Put, $"/api/admin/clientes/{id}/clasificacion",
            new ActualizarClasificacionClienteRequest(idClasificacion, observaciones), ct);

    /// <summary>Baja lógica del cliente.</summary>
    public async Task<(bool ok, string? error)> DarDeBajaClienteAsync(long id, CancellationToken ct = default)
    {
        var (datos, error) = await EnviarSobreAsync<ClienteDto>(HttpMethod.Delete, $"/api/admin/clientes/{id}", null, ct);
        return (error is null && datos is not null, error);
    }
}
