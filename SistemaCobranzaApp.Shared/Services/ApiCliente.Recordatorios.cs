using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Cliente de recordatorios de cobranza por WhatsApp (envío manual confirmado por el usuario).
/// Consume el sobre estándar vía GetSobreIndividualAsync / EnviarSobreAsync.
/// </summary>
public partial class ApiCliente
{
    /// <summary>Lista los candidatos a recordatorio (clientes en atraso con preview del mensaje).</summary>
    public Task<(RecordatorioCandidatosResponse? datos, string? error)> ListarCandidatosRecordatorioAsync(
        long? idZona = null, long? idRuta = null, int diasAtrasoMinimo = 1, CancellationToken ct = default)
    {
        var url = $"/api/admin/whatsapp/recordatorios/?diasAtrasoMinimo={diasAtrasoMinimo}";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        return GetSobreIndividualAsync<RecordatorioCandidatosResponse>(url, ct);
    }

    /// <summary>Encola los recordatorios de los clientes seleccionados (el filtro se reaplica en el servidor).</summary>
    public Task<(EnviarRecordatoriosResponse? datos, string? error)> EnviarRecordatoriosAsync(
        EnviarRecordatoriosRequest request, long? idZona = null, long? idRuta = null, int diasAtrasoMinimo = 1,
        CancellationToken ct = default)
    {
        var url = $"/api/admin/whatsapp/recordatorios/enviar?diasAtrasoMinimo={diasAtrasoMinimo}";
        if (idZona is { } z) url += $"&idZona={z}";
        if (idRuta is { } r) url += $"&idRuta={r}";
        return EnviarSobreAsync<EnviarRecordatoriosResponse>(HttpMethod.Post, url, request, ct);
    }
}
