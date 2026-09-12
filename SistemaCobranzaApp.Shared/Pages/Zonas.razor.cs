using Microsoft.AspNetCore.Components;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Shared.Pages;

public class ZonasBase : ComponentBase
{
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected List<ZonaBackendDto> ZonasList { get; set; } = new();
    protected bool Ocupado { get; set; }
    protected string? MensajeExito { get; set; }
    protected string? MensajeError { get; set; }

    protected long? IdEditando { get; set; }
    protected string Descripcion { get; set; } = string.Empty;
    protected bool FormularioVisible { get; set; }
    protected long? IdExpandido { get; set; }

    public record ZonaBackendDto(long IdZona, string Descripcion);

    protected override Task OnInitializedAsync()
    {
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Api.CargarSesionDesdeAlmacenamientoAsync();
            if (!Api.Autenticado)
            {
                Navigation.NavigateTo("/login");
                return;
            }
            await CargarAsync();
            StateHasChanged();
        }
    }

    protected async Task CargarAsync()
    {
        if (Ocupado) return;
        Ocupado = true;
        MensajeError = null;
        
        try
        {
            var (datos, error) = await Api.AdminListarAsync<ZonaBackendDto>("zonas", false, default);
            if (error is not null)
            {
                MensajeError = error;
                return;
            }
            ZonasList = datos ?? new List<ZonaBackendDto>();
        }
        catch (Exception ex)
        {
            MensajeError = ex.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    protected void AlternarDetalle(long idZona)
    {
        IdExpandido = IdExpandido == idZona ? null : idZona;
    }

    protected void NuevaZona()
    {
        LimpiarFormulario();
        MensajeError = null;
        MensajeExito = null;
        FormularioVisible = true;
    }

    protected void Editar(ZonaBackendDto zona)
    {
        if (zona is null) return;
        IdEditando = zona.IdZona; 
        Descripcion = zona.Descripcion;
        FormularioVisible = true;
    }

    protected void Cancelar()
    {
        LimpiarFormulario();
        FormularioVisible = false;
    }

    protected void LimpiarFormulario()
    {
        IdEditando = null;
        Descripcion = string.Empty;
    }

    protected Task KeepGuardar() => Task.CompletedTask;
    protected Task DarDeBajaAsync(ZonaBackendDto zona) => Task.CompletedTask;
}
