using Microsoft.AspNetCore.Components;
using MudBlazor;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Shared.Pages;

/// <summary>
/// Consulta de la cola de mensajes de WhatsApp (tabla de paso colamensajeswhatsapp).
/// Muestra los mensajes encolados por ventas/pagos, su estado de envío, reintentos y el
/// error del último fallo. Solo lectura: el envío y la purga los hacen los workers de la API.
/// </summary>
public partial class MensajesWhatsApp : ComponentBase
{
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    /// <summary>Estados posibles (coinciden con el CHECK chk_estado_whatsapp).</summary>
    protected static readonly string[] Estados = { "Pendiente", "Procesando", "Enviado", "Fallido" };

    protected List<MensajeWhatsAppDto> Lista { get; set; } = new();
    protected bool Cargando { get; set; }
    protected string Busqueda { get; set; } = string.Empty;
    protected string? FiltroEstado { get; set; }
    protected long? Seleccionado { get; set; }

    protected IEnumerable<MensajeWhatsAppDto> Filtrados
    {
        get
        {
            IEnumerable<MensajeWhatsAppDto> q = Lista;
            if (FiltroEstado is not null)
                q = q.Where(m => m.Estado == FiltroEstado);

            var t = Busqueda?.Trim() ?? "";
            if (string.IsNullOrEmpty(t)) return q;
            return q.Where(m =>
                m.Telefono.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (m.Cliente ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)
                || (m.CodigoVenta ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)
                || m.Mensaje.Contains(t, StringComparison.OrdinalIgnoreCase)
                || (m.ErrorLog ?? "").Contains(t, StringComparison.OrdinalIgnoreCase));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Api.CargarSesionDesdeAlmacenamientoAsync();
        if (!Api.Autenticado)
        {
            Nav.NavigateTo("/login");
            return;
        }
        await CargarAsync();
    }

    protected async Task CargarAsync()
    {
        Cargando = true;
        var (datos, error) = await Api.ListarMensajesWhatsAppAsync();
        if (error is not null) Snackbar.Add(error, Severity.Error);
        Lista = datos ?? new();
        Cargando = false;
    }

    protected int Conteo(string estado) => Lista.Count(m => m.Estado == estado);

    protected void FiltrarEstado(string? estado)
        => FiltroEstado = FiltroEstado == estado ? null : estado;

    protected void AlternarDetalle(long id)
        => Seleccionado = Seleccionado == id ? null : id;

    protected static Color ColorEstado(string estado) => estado switch
    {
        "Enviado" => Color.Success,
        "Fallido" => Color.Error,
        "Procesando" => Color.Info,
        _ => Color.Warning
    };
}
