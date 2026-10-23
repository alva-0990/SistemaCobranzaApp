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
public partial class Pagos : ComponentBase, IAsyncDisposable
{
    [Inject] protected ApiCliente Api { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected IDialogService Dialogo { get; set; } = default!;
    [Inject] protected IFormFactor FormFactor { get; set; } = default!;
    [Inject] protected ServicioImpresionTicket ImpresionTicket { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected IPermisosCamara PermisosCamara { get; set; } = default!;

    [Inject] protected EstadoEmpresaGlobal EmpresaGlobal { get; set; } = default!;


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

    // Cobradores para el combo del diálogo de pago (lista general de respaldo).
    protected List<CobradorDto> Cobradores { get; set; } = new();

    // Parámetros de la promoción de racha de pagos (se leen de ParametrosSistema).
    protected bool PromoRachaActiva { get; set; }
    protected int MetaRacha { get; set; } = 5;

    // Nombre del cobrador activo de la zona del cliente (respaldo para el ticket si no hay sesión).
    private string? _cobradorZonaNombre;

    // ---- Escáner de QR (lectura del folio de la venta por cámara) ----
    protected bool EsMovil => EsEntornoMovil;
    protected bool QrEscaneando { get; set; }
    protected string? QrError { get; set; }
    private IJSObjectReference? _qrModulo;
    private DotNetObjectReference<Pagos>? _qrRef;
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

        // Cobradores activos para el diálogo de registro de pago (lista general de respaldo).
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

    // Tras un pago exitoso espera 5 s (para que el usuario vea el resultado / termine la impresión)
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
    protected bool PuedeRegistrarPago => Venta is not null && Venta.SaldoPendiente > 0;

    // Abre el diálogo de registro de pago con el contexto de la venta y sus promociones,
    // y refresca la vista al confirmarse (para reflejar las filas automáticas que inyecta el trigger).
    protected async Task AbrirDialogoPago()
    {
        if (Venta is null || Cliente is null)
            return;

        var planesVigentes = (Venta.PlanesPromocionales ?? new())
            .Where(p => p.Activo)
            .OrderBy(p => p.NumeroMeses)
            .ToList();

        // Cobradores de la ZONA del cliente (para sugerir automáticamente el cobrador correcto).
        // Si el cliente no tiene zona o no hay cobradores en ella, caemos a la lista general ya cargada.
        var cobradoresZona = Cobradores;
        long? idCobradorSugerido = null;
        _cobradorZonaNombre = null;
        if (Cliente.IdZona is { } idZona)
        {
            var (cobsZona, _) = await Api.ListarCobradoresOperativosAsync(idZona);
            if (cobsZona is { Count: > 0 })
            {
                cobradoresZona = cobsZona;
                idCobradorSugerido = cobsZona[0].IdCobrador; // Cobrador asignado a la zona.
                _cobradorZonaNombre = cobsZona[0].Nombre;     // Respaldo para el ticket.
            }
        }

        var parametros = new DialogParameters<Components.DialogoRegistrarPago>
        {
            { x => x.IdVenta, Venta.IdVenta },
            { x => x.IdCliente, Venta.IdCliente },
            { x => x.Cliente, Venta.NombreCliente },
            { x => x.CodigoVenta, Venta.CodigoVenta },
            { x => x.SaldoPendiente, Venta.SaldoPendiente },
            { x => x.CuotaFija, Venta.MontoCuotaFija },
            { x => x.PromoRachaActiva, PromoRachaActiva },
            { x => x.MetaRacha, MetaRacha },   // Lee PROMO_PAGOS_CONSECUTIVOS_META real.
            { x => x.RachaActual, 0 },         // El trigger es la fuente de verdad de la racha real.
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
        
        var dialogo = await Dialogo.ShowAsync<Components.DialogoRegistrarPago>("Registrar pago", parametros, opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: ResultadoDialogoPago intencion })
            return;

        await ProcesarPagoAsync(intencion);
    }

    // IdMovimiento del catálogo: 1 = ABONO, 5 = AJUSTE (condonación de saldo).
    private const long MovimientoAbono = 1;
    private const long MovimientoAjuste = 5;
    // Marca que el trigger detecta para aplicar el descuento por plan de pronto pago.
    private const string MarcaProntoPago = "LIQUIDACION PRONTO PAGO";

    // Ejecuta las llamadas a la API según el modo elegido en el diálogo.
    private async Task ProcesarPagoAsync(ResultadoDialogoPago i)
    {
        if (Venta is null || Cliente is null)
            return;

        var idUsuario = Api.Sesion?.IdUsuario ?? 0;

        switch (i.Modo)
        {
            case ModoPagoDialogo.Abono:
            {
                // Abono normal: el trigger aplica cascada y promociones (racha).
                var req = new RegistrarPagoRequest(
                    Guid.NewGuid(), Venta.IdVenta, i.Monto, i.IdCobrador,
                    MovimientoAbono, idUsuario, i.Observaciones, DateTimeOffset.UtcNow);
                await EnviarPagoAsync(req);
                break;
            }

            case ModoPagoDialogo.LiquidacionPlan:
            {
                // Liquidación por plan: la marca activa el descuento automático del trigger.
                var obs = string.IsNullOrWhiteSpace(i.Observaciones)
                    ? MarcaProntoPago
                    : $"{MarcaProntoPago} — {i.Observaciones}";
                var req = new RegistrarPagoRequest(
                    Guid.NewGuid(), Venta.IdVenta, i.Monto, i.IdCobrador,
                    MovimientoAbono, idUsuario, obs, DateTimeOffset.UtcNow);
                await EnviarPagoAsync(req);
                break;
            }

            case ModoPagoDialogo.LiquidacionAcuerdo:
            {
                // Acuerdo sin plan: (1) abono por el monto convenido y (2) ajuste que condona el resto.
                var abono = new RegistrarPagoRequest(
                    Guid.NewGuid(), Venta.IdVenta, i.Monto, i.IdCobrador,
                    MovimientoAbono, idUsuario, i.Observaciones, DateTimeOffset.UtcNow);
                var (respAbono, errorAbono) = await Api.RegistrarPagoAdminAsync(abono);
                if (errorAbono is not null)
                {
                    Snackbar.Add(errorAbono, Severity.Error);
                    return;
                }

                var condonado = i.SaldoPendiente - i.Monto;
                if (condonado > 0 && respAbono is { Liquidada: false })
                {
                    var ajuste = new RegistrarPagoRequest(
                        Guid.NewGuid(), Venta.IdVenta, condonado, i.IdCobrador,
                        MovimientoAjuste, idUsuario,
                        $"CONDONACIÓN POR ACUERDO DE LIQUIDACIÓN. Saldo perdonado de {condonado:C2}.",
                        DateTimeOffset.UtcNow);
                    var (_, errorAjuste) = await Api.RegistrarPagoAdminAsync(ajuste);
                    if (errorAjuste is not null)
                    {
                        Snackbar.Add($"El abono se registró, pero no se pudo condonar el resto: {errorAjuste}", Severity.Warning);
                        await SeleccionarClienteAsync(Cliente.IdCliente, Venta.IdVenta);
                        return;
                    }

                    // El abono por sí solo deja saldo pendiente (= condonado) y Liquidada=false.
                    // Tras aplicar el ajuste de condonación, la cuenta queda en cero: corregimos la
                    // respuesta para que el COMPROBANTE DE PAGO refleje el estado final (saldo 0,
                    // cuenta liquidada) en lugar del saldo intermedio del abono.
                    respAbono = respAbono with { SaldoActual = 0m, Liquidada = true };
                }

                Snackbar.Add("Liquidación por acuerdo registrada. ¡La cuenta quedó liquidada!", Severity.Success);
                if (respAbono is not null)
                    await OfrecerImprimirTicketPagoAsync(respAbono);
                await SeleccionarClienteAsync(Cliente.IdCliente, Venta.IdVenta);
                await ProgramarRegresoBusquedaAsync();
                break;
            }
        }
    }

    // Envía un pago simple y reporta el resultado, refrescando la vista.
    private async Task EnviarPagoAsync(RegistrarPagoRequest req)
    {
        if (Cliente is null || Venta is null)
            return;

        var (resp, error) = await Api.RegistrarPagoAdminAsync(req);
        if (error is not null)
        {
            Snackbar.Add(error, Severity.Error);
            return;
        }

        Snackbar.Add(
            resp is { Liquidada: true }
                ? $"Pago {resp.FolioPago} registrado. ¡La cuenta quedó liquidada!"
                : $"Pago {resp?.FolioPago} registrado. Saldo actual: {resp?.SaldoActual:C2}.",
            Severity.Success);

        // Ofrecemos imprimir el ticket del pago (solo en móvil) antes de recargar la vista.
        if (resp is not null)
            await OfrecerImprimirTicketPagoAsync(resp);

        // Refrescamos para reflejar los movimientos automáticos del trigger (descuento/bonificación).
        await SeleccionarClienteAsync(Cliente.IdCliente, Venta.IdVenta);

        // Tras registrar el pago (y cerrar la impresión en móvil, o confirmar en web),
        // esperamos 5 s y regresamos al buscador para agilizar el siguiente cobro.
        await ProgramarRegresoBusquedaAsync();
    }

    // ===================== Impresión del ticket de pago =====================

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

    // Tras un pago exitoso, ofrece imprimir el comprobante (solo en móvil). El QR del ticket
    // contiene el CÓDIGO DE LA VENTA, de modo que al escanearlo luego se puede ubicar la venta.
    private async Task OfrecerImprimirTicketPagoAsync(RegistrarPagoResponse resp)
    {
        // La impresión por Bluetooth solo aplica en el entorno móvil (MAUI/Android).
        if (!EsEntornoMovil || Venta is null || Cliente is null)
            return;

        var ticket = ConstruirTicketPago(resp);

        var opciones = new DialogOptions
        {
            FullScreen = true,
            CloseButton = true,
            FullWidth = true,
            MaxWidth = MaxWidth.Small
        };

        var dialogo = await Dialogo.ShowAsync<Components.ImpresoraBluetoothDialog>("Imprimir ticket", opciones);
        var resultado = await dialogo.Result;

        if (resultado is not { Canceled: false, Data: ImpresoraBluetoothDto impresora })
            return;

        try
        {
            var impreso = await ImpresionTicket.ImprimirPagoAsync(impresora, ticket);
            Snackbar.Add(
                impreso ? "Ticket de pago impreso correctamente." : "El pago se registró, pero no fue posible imprimir el ticket.",
                impreso ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"El pago se registró, pero ocurrió un error al imprimir: {ex.Message}", Severity.Warning);
        }
    }

    // Construye el COMPROBANTE DE PAGO. El QR usa el código de la venta para re-localizar la cuenta.
    private TicketPagoDto ConstruirTicketPago(RegistrarPagoResponse resp)
    {
        var v = Venta!;
        var c = Cliente!;

        // Cobrador del ticket: el usuario logueado en el dispositivo (token). Si no hay nombre
        // de sesión, se usa el cobrador activo de la zona del cliente como respaldo.
        var cobrador = !string.IsNullOrWhiteSpace(Api.Sesion?.NombreCompleto)
            ? Api.Sesion!.NombreCompleto
            : _cobradorZonaNombre;
        string? nombreCorto = EmpresaGlobal.Info?.NombreCorto ?? EmpresaGlobal.Identidad?.NombreCorto;

        return new TicketPagoDto(
            NombreNegocio : string.IsNullOrWhiteSpace(nombreCorto) ? "Sistema de Cobranza" : nombreCorto,

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
            SaldoAnterior: resp.SaldoAnterior,
            MontoPagado: resp.MontoAbona,
            SaldoActual: resp.SaldoActual,
            Movimiento: "ABONO",
            Cobrador: cobrador,
            Liquidada: resp.Liquidada,
            Compacto: EmpresaGlobal.TicketPagoCompacto);
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
