using SistemaCobranzaApp.Shared.Dtos;
using System.Text.Json;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para el catálogo y configuración de la Empresa.
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers
/// GetSobre*/EnviarSobre* definidos en el núcleo de <see cref="ApiCliente"/>.
/// </summary>
public partial class ApiCliente
{
    // ---- Operativo (Información pública de la empresa) ----
    public async Task<(EmpresaDto? datos, string? error)> ObtenerEmpresaOperativaAsync(CancellationToken ct = default)
    {
        var resultado = await GetSobreIndividualAsync<EmpresaDto>("/api/empresa", ct);
        if (resultado.datos is { } empresa)
            await GuardarIdentidadEmpresaAsync(new(empresa.NombreComercial, empresa.NombreCorto,
                empresa.LogoUrl, empresa.LogoBlancoUrl));
        return resultado;
    }

    public Task<(IdentidadEmpresaDto? datos, string? error)> ObtenerIdentidadEmpresaAsync(CancellationToken ct = default)
        => GetSobreIndividualAsync<IdentidadEmpresaDto>("/api/empresa/identidad", ct);

    public async Task<IdentidadEmpresaDto?> CargarIdentidadEmpresaAsync()
    {
        try
        {
            var json = await _almacenamiento.ObtenerCadenaAsync("empresa_identidad");
            return json is null ? null : JsonSerializer.Deserialize<IdentidadEmpresaDto>(json);
        }
        catch { return null; }
    }

    public async Task GuardarIdentidadEmpresaAsync(IdentidadEmpresaDto identidad)
    {
        // La caché es opcional; un fallo local no debe impedir el acceso ni guardar la empresa.
        // IMPORTANTE: los logos pueden venir como data-URI base64 de ~50 KB cada uno. Persistir ese
        // blob en localStorage (y releerlo/deserializarlo en cada arranque sobre el circuito de
        // Blazor Server) congela el inicio de sesión. Cacheamos solo los textos ligeros; los logos
        // se obtienen de la API en vivo durante la sesión y no se guardan localmente cuando son
        // data-URI incrustados.
        try
        {
            var liviana = identidad with
            {
                LogoUrl = EsDataUri(identidad.LogoUrl) ? null : identidad.LogoUrl,
                LogoBlancoUrl = EsDataUri(identidad.LogoBlancoUrl) ? null : identidad.LogoBlancoUrl
            };
            await _almacenamiento.GuardarCadenaAsync("empresa_identidad", JsonSerializer.Serialize(liviana));
        }
        catch { }
    }

    // Un logo incrustado como data-URI (p. ej. "data:image/png;base64,....") es demasiado pesado
    // para persistirlo en el almacenamiento local; una URL normal sí es ligera y se conserva.
    private static bool EsDataUri(string? valor)
        => !string.IsNullOrWhiteSpace(valor)
           && valor.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    // ---- Administración ----
    public Task<(EmpresaDto? datos, string? error)> ObtenerEmpresaAdminAsync(int id, CancellationToken ct = default)
        => GetSobreIndividualAsync<EmpresaDto>($"/api/admin/empresa/{id}", ct);

    public Task<(EmpresaDto? datos, string? error)> CrearEmpresaAsync(CrearEmpresaRequest solicitud, CancellationToken ct = default)
        => EnviarSobreAsync<EmpresaDto>(HttpMethod.Post, "/api/admin/empresa", solicitud, ct);

    public Task<(EmpresaDto? datos, string? error)> ActualizarEmpresaAsync(int id, ActualizarEmpresaRequest solicitud, CancellationToken ct = default)
        => EnviarSobreAsync<EmpresaDto>(HttpMethod.Put, $"/api/admin/empresa/{id}", solicitud, ct);
}