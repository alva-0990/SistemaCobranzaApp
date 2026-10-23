using Microsoft.AspNetCore.Components;
using MudBlazor;
using SistemaCobranzaApp.Shared.Components;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Shared.Pages;

/// <summary>
/// Administración de los parámetros del sistema (tabla ParametrosSistema).
/// Permite dar de alta, editar y eliminar parámetros de configuración/promociones
/// (p. ej. PROMO_PAGOS_CONSECUTIVOS_ACTIVA / PROMO_PAGOS_CONSECUTIVOS_META).
/// </summary>
public partial class Parametros : ComponentBase
{
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected IDialogService Dialogo { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    protected List<ParametroSistemaDto> Lista { get; set; } = new();
    protected bool Cargando { get; set; }
    protected string Busqueda { get; set; } = string.Empty;

    protected IEnumerable<ParametroSistemaDto> Filtrados
    {
        get
        {
            var q = Busqueda?.Trim() ?? "";
            if (string.IsNullOrEmpty(q)) return Lista;
            return Lista.Where(p =>
                p.Clave.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.Descripcion ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.ValorTexto ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.ValorNumerico?.ToString() ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
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

    private async Task CargarAsync()
    {
        Cargando = true;
        var (datos, error) = await Api.ListarParametrosAdminAsync();
        if (error is not null) Snackbar.Add(error, Severity.Error);
        Lista = datos ?? new();
        Cargando = false;
    }

    // ===================== Alta =====================

    protected async Task AbrirNuevo()
    {
        var modelo = new DialogoParametro.FormModel();
        var m = await MostrarDialogo("Nuevo parámetro", esNuevo: true, modelo);
        if (m is null) return;

        var req = new CrearParametroSistemaRequest(
            m.Clave.Trim(), m.Descripcion.Trim(), m.ValorNumerico, Limpiar(m.ValorTexto));

        var (_, error) = await Api.CrearParametroAsync(req);
        Notificar(error, $"Parámetro '{m.Clave.Trim().ToUpper()}' creado.");
        if (error is null) await CargarAsync();
    }

    // ===================== Edición =====================

    protected async Task AbrirEditar(ParametroSistemaDto p)
    {
        var modelo = new DialogoParametro.FormModel
        {
            Clave = p.Clave,
            Descripcion = p.Descripcion,
            ValorNumerico = p.ValorNumerico,
            ValorTexto = p.ValorTexto
        };
        var m = await MostrarDialogo($"Editar '{p.Clave}'", esNuevo: false, modelo);
        if (m is null) return;

        var req = new ActualizarParametroSistemaRequest(
            m.Descripcion.Trim(), m.ValorNumerico, Limpiar(m.ValorTexto));

        var (_, error) = await Api.ActualizarParametroAsync(p.Clave, req);
        Notificar(error, $"Parámetro '{p.Clave}' actualizado.");
        if (error is null) await CargarAsync();
    }

    // ===================== Eliminación =====================

    protected async Task Eliminar(ParametroSistemaDto p)
    {
        var confirmado = await ConfirmarDialogo(
            "Eliminar parámetro",
            $"¿Eliminar el parámetro '{p.Clave}'? Esta acción no se puede deshacer y puede afectar promociones del sistema.",
            "Sí, eliminar");
        if (!confirmado) return;

        var (_, error) = await Api.EliminarParametroAsync(p.Clave);
        Notificar(error, $"Parámetro '{p.Clave}' eliminado.");
        if (error is null) await CargarAsync();
    }

    // ===================== Helpers de diálogo =====================

    private async Task<DialogoParametro.FormModel?> MostrarDialogo(string titulo, bool esNuevo, DialogoParametro.FormModel modelo)
    {
        var opciones = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var parametros = new DialogParameters<DialogoParametro>
        {
            { x => x.Titulo, titulo },
            { x => x.EsNuevo, esNuevo },
            { x => x.Modelo, modelo }
        };
        var dialogo = await Dialogo.ShowAsync<DialogoParametro>(titulo, parametros, opciones);
        var resultado = await dialogo.Result;
        return resultado is { Canceled: false, Data: DialogoParametro.FormModel datos } ? datos : null;
    }

    private async Task<bool> ConfirmarDialogo(string titulo, string mensaje, string textoAceptar)
    {
        var opciones = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true };
        var parametros = new DialogParameters<DialogoConfirmacion>
        {
            { x => x.Titulo, titulo },
            { x => x.Mensaje, mensaje },
            { x => x.TextoAceptar, textoAceptar },
            { x => x.ColorAceptar, Color.Error },
            { x => x.Icono, Icons.Material.Filled.Warning },
            { x => x.ColorIcono, Color.Error }
        };
        var dialogo = await Dialogo.ShowAsync<DialogoConfirmacion>(titulo, parametros, opciones);
        var resultado = await dialogo.Result;
        return resultado is { Canceled: false, Data: true };
    }

    private void Notificar(string? error, string exito)
    {
        if (error is null) Snackbar.Add(exito, Severity.Success);
        else Snackbar.Add(error, Severity.Error);
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
