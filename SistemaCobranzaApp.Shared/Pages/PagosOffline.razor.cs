using SistemaCobranzaApp.Domain.Comun;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Microsoft.JSInterop;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Shared.Pages;

/// <summary>
/// Code-behind de la vista de Pagos.
///
/// Buscador inteligente con tres criterios de entrada:
///   1. IdCliente  (texto numérico): carga directamente el estado de cuenta del cliente.
///   2. IdVenta    (texto numérico): resuelve la venta, obtiene su cliente y preselecciona esa venta.
///   3. Nombre/Apellidos (texto): lista los clientes que coinciden para que el usuario elija uno.
///
/// Al tener un cliente seleccionado se muestra:
///   - Datos del cliente.
///   - Tabla de ventas (estado de cuenta) con selección de la venta a inspeccionar.
///   - Datos de la venta y su producto (detalle de artículos).
///   - Tabla de pagos (historial de abonos de la venta).
///   - Plan de cuotas (tabla de amortización) y planes promocionales.
///   - Resumen financiero de la venta.
/// </summary>
public partial class PagosOffline : ComponentBase, IAsyncDisposable
{
    [Inject] protected ServicioJornadaLocal Jornada { get; set; } = default!;
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IDialogService Dialogo { get; set; } = default!;
    [Inject] protected IFormFactor FormFactor { get; set; } = default!;
    [Inject] protected ServicioImpresionTicket ImpresionTicket { get; set; } = default!;
    [Inject] protected EstadoEmpresaGlobal EmpresaGlobal { get; set; } = default!;

    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected IPermisosCamara PermisosCamara { get; set; } = default!;

    protected bool ModoJornada => Jornada.Disponible && Jornada.EsUsuarioCampo;
    private bool _guardandoPago;
    protected Components.ImpresionJornada? _impresion;

    // ---- Estado del buscador (dos pestañas: por cliente y por venta) ----
    // Pestaña 1: Id del cliente o nombre/apellidos.
    protected string BusquedaCliente { get; set; } = string.Empty;
    // Pestaña 2: Id de la venta.
    protected string BusquedaVenta { get; set; } = string.Empty;
    protected bool Cargando { get; set; }
    protected bool HaBuscado { get; set; }
    // Texto del último criterio buscado (para los mensajes de "sin resultados").
    protected string UltimaBusqueda { get; set; } = string.Empty;
    // Indica si la búsqueda activa fue por VENTA (modo enfocado: solo esa venta, sin resumen ni lista de ventas).
    protected bool BusquedaPorVenta { get; set; }

    // ---- Escáner de QR (lectura del folio de la venta por cámara) ----
    protected bool EsMovil => EsEntornoMovil;
    protected bool QrEscaneando { get; set; }
    protected string? QrError { get; set; }
    private IJSObjectReference? _qrModulo;
    private DotNetObjectReference<PagosOffline>? _qrRef;
    private const string QrVideoId = "pagos-qr-video";

    // Resultados de la búsqueda por nombre/apellidos (cuando hay varios candidatos).
    protected List<ClienteDto> ClientesEncontrados { get; set; } = new();

    // ---- Cliente seleccionado y su estado de cuenta ----
    protected ClienteDto? Cliente { get; set; }
    protected EstadoCuentaResponse? EstadoCuenta { get; set; }

    // ---- Venta seleccionada (detalle completo + pagos) ----
    protected long? IdVentaSeleccionada { get; set; }
    protected VentaDto? Venta { get; set; }
    protected List<PagoHistoricoDto> PagosVenta { get; set; } = new();
    protected bool CargandoVenta { get; set; }

    // Ventas del cliente con saldo pendiente (para la vista intermedia de selección
    // cuando el cliente tiene más de una venta activa con saldo).
    protected List<EstadoCuentaVentaDto> VentasConSaldo { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await Api.CargarSesionDesdeAlmacenamientoAsync();
        if (!Api.Autenticado) { Nav.NavigateTo("/login"); return; }
        await Jornada.CargarAsync();
        if (!Jornada.Disponible || Api.Sesion?.IdRol != 4 || Jornada.Estado.Descarga is null)
        { Nav.NavigateTo("/jornada"); return; }
        Jornada.Cambio += AlCambiarJornada;
    }

    // Lee de ParametrosSistema si la promoción de racha está activa y su meta de pagos.


    // ===================== Buscador: pestaña por CLIENTE (Id o nombre/apellidos) =====================

    // Dispara la búsqueda por cliente desde el botón o la tecla Enter de la pestaña.
    protected async Task BuscarClienteAsync()
    {
        var texto = BusquedaCliente?.Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            Limpiar();
            return;
        }

        Cargando = true;
        HaBuscado = true;
        UltimaBusqueda = texto;
        BusquedaPorVenta = false;
        ClientesEncontrados = new();
        Cliente = null;
        EstadoCuenta = null;
        LimpiarVenta();

        try
        {
            // Si el texto es un número, se trata como Id del cliente; si no, como nombre/apellidos.
            if (long.TryParse(texto, out var idCliente))
                await BuscarClientePorIdAsync(idCliente);
            else
                await BuscarPorNombreAsync(texto);
        }
        finally
        {
            Cargando = false;
        }
    }

    // Intento directo por IdCliente.
    private async Task BuscarClientePorIdAsync(long idCliente)
    {
        if (Jornada.ClientesDisponibles().Any(c => c.IdCliente == idCliente)) await SeleccionarClienteAsync(idCliente);
    }

    // Búsqueda por nombre/apellidos/teléfono. Usa el mismo listado administrativo que la pantalla
    // de Clientes (texto único con OR en nombre/apellidos/teléfono) e incluye inactivos para cobranza.
    private async Task BuscarPorNombreAsync(string texto)
    {
        ClientesEncontrados = Jornada.BuscarClientes(texto);
        if (ClientesEncontrados.Count == 1) await SeleccionarClienteAsync(ClientesEncontrados[0].IdCliente);
    }

    // ===================== Buscador: pestaña por VENTA (Id de venta) =====================

    // Dispara la búsqueda por Id de venta: resuelve la venta, su cliente y preselecciona esa venta.
    protected async Task BuscarVentaAsync()
    {
        var texto = NormalizarCodigoVenta(BusquedaVenta);
        if (string.IsNullOrWhiteSpace(texto)) { Limpiar(); return; }
        HaBuscado = true;
        UltimaBusqueda = texto;
        BusquedaPorVenta = true;
        ClientesEncontrados = new();
        Cliente = null; EstadoCuenta = null; LimpiarVenta();
        var venta = Jornada.VentasDisponibles().FirstOrDefault(v => v.IdVenta.ToString() == texto ||
            string.Equals(v.CodigoVenta, texto, StringComparison.OrdinalIgnoreCase));
        if (venta is not null) await SeleccionarClienteAsync(venta.IdCliente, venta.IdVenta);
    }

    // ===================== Selección de cliente =====================

    // Carga el cliente y su estado de cuenta. Opcionalmente preselecciona una venta.
    protected async Task SeleccionarClienteAsync(long idCliente, long? idVentaPreseleccionada = null)
    {
        ClientesEncontrados = new(); EstadoCuenta = null; LimpiarVenta();
        Cliente = Jornada.ClientesDisponibles().FirstOrDefault(c => c.IdCliente == idCliente);
        if (Cliente is null) return;
        CargarEstadoLocal();
        if (idVentaPreseleccionada is { } id) await SeleccionarVentaAsync(id);
        else if (VentasConSaldo.Count == 1) await SeleccionarVentaAsync(VentasConSaldo[0].IdVenta);
    }

    // ===================== Selección de venta =====================

    protected Task SeleccionarVentaAsync(long idVenta)
    {
        IdVentaSeleccionada = idVenta;
        CargarVentaLocal(idVenta);
        return Task.CompletedTask;
    }

    protected Task OnFilaVentaClick(TableRowClickEventArgs<EstadoCuentaVentaDto> e)
        => e.Item is null ? Task.CompletedTask : SeleccionarVentaAsync(e.Item.IdVenta);

    private void CargarEstadoLocal()
    {
        if (Cliente is null || Jornada.Estado.Descarga is not { } d) return;
        var ventas = d.Ventas.Where(v => v.IdCliente == Cliente.IdCliente).Select(v =>
            new EstadoCuentaVentaDto(v.IdVenta, v.CodigoVenta, v.TotalFinal, v.Enganche,
                v.TotalAbonado + v.SaldoPendiente - Jornada.SaldoDisponible(v), Jornada.SaldoDisponible(v),
                v.DescripcionEstatus, v.FechaCompra, v.FechaUltimoPago)).ToList();
        EstadoCuenta = new(Cliente.IdCliente, Cliente.NombreCompleto, Cliente.Ruta, Api.Sesion?.Zona,
            ventas.Sum(v => v.TotalFinal), ventas.Sum(v => v.TotalAbonos), ventas.Sum(v => v.Saldo), ventas, []);
        VentasConSaldo = ventas.Where(v => v.Saldo > 0).ToList();
    }

    private void CargarVentaLocal(long idVenta)
    {
        var d = Jornada.Estado.Descarga;
        var original = d?.Ventas.FirstOrDefault(v => v.IdVenta == idVenta && v.IdCliente == Cliente?.IdCliente);
        if (original is null) { Venta = null; PagosVenta = new(); return; }
        var saldo = Jornada.SaldoDisponible(original);
        Venta = original with { SaldoPendiente = saldo, TotalAbonado = original.TotalAbonado + original.SaldoPendiente - saldo };
        PagosVenta = d!.Pagos.Where(p => p.IdVenta == idVenta).OrderByDescending(p => p.FechaPago)
            .Select(p => new PagoHistoricoDto(p.IdPago, p.FolioPago, p.ConsecutivoPago, p.MontoAbona,
                p.SaldoAnterior, p.SaldoActual, p.IdCobrador, p.Cobrador, p.IdMovimiento ?? 0,
                p.Movimiento, p.Observaciones, p.FechaPago, null, null)).ToList();
        foreach (var o in Jornada.Estado.Operaciones.Where(o => o.Aplicada && o.Pago?.IdVenta == idVenta && o.PagoCentral is not null))
        {
            var p = o.PagoCentral!;
            if (PagosVenta.Any(h => h.IdPago == p.IdPago)) continue;
            PagosVenta.Add(new(p.IdPago, p.FolioPago, p.ConsecutivoPago, p.MontoAbona,
                p.SaldoAnterior, p.SaldoActual, o.Pago!.IdCobrador, Api.Sesion?.NombreCompleto,
                1, "ABONO", o.Pago.Observaciones, o.Pago.FechaPago ?? o.FechaCaptura, null, null));
        }
        PagosVenta = PagosVenta.OrderByDescending(p => p.FechaPago).ToList();
    }

    private void AlCambiarJornada() => _ = InvokeAsync(() =>
    {
        CargarEstadoLocal();
        if (IdVentaSeleccionada is { } id) CargarVentaLocal(id);
        StateHasChanged();
    });

    // ===================== Helpers =====================

    protected void NuevaBusqueda()
    {
        BusquedaCliente = string.Empty;
        BusquedaVenta = string.Empty;
        Limpiar();
    }

    // Tras un pago exitoso espera 5 s (para ver el resultado / terminar la impresión)
    // y regresa automáticamente al buscador para agilizar el siguiente cobro.
    private async Task ProgramarRegresoBusquedaAsync()
    {
        Snackbar.Add("Regresando a la búsqueda en 5 segundos…", Severity.Info);
        await Task.Delay(TimeSpan.FromSeconds(5));
        Regresar();
        StateHasChanged();
    }

    // Regresa del detalle al buscador (misma UX que la pantalla de Clientes).
    protected void Regresar()
    {
        BusquedaCliente = string.Empty;
        BusquedaVenta = string.Empty;
        Limpiar();
    }

    // Enter en la pestaña de cliente dispara la búsqueda por cliente.
    protected async Task OnKeyUpCliente(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter")
            await BuscarClienteAsync();
    }

    // Enter en la pestaña de venta dispara la búsqueda por venta.
    protected async Task OnKeyUpVenta(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter")
            await BuscarVentaAsync();
    }

    private void Limpiar()
    {
        HaBuscado = false;
        UltimaBusqueda = string.Empty;
        BusquedaPorVenta = false;
        ClientesEncontrados = new();
        Cliente = null;
        EstadoCuenta = null;
        VentasConSaldo = new();
        LimpiarVenta();
    }

    private void LimpiarVenta()
    {
        IdVentaSeleccionada = null;
        Venta = null;
        PagosVenta = new();
    }

    protected static string Mostrar(string? valor) => string.IsNullOrWhiteSpace(valor) ? "—" : valor!;

    // Normaliza el código de venta tecleado o escaneado. Algunos lectores de código de barras,
    // por el layout de teclado, mapean el guión '-' a otros caracteres (apóstrofe, backtick, etc.).
    // Convertimos esos separadores a '-' para que el código coincida con el almacenado.
    private static string NormalizarCodigoVenta(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        if (t.Length == 0) return t;

        // Reemplaza separadores "equivocados" por guión. No tocamos letras/dígitos.
        var sb = new System.Text.StringBuilder(t.Length);
        foreach (var ch in t)
        {
            sb.Append(ch switch
            {
                '\'' or '`' or '´' or '’' or '‘' or '/' or '_' or ' ' => '-',
                _ => ch
            });
        }
        return sb.ToString();
    }

    // Color del chip de estatus de una cuota.
    protected static Color ColorCuota(string? estatus) => estatus switch
    {
        "PAGADA" => Color.Success,
        "VENCIDA" => Color.Error,
        _ => Color.Warning // PENDIENTE
    };

    // ===================== Registro de pago =====================

    // Indica si la venta seleccionada admite registrar un pago (tiene saldo pendiente).
    protected bool PuedeRegistrarPago => Venta is not null && Venta.SaldoPendiente > 0 && (!ModoJornada || Api.Sesion?.IdRol == 4) && !_guardandoPago;

    // Abre el diálogo de registro de pago con el contexto de la venta y sus promociones,
    // y refresca la vista al confirmarse (para reflejar las filas automáticas que inyecta el trigger).
    protected async Task AbrirDialogoPago()
    {
        if (!PuedeRegistrarPago || Venta is null || Jornada.Estado.Descarga?.Asignacion.IdCobrador is not { } cob) return;
        // Planes de liquidación vigentes de la venta (para la opción de pronto pago en el diálogo).
        var planesVigentes = (Venta.PlanesPromocionales ?? new())
            .Where(p => p.Activo)
            .OrderBy(p => p.NumeroMeses)
            .ToList();
        var parametros = new DialogParameters<Components.DialogoRegistrarPagoOffline>
        {
            { x => x.SoloAbono, false }, { x => x.PlanesLiquidacion, planesVigentes },
            { x => x.IdVenta, Venta.IdVenta }, { x => x.IdCliente, Venta.IdCliente },
            { x => x.Cliente, Venta.NombreCliente }, { x => x.CodigoVenta, Venta.CodigoVenta },
            { x => x.SaldoPendiente, Venta.SaldoPendiente }, { x => x.CuotaFija, Venta.MontoCuotaFija },
            { x => x.Cobradores, new List<CobradorDto> { new(cob, Api.Sesion!.NombreCompleto, Api.Sesion.IdZona,
                Api.Sesion.Zona, Api.Sesion.IdUsuario, Api.Sesion.Usuario, true) } },
            { x => x.IdCobradorSugerido, cob }, { x => x.IdUsuarioRegistro, Api.Sesion!.IdUsuario }
        };
        var dialogo = await Dialogo.ShowAsync<Components.DialogoRegistrarPagoOffline>("Registrar pago sin conexión", parametros,
            new DialogOptions { FullScreen = true, FullWidth = true, CloseButton = true });
        if (await dialogo.Result is { Canceled: false, Data: ResultadoDialogoPago intencion })
            await ProcesarPagoAsync(intencion);
    }

    private async Task ProcesarPagoAsync(ResultadoDialogoPago i)
    {
        if (_guardandoPago || Venta is null || Cliente is null) return;
        _guardandoPago = true;
        try
        {
            // Según el modo elegido en el diálogo se registra: un abono normal, una liquidación por
            // plan (abono con marca de pronto pago) o una liquidación por acuerdo (abono + ajuste).
            var uuid = i.Modo switch
            {
                ModoPagoDialogo.LiquidacionPlan =>
                    await Jornada.GuardarLiquidacionPlanAsync(Venta.IdVenta, i.Monto, i.Observaciones),
                ModoPagoDialogo.LiquidacionAcuerdo =>
                    await Jornada.GuardarLiquidacionAcuerdoAsync(Venta.IdVenta, i.Monto, i.Observaciones),
                _ =>
                    await Jornada.GuardarPagoAsync(Venta.IdVenta, i.Monto, i.Observaciones)
            };
            Snackbar.Add(i.Modo == ModoPagoDialogo.Abono
                ? "Pago guardado en dispositivo."
                : "Liquidación guardada en dispositivo. ¡La cuenta quedó liquidada!", Severity.Success);
            await SeleccionarClienteAsync(Cliente.IdCliente, Venta.IdVenta);
            await _impresion!.ImprimirAsync(uuid);
            // Tras guardar el pago y cerrar la impresión del ticket, esperamos 5 s y
            // regresamos al buscador para agilizar el siguiente cobro.
            await ProgramarRegresoBusquedaAsync();
        }
        catch (Exception ex) { Snackbar.Add(ex.Message, Severity.Error); }
        finally { _guardandoPago = false; }
    }

    // Determina si la app corre en un entorno móvil (no Web ni Desktop) para ofrecer la impresión Bluetooth.
    private bool EsEntornoMovil
    {
        get
        {
            var ff = FormFactor.GetFormFactor();
            return !ff.Equals("Web", StringComparison.OrdinalIgnoreCase)
                   && !ff.Equals("Desktop", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ===================== Escáner de QR =====================

    // Inicia la cámara y la detección de QR. El módulo JS notifica el folio vía OnQrDetectado.
    protected async Task IniciarEscaneoQrAsync()
    {
        QrError = null;
        try
        {
            // Pedimos el permiso de cámara en runtime (Android). En Web el navegador lo gestiona.
            if (!await PermisosCamara.SolicitarAsync())
            {
                QrError = "Se requiere el permiso de cámara para escanear el QR. Concédelo en los ajustes de la app.";
                return;
            }

            _qrModulo ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/SistemaCobranzaApp.Shared/qr-scanner.js");

            var soportado = await _qrModulo.InvokeAsync<bool>("soportado");
            if (!soportado)
            {
                QrError = "Este dispositivo no permite leer códigos QR desde la app. Usa la búsqueda por Id de venta.";
                return;
            }

            _qrRef ??= DotNetObjectReference.Create(this);
            var r = await _qrModulo.InvokeAsync<ResultadoJs>("iniciar", QrVideoId, _qrRef);
            if (!r.Ok)
            {
                QrError = r.Error ?? "No se pudo iniciar la cámara.";
                QrEscaneando = false;
                return;
            }

            QrEscaneando = true;
        }
        catch (Exception ex)
        {
            QrError = $"No se pudo iniciar el escáner: {ex.Message}";
            QrEscaneando = false;
        }
    }

    // Detiene la cámara y libera recursos del escáner.
    protected async Task DetenerEscaneoQrAsync()
    {
        QrEscaneando = false;
        if (_qrModulo is not null)
        {
            try { await _qrModulo.InvokeVoidAsync("detener"); }
            catch { /* el módulo pudo no estar activo */ }
        }
    }

    // Callback invocado desde JS al detectar un QR. El valor es el folio/código de la venta.
    [JSInvokable]
    public async Task OnQrDetectado(string valor)
    {
        await DetenerEscaneoQrAsync();

        if (string.IsNullOrWhiteSpace(valor))
            return;

        // Reutilizamos el flujo de búsqueda por venta con el código leído del QR.
        BusquedaVenta = valor.Trim();
        await BuscarVentaAsync();
        StateHasChanged();
    }

    // Resultado de las funciones JS iniciar().
    private sealed record ResultadoJs(bool Ok, string? Error);

    public async ValueTask DisposeAsync()
    {
        Jornada.Cambio -= AlCambiarJornada;
        try
        {
            if (_qrModulo is not null)
            {
                await _qrModulo.InvokeVoidAsync("detener");
                await _qrModulo.DisposeAsync();
            }
        }
        catch { /* ignorar errores de limpieza */ }
        finally
        {
            _qrRef?.Dispose();
        }
    }
}
