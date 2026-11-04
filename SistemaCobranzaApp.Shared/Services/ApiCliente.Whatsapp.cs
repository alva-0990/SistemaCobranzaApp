using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>
/// Métodos del cliente para consultar la cola de mensajes de WhatsApp (tabla de paso).
/// Consumen el sobre estándar <see cref="ApiRespuesta{T}"/> vía los helpers GetSobre*.
///   - Administración (/api/admin/whatsapp): solo lectura.
/// </summary>
public partial class ApiCliente
{
    /// <summary>Lista los mensajes de WhatsApp (más recientes primero) con filtros opcionales.</summary>
    public Task<(List<MensajeWhatsAppDto>? datos, string? error)> ListarMensajesWhatsAppAsync(
        string? estado = null, string? texto = null, int limite = 300, CancellationToken ct = default)
    {
        var url = $"/api/admin/whatsapp/mensajes?limite={limite}";
        if (!string.IsNullOrWhiteSpace(estado)) url += $"&estado={Uri.EscapeDataString(estado)}";
        if (!string.IsNullOrWhiteSpace(texto)) url += $"&texto={Uri.EscapeDataString(texto)}";
        return GetSobreListaAsync<MensajeWhatsAppDto>(url, ct);
    }
}
