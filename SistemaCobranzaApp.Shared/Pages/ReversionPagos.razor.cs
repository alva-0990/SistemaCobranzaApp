using SistemaCobranzaApp.Domain.Comun;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Microsoft.JSInterop;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Shared.Pages;

/// <summary>
/// Code-behind de la vista de AUDITORÍA de reversión de pagos (/reversion-pagos).
///
/// Clon de la vista de Pagos usado para auditar y corregir el último abono físico
/// de una venta. En lugar de registrar dinero nuevo, permite:
///   - Revertir por completo el pago más reciente (si es dinero real en caja).
///   - Editar el pago más reciente: reversa el abono original y re-inserta el corregido.
///
/// Reutiliza el mismo buscador inteligente de la vista de Pagos:
///   1. IdCliente  (texto numérico): carga directamente el estado de cuenta del cliente.
///   2. IdVenta    (texto numérico): resuelve la venta, obtiene su cliente y preselecciona esa venta.
///   3. Nombre/Apellidos (texto): lista los clientes que coinciden para que el usuario elija uno.
/// </summary>
public partial class ReversionPagos : ComponentBase, IAsyncDisposable
{
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IDialogService Dialogo { get; set; } = default!;
    [Inject] protected IFormFactor FormFactor { get; set; } = default!;
    [Inject] protected ServicioImpresionTicket ImpresionTicket { get; set; } = default!;
    [Inject] protected EstadoEmpresaGlobal EmpresaGlobal { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected IPermisosCamara PermisosCamara { get; set; } = default!;

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

    // Cobradores para el combo del diálogo de edición (lista general de respaldo).
    protected List<CobradorDto> Cobradores { get; set; } = new();

    // Parámetros de la promoción de racha de pagos (se leen de ParametrosSistema).
    protected bool PromoRachaActiva { get; set; }
    protected int MetaRacha { get; set; } = 5;

    // ---- Escáner de QR (lectura del folio de la venta por cámara) ----
    protected bool EsMovil => EsEntornoMovil;
    protected bool QrEscaneando { get; set; }
    protected string? QrError { get; set; }
    private IJSObjectReference? _qrModulo;
    private DotNetObjectReference<ReversionPagos>? _qrRef;
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
        if (!Api.Autenticado)
        {
            Nav.NavigateTo("/login");
            return;
        }

        // Cobradores activos para el diálogo de edición de pago (lista general de respaldo).
        var (cobradores, _) = await Api.ListarCobradoresOperativosAsync();
        Cobradores = cobradores ?? new();

        // Parámetros reales de la promoción de racha de pagos (ParametrosSistema).
        await CargarPromocionAsync();
    }

    // Lee de ParametrosSistema si la promoción de racha está activa y su meta de pagos.
    private async Task CargarPromocionAsync()
    {
        var (activa, _) = await Api.ObtenerParametroAsync("PROMO_PAGOS_CONSECUTIVOS_ACTIVA");
        PromoRachaActiva = string.Equals(activa?.ValorTexto?.Trim(), "SI", StringComparison.OrdinalIgnoreCase);

        var (meta, _) = await Api.ObtenerParametroAsync("PROMO_PAGOS_CONSECUTIVOS_META");
        if (meta?.ValorNumerico is { } v && v > 0)
            MetaRacha = (int)v;
    }

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
        var (cliente, error) = await Api.ObtenerClienteAsync(idCliente);
        if (cliente is not null)
        {
            await SeleccionarClienteAsync(cliente.IdCliente);
            return;
        }
        if (error is not null) Snackbar.Add(error, Severity.Warning);
    }

    // Búsqueda por nombre/apellidos/teléfono. Usa el mismo listado administrativo que la pantalla
    // de Clientes (texto único con OR en nombre/apellidos/teléfono) e incluye inactivos para cobranza.
    private async Task BuscarPorNombreAsync(string texto)
    {
        var (pagina, error) = await Api.ListarClientesAsync(
            texto: texto, incluirInactivos: true, pagina: 1, tamanoPagina: 200);

        if (error is not null)
        {
            Snackbar.Add(error, Severity.Error);
            return;
        }

        ClientesEncontrados = (pagina?.Items ?? Array.Empty<ClienteDto>())
            .GroupBy(c => c.IdCliente)
            .Select(g => g.First())
            .ToList();

        // Si solo hay un candidato, lo abrimos directo para ahorrar un clic.
        if (ClientesEncontrados.Count == 1)
            await SeleccionarClienteAsync(ClientesEncontrados[0].IdCliente);
    }

    // ===================== Buscador: pestaña por VENTA (Id de venta) =====================

    // Dispara la búsqueda por Id de venta: resuelve la venta, su cliente y preselecciona esa venta.
    protected async Task BuscarVentaAsync()
    {
        var texto = NormalizarCodigoVenta(BusquedaVenta);
        if (string.IsNullOrWhiteSpace(texto))
        {
            Limpiar();
            return;
        }

        Cargando = true;
        HaBuscado = true;
        UltimaBusqueda = texto;
        BusquedaPorVenta = true;
        ClientesEncontrados = new();
        Cliente = null;
        EstadoCuenta = null;
        LimpiarVenta();

        try
        {
            // La búsqueda de venta resuelve el IdCliente dueño de la cuenta.
            var (ventaDetalle, error) = await Api.BuscarVentaCobranzaAdminAsync(texto);
            if (ventaDetalle is not null)
            {
                await SeleccionarClienteAsync(ventaDetalle.IdCliente, ventaDetalle.IdVenta);
                return;
            }
            if (error is not null) Snackbar.Add(error, Severity.Warning);
        }
        finally
        {
            Cargando = false;
        }
    }

    // ===================== Selección de cliente =====================

    // Carga el cliente y su estado de cuenta. Opcionalmente preselecciona una venta.
    protected async Task SeleccionarClienteAsync(long idCliente, long? idVentaPreseleccionada = null)
    {
        Cargando = true;
        ClientesEncontrados = new();
        Cliente = null;
        EstadoCuenta = null;
        LimpiarVenta();

        try
        {
            var (cliente, errorCliente) = await Api.ObtenerClienteAsync(idCliente);
            if (errorCliente is not null) Snackbar.Add(errorCliente, Severity.Error);
            Cliente = cliente;

            if (Cliente is null)
                return;

            var (estado, errorEstado) = await Api.ObtenerEstadoCuentaAdminAsync(idCliente);
            if (errorEstado is not null) Snackbar.Add(errorEstado, Severity.Warning);
            EstadoCuenta = estado;

            // Si la búsqueda fue por venta, vamos directo a esa venta.
            if (idVentaPreseleccionada is { } idVentaFija)
            {
                await SeleccionarVentaAsync(idVentaFija);
                return;
            }

            // Búsqueda por cliente: solo nos interesan las ventas con saldo pendiente.
            VentasConSaldo = (EstadoCuenta?.Ventas ?? Array.Empty<EstadoCuentaVentaDto>())
                .Where(x => x.Saldo > 0)
                .ToList();

            // Una sola venta con saldo -> detalle directo. Varias -> vista intermedia de selección.
            if (VentasConSaldo.Count == 1)
                await SeleccionarVentaAsync(VentasConSaldo[0].IdVenta);
        }
        finally
        {
            Cargando = false;
        }
    }

    // ===================== Selección de venta =====================

    protected async Task SeleccionarVentaAsync(long idVenta)
    {
        IdVentaSeleccionada = idVenta;
        CargandoVenta = true;
        Venta = null;
        PagosVenta = new();

        try
        {
            // Detalle completo (cabecera + producto/detalle + cuotas + planes promocionales).
            var (venta, errorVenta) = await Api.ObtenerVentaAdminAsync(idVenta);
            if (errorVenta is not null) Snackbar.Add(errorVenta, Severity.Error);
            Venta = venta;

            // Historial de pagos aplicados a la venta.
            var (pagos, errorPagos) = await Api.ObtenerHistorialPagosAdminAsync(idVenta);
            if (errorPagos is not null) Snackbar.Add(errorPagos, Severity.Warning);
            PagosVenta = pagos ?? new();
        }
        finally
        {
            CargandoVenta = false;
        }
    }

    protected Task OnFilaVentaClick(TableRowClickEventArgs<EstadoCuentaVentaDto> e)
        => e.Item is null ? Task.CompletedTask : SeleccionarVentaAsync(e.Item.IdVenta);

    // ===================== Helpers =====================

    protected void NuevaBusqueda()
    {
        BusquedaCliente = string.Empty;
        BusquedaVenta = string.Empty;
        Limpiar();
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

    // ===================== Auditoría: reversión y edición de pagos =====================

    // IdMovimiento del catálogo: 1 = ABONO (abono físico que admite reversa).
    private const long MovimientoAbono = 1;

    // Pide confirmación en un diálogo amigable (pantalla completa en móvil) y, si el usuario acepta,
    // revierte por completo el pago seleccionado. El motor de PostgreSQL limpia bonificaciones y reajusta las cuotas.
    protected async Task ConfirmarReversionAsync(PagoHistoricoDto pago)
    {
        if (Venta is null || IdVentaSeleccionada is null)
            return;

        var parametros = new DialogParameters<Components.DialogoConfirmacion>
        {
            { x => x.Titulo, "Revertir pago" },
            { x => x.Mensaje,
                $"Vas a revertir por completo el pago {Mostrar(pago.FolioPago)} por {pago.MontoAbona:C2}.\n\n" +
                "Esta acción elimina el abono, reajusta las cuotas afectadas y limpia las bonificaciones " +
                "asociadas (pronto pago / promoción). No se puede deshacer.\n\n¿Deseas continuar?" },
            { x => x.Icono, Icons.Material.Filled.Undo },
            { x => x.ColorIcono, Color.Error },
            { x => x.TextoAceptar, "Sí, revertir" },
            { x => x.TextoCancelar, "Cancelar" },
            { x => x.ColorAceptar, Color.Error }
        };

        var factor = FormFactor.GetFormFactor();
        var esMovil = !factor.Equals("Web", StringComparison.OrdinalIgnoreCase)
                      && !factor.Equals("Desktop", StringComparison.OrdinalIgnoreCase);

        var opciones = new DialogOptions
        {
            FullScreen = esMovil,
            FullWidth = true,
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            BackdropClick = false
        };

        var dialogo = await Dialogo.ShowAsync<Components.DialogoConfirmacion>("Revertir pago", parametros, opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: true })
            return;

        // Saldo de la venta ANTES de la reversión (para el comprobante).
        var saldoAntes = Venta.SaldoPendiente;

        var (ok, error) = await Api.RevertirPagoAsync(pago.IdPago);
        if (error is not null || !ok)
        {
            Snackbar.Add(error ?? "No se pudo revertir el pago.", Severity.Error);
            return;
        }

        Snackbar.Add($"Pago {Mostrar(pago.FolioPago)} revertido correctamente.", Severity.Success);

        // En móvil, ofrecemos imprimir un ticket pequeño del pago revertido.
        await OfrecerImprimirTicketReversionAsync(pago, saldoAntes);

        // Refrescamos la pantalla para reflejar el estado actualizado de la venta.
        await SeleccionarVentaAsync(IdVentaSeleccionada.Value);
    }

    // Abre el diálogo flotante de captura con los datos actuales del pago para corregirlo.
    // Al confirmar: (1) revierte el abono original en la API y (2) re-inserta el pago corregido.
    protected async Task AbrirEditarPagoDialogo(PagoHistoricoDto pago)
    {
        if (Venta is null || Cliente is null || IdVentaSeleccionada is null)
            return;

        var planesVigentes = (Venta.PlanesPromocionales ?? new())
            .Where(p => p.Activo)
            .OrderBy(p => p.NumeroMeses)
            .ToList();

        // Cobradores de la ZONA del cliente para sugerir el cobrador correcto; respaldo: lista general.
        var cobradoresZona = Cobradores;
        long? idCobradorSugerido = pago.IdCobrador;
        if (Cliente.IdZona is { } idZona)
        {
            var (cobsZona, _) = await Api.ListarCobradoresOperativosAsync(idZona);
            if (cobsZona is { Count: > 0 })
            {
                cobradoresZona = cobsZona;
                idCobradorSugerido ??= cobsZona[0].IdCobrador;
            }
        }

        // Al editar, el saldo "disponible" vuelve a incluir el monto del pago que vamos a revertir.
        var saldoTrasReversa = Venta.SaldoPendiente + pago.MontoAbona;

        var parametros = new DialogParameters<Components.DialogoRegistrarPago>
        {
            { x => x.IdVenta, Venta.IdVenta },
            { x => x.IdCliente, Venta.IdCliente },
            { x => x.Cliente, Venta.NombreCliente },
            { x => x.CodigoVenta, Venta.CodigoVenta },
            { x => x.SaldoPendiente, saldoTrasReversa },
            { x => x.CuotaFija, Venta.MontoCuotaFija },
            // Precargamos el input con el monto original del pago (el que muestra la tabla del historial).
            { x => x.MontoInicial, pago.MontoAbona },
            { x => x.PromoRachaActiva, PromoRachaActiva },
            { x => x.MetaRacha, MetaRacha },
            { x => x.RachaActual, 0 },
            { x => x.PlanesLiquidacion, planesVigentes },
            { x => x.Cobradores, cobradoresZona },
            { x => x.IdCobradorSugerido, idCobradorSugerido },
            { x => x.IdUsuarioRegistro, Api.Sesion?.IdUsuario ?? 0 }
        };

        var factor = FormFactor.GetFormFactor();
        var esMovil = !factor.Equals("Web", StringComparison.OrdinalIgnoreCase)
                      && !factor.Equals("Desktop", StringComparison.OrdinalIgnoreCase);

        var opciones = new DialogOptions
        {
            FullScreen = esMovil,
            FullWidth = esMovil,
            CloseButton = true,
            MaxWidth = MaxWidth.Medium
        };

        var dialogo = await Dialogo.ShowAsync<Components.DialogoRegistrarPago>("Editar pago", parametros, opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: ResultadoDialogoPago intencion })
            return;

        // (1) Reverso del pago original: el motor limpia bonificaciones y reajusta cuotas.
        var (okReversa, errorReversa) = await Api.RevertirPagoAsync(pago.IdPago);
        if (errorReversa is not null || !okReversa)
        {
            Snackbar.Add(errorReversa ?? "No se pudo revertir el pago original.", Severity.Error);
            return;
        }

        // (2) Re-inserción del pago corregido con los valores capturados en el diálogo.
        var req = new RegistrarPagoRequest(
            Guid.NewGuid(),
            Venta.IdVenta,
            intencion.Monto,
            intencion.IdCobrador,
            MovimientoAbono,
            Api.Sesion?.IdUsuario ?? 0,
            intencion.Observaciones,
            DateTimeOffset.UtcNow);

        var (resp, errorAlta) = await Api.RegistrarPagoAdminAsync(req);
        if (errorAlta is not null)
        {
            Snackbar.Add($"El pago original se revirtió, pero no se pudo re-insertar el corregido: {errorAlta}", Severity.Error);
            await SeleccionarVentaAsync(IdVentaSeleccionada.Value);
            return;
        }

        Snackbar.Add(
            resp is { Liquidada: true }
                ? $"Pago corregido {resp.FolioPago} registrado. ¡La cuenta quedó liquidada!"
                : $"Pago corregido {resp?.FolioPago} registrado. Saldo actual: {resp?.SaldoActual:C2}.",
            Severity.Success);

        // En móvil, imprimimos el ticket de ACTUALIZACIÓN con los datos reales del nuevo pago
        // (folio y saldos vigentes) e indicando el monto anterior corregido.
        if (resp is not null)
            await OfrecerImprimirTicketActualizacionAsync(pago, resp);

        // Refrescamos la pantalla para reflejar el estado actualizado de la venta.
        await SeleccionarVentaAsync(IdVentaSeleccionada.Value);
    }

    // ===================== Comprobante de reversión =====================

    // Tras revertir un pago, ofrece imprimir un ticket PEQUEÑO (solo en móvil) que deja constancia
    // del folio del pago revertido. Reutiliza el comprobante de pago marcándolo como "REVERSIÓN".
    // saldoAntes es el saldo pendiente de la venta ANTES de aplicar la reversión.
    private async Task OfrecerImprimirTicketReversionAsync(PagoHistoricoDto pago, decimal saldoAntes)
    {
        // La impresión por Bluetooth solo aplica en el entorno móvil (MAUI/Android).
        if (!EsEntornoMovil || Venta is null || Cliente is null)
            return;

        var ticket = ConstruirTicketReversion(pago, saldoAntes);

        var opciones = new DialogOptions
        {
            FullScreen = true,
            CloseButton = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Small
        };

        var dialogo = await Dialogo.ShowAsync<Components.ImpresoraBluetoothDialog>("Imprimir ticket de reversión", opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: ImpresoraBluetoothDto impresora })
            return;

        try
        {
            var impreso = await ImpresionTicket.ImprimirPagoAsync(impresora, ticket);
            Snackbar.Add(
                impreso
                    ? "Ticket de reversión impreso correctamente."
                    : "El pago se revirtió, pero no fue posible imprimir el ticket.",
                impreso ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"El pago se revirtió, pero ocurrió un error al imprimir: {ex.Message}", Severity.Warning);
        }
    }

    // Arma el comprobante de REVERSIÓN. Deja constancia del folio del pago revertido, el monto
    // devuelto y el saldo de la venta antes/después de la reversión. El QR lleva el código de la venta.
    private TicketPagoDto ConstruirTicketReversion(PagoHistoricoDto pago, decimal saldoAntes)
    {
        var v = Venta!;
        var c = Cliente!;

        // Al revertir, el saldo pendiente AUMENTA en el monto devuelto.
        var saldoDespues = saldoAntes + pago.MontoAbona;

        // Cobrador del ticket: el usuario logueado; respaldo: el cobrador que registró el pago original.
        var cobrador = !string.IsNullOrWhiteSpace(Api.Sesion?.NombreCompleto)
            ? Api.Sesion!.NombreCompleto
            : pago.Cobrador;
        string? nombreCorto = EmpresaGlobal.Info?.NombreCorto ?? EmpresaGlobal.Identidad?.NombreCorto;
        return new TicketPagoDto(
            NombreNegocio : string.IsNullOrWhiteSpace(nombreCorto) ? "Sistema de Cobranza" : nombreCorto,
            FolioPago: Mostrar(pago.FolioPago),
            Fecha: FechaProyecto.Ahora.DateTime,
            Cliente: new ClienteTicketDto(
                NombreCompleto: c.NombreCompleto,
                Telefono: c.TelefonoPrincipal,
                Direccion: c.Direccion,
                Ruta: c.Ruta,
                Zona: EstadoCuenta?.Zona),
            CodigoVenta: v.CodigoVenta ?? $"V{v.IdVenta}",
            IdVenta: v.IdVenta,
            SaldoAnterior: saldoAntes,
            MontoPagado: pago.MontoAbona,
            SaldoActual: saldoDespues,
            Movimiento: "REVERSION DE PAGO",
            Cobrador: cobrador,
            Liquidada: false,
            Compacto: EmpresaGlobal.TicketPagoCompacto);
    }

    // ===================== Comprobante de actualización (edición) =====================

    // Tras EDITAR un pago (reverso del original + alta del corregido), ofrece imprimir un ticket
    // (solo en móvil) que indica claramente que es una ACTUALIZACIÓN y muestra los datos REALES
    // del nuevo pago: su folio y los saldos vigentes, más el monto anterior corregido.
    // pagoOriginal es el abono revertido; resp es la respuesta del alta del pago corregido.
    private async Task OfrecerImprimirTicketActualizacionAsync(PagoHistoricoDto pagoOriginal, RegistrarPagoResponse resp)
    {
        if (!EsEntornoMovil || Venta is null || Cliente is null)
            return;

        var ticket = ConstruirTicketActualizacion(pagoOriginal, resp);

        var opciones = new DialogOptions
        {
            FullScreen = true,
            CloseButton = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Small
        };

        var dialogo = await Dialogo.ShowAsync<Components.ImpresoraBluetoothDialog>("Imprimir ticket de actualización", opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: ImpresoraBluetoothDto impresora })
            return;

        try
        {
            var impreso = await ImpresionTicket.ImprimirPagoAsync(impresora, ticket);
            Snackbar.Add(
                impreso
                    ? "Ticket de actualización impreso correctamente."
                    : "El pago se actualizó, pero no fue posible imprimir el ticket.",
                impreso ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"El pago se actualizó, pero ocurrió un error al imprimir: {ex.Message}", Severity.Warning);
        }
    }

    // Arma el comprobante de ACTUALIZACIÓN con los datos reales del nuevo pago (folio y saldos
    // vigentes de la respuesta del alta) e incluye el monto del pago anterior corregido.
    private TicketPagoDto ConstruirTicketActualizacion(PagoHistoricoDto pagoOriginal, RegistrarPagoResponse resp)
    {
        var v = Venta!;
        var c = Cliente!;

        // Cobrador del ticket: el usuario logueado; respaldo: el cobrador del pago original.
        var cobrador = !string.IsNullOrWhiteSpace(Api.Sesion?.NombreCompleto)
            ? Api.Sesion!.NombreCompleto
            : pagoOriginal.Cobrador;
        string? nombreCorto = EmpresaGlobal.Info?.NombreCorto ?? EmpresaGlobal.Identidad?.NombreCorto;
        
        return new TicketPagoDto(
            NombreNegocio: string.IsNullOrWhiteSpace(nombreCorto) ? "Sistema de Cobranza" : nombreCorto,
            // Folio VIGENTE: el del nuevo pago corregido.
            FolioPago: resp.FolioPago,
            Fecha: FechaProyecto.Ahora.DateTime,
            Cliente: new ClienteTicketDto(
                NombreCompleto: c.NombreCompleto,
                Telefono: c.TelefonoPrincipal,
                Direccion: c.Direccion,
                Ruta: c.Ruta,
                Zona: EstadoCuenta?.Zona),
            CodigoVenta: v.CodigoVenta ?? $"V{v.IdVenta}",
            IdVenta: v.IdVenta,
            // Saldos REALES tras aplicar el pago corregido.
            SaldoAnterior: resp.SaldoAnterior,
            MontoPagado: resp.MontoAbona,
            SaldoActual: resp.SaldoActual,
            Movimiento: "ACTUALIZACION DE PAGO",
            Cobrador: cobrador,
            Liquidada: resp.Liquidada,
            // Monto del pago original corregido (para dejar constancia del cambio).
            MontoAnterior: pagoOriginal.MontoAbona,
            Compacto: EmpresaGlobal.TicketPagoCompacto);
    }

    // ===================== Entorno =====================

    // Determina si la app corre en un entorno móvil (no Web ni Desktop).
    private bool EsEntornoMovil
    {
        get
        {
            var ff = FormFactor.GetFormFactor();
            return !ff.Equals("Web", StringComparison.OrdinalIgnoreCase)
                   && !ff.Equals("Desktop", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async ValueTask DisposeAsync()
    {
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
