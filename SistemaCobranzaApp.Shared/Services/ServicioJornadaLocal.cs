using System.Text.Json;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Domain.Clientes;
using SistemaCobranzaApp.Domain.Ventas;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public sealed class ServicioJornadaLocal : IDisposable
{
    private readonly EstadoEmpresaGlobal EmpresaGlobal;
    private readonly ApiCliente _api;
    private readonly IAlmacenJornada _almacen;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private long _usuario;
    private readonly Timer _reintentos;
    public JornadaLocal Estado { get; private set; } = new();
    public bool Disponible => _almacen.Disponible;
    public bool ConInternet => _almacen.ConInternet;
    public bool EsUsuarioCampo => _api.Sesion?.IdRol is 3 or 4;
    public bool Sincronizando { get; private set; }
    public string? UltimoError { get; private set; }
    public int Pendientes => Estado.Operaciones.Count(o => !o.Aplicada);
    public event Action? Cambio;



    public ServicioJornadaLocal(ApiCliente api, IAlmacenJornada almacen, EstadoEmpresaGlobal empresaGlobal)
    {
        EmpresaGlobal = empresaGlobal;
        _api = api;
        _almacen = almacen;
        _almacen.ConexionRecuperada += AlRecuperarConexion;
        _reintentos = new Timer(_ => { if (Pendientes > 0) AlRecuperarConexion(); }, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public async Task CargarAsync()
    {
        if (!Disponible || _api.Sesion is null) return;
        await _mutex.WaitAsync();
        try
        {
            if (_usuario == _api.Sesion.IdUsuario) return;
            var usuario = _api.Sesion.IdUsuario;
            var json = await _almacen.LeerAsync(usuario);
            var estado = json is null ? new JornadaLocal() : JsonSerializer.Deserialize<JornadaLocal>(json)
                ?? throw new IOException("No se pudo leer la jornada local. No descargues otra hasta recuperar los pendientes.");
            if (estado.Descarga is { } d && d.IdUsuario != usuario)
                throw new IOException("La jornada guardada pertenece a otro usuario.");
            Estado = estado;
            _usuario = usuario;
        }
        finally { _mutex.Release(); Cambio?.Invoke(); }
    }

    public async Task DescargarAsync(long ruta)
    {
        await CargarAsync();
        await _mutex.WaitAsync();
        try
        {
            if (!Disponible || _api.Sesion?.IdUsuario != _usuario || !EsUsuarioCampo)
                throw new InvalidOperationException("Inicia sesión como vendedor o cobrador.");
            if (Pendientes > 0) throw new InvalidOperationException("Sincroniza los pendientes antes de descargar otra jornada.");
            var (datos, error) = await _api.DescargarJornadaAsync(ruta);
            if (datos is null || error is not null) throw new InvalidOperationException(error ?? "No se recibió la jornada completa.");
            var nuevo = new JornadaLocal { Descarga = datos };
            await Persistir(nuevo);
            Estado = nuevo;
            UltimoError = null;
        }
        finally { _mutex.Release(); Cambio?.Invoke(); }
    }

    public async Task<Guid> GuardarVentaAsync(CrearVentaRequest venta)
        => await GuardarVentaCapturadaAsync(venta, null);

    public async Task<Guid> GuardarRenovacionAsync(long idVentaVieja, CrearVentaRequest venta)
        => await GuardarVentaCapturadaAsync(venta, idVentaVieja);

    private async Task<Guid> GuardarVentaCapturadaAsync(CrearVentaRequest venta, long? idVentaVieja)
    {
        await _mutex.WaitAsync();
        Guid uuid;
        try
        {
            var d = ValidarUsuario();
            if (_api.Sesion!.IdRol != 3 || d.Asignacion.IdVendedor is null)
                throw new InvalidOperationException("El usuario no es vendedor de esta jornada.");
            if (!ClientesDisponibles().Any(c => c.IdCliente == venta.IdCliente)) throw new InvalidOperationException("El cliente no pertenece a esta zona.");
            if (venta.Detalles.Count == 0 || venta.Detalles.Any(l => l.Cantidad <= 0 || l.PrecioCobrado < 0))
                throw new InvalidOperationException("Revisa los artículos de la venta.");
            foreach (var g in venta.Detalles.GroupBy(l => l.IdProducto))
                if (InventarioDisponible().FirstOrDefault(x => x.IdProducto == g.Key)?.Cantidad < g.Sum(x => x.Cantidad)
                    || !d.Inventario.Any(x => x.IdProducto == g.Key))
                    throw new InvalidOperationException("No hay suficientes existencias en la jornada descargada.");
            decimal arrastre = 0;
            Guid? uuidOrigen = null;
            if (idVentaVieja is { } idOrigen)
            {
                var origen = VentasDisponibles().SingleOrDefault(v => v.IdVenta == idOrigen);
                if (origen is null || origen.IdCliente != venta.IdCliente || origen.IdEstatusVenta == 4)
                    throw new InvalidOperationException("La cuenta anterior no pertenece al cliente o no está activa.");
                arrastre = SaldoDisponible(origen);
                if (arrastre <= 0) throw new InvalidOperationException("La cuenta anterior ya no tiene saldo pendiente.");
                if (!venta.IdFrecuenciaPago.HasValue || venta.MontoCuotaFija is not > 0
                    || venta.TotalFinal - venta.Enganche <= 0)
                    throw new InvalidOperationException("La renovación requiere una nueva venta a crédito.");
                uuidOrigen = Estado.Operaciones.FirstOrDefault(o => o.Venta is not null &&
                    (o.IdLocal == idOrigen || o.VentaCentral?.IdVenta == idOrigen))?.Uuid;
            }
            var o = new OperacionJornadaLocal { IdVentaVieja = idVentaVieja, VentaViejaUuid = uuidOrigen,
                SaldoArrastre = arrastre, IdLocal = -(Estado.Operaciones.Count + 1L), Venta = venta with { IdVendedor = d.Asignacion.IdVendedor,
                CodigoVenta = CalculoVenta.ValidarCodigoVentaOffline(venta.CodigoVenta)
                    ? venta.CodigoVenta!.Trim().ToUpperInvariant()
                    : CalculoVenta.GenerarCodigoVenta(d.Asignacion.IdVendedor.Value) } };
            o.TicketVenta = ConstruirTicketVenta(o);
            uuid = o.Uuid;
            await Agregar(o);
        }
        finally { _mutex.Release(); Cambio?.Invoke(); }
        AlRecuperarConexion();
        return uuid;
    }

    public async Task<Guid> GuardarPagoAsync(long venta, decimal monto, string? observaciones)
    {
        return await RegistrarCobroAsync(venta, monto, (v, saldo, m) =>
        {
            if (m <= 0 || m > saldo)
                throw new InvalidOperationException("El abono debe ser positivo y no superar el saldo local.");
            // Un solo abono normal (IdMovimiento = ABONO). Liquida si cubre todo el saldo.
            return new[]
            {
                new OperacionCobro(m, MovimientoIds.Abono, observaciones,
                    EsPrincipal: true, Concepto: "ABONO LOCAL · PENDIENTE DE CONFIRMACIÓN",
                    SaldoResultante: saldo - m)
            };
        });
    }

    // Marca que el trigger central detecta para condonar la diferencia por plan de pronto pago.
    private const string MarcaProntoPago = "LIQUIDACION PRONTO PAGO";

    /// <summary>
    /// Liquidación anticipada por PLAN (pronto pago) sin conexión. Se registra un único abono
    /// con la marca "LIQUIDACION PRONTO PAGO" en las observaciones: al sincronizar, el trigger
    /// central condona la diferencia. Localmente la cuenta queda liquidada (saldo 0).
    /// </summary>
    public async Task<Guid> GuardarLiquidacionPlanAsync(long venta, decimal montoLiquidacion, string? observaciones)
    {
        return await RegistrarCobroAsync(venta, montoLiquidacion, (v, saldo, m) =>
        {
            if (m <= 0 || m > saldo)
                throw new InvalidOperationException("El monto de liquidación debe ser positivo y no superar el saldo local.");
            var obs = string.IsNullOrWhiteSpace(observaciones) ? MarcaProntoPago : $"{MarcaProntoPago} — {observaciones}";
            // Replicamos la MISMA condición que el trigger central (fn_procesar_abono_cascada):
            // el descuento por pronto pago solo aplica si existe un plan VIGENTE cuyo NumeroMeses
            // cubra los meses transcurridos del crédito y el monto pagado alcance su liquidación.
            // Si no califica, la cuenta NO queda liquidada: solo se abona (saldo - monto).
            var califica = CalificaProntoPago(v, m);
            var saldoResultante = califica ? 0m : saldo - m;
            var concepto = califica
                ? "LIQUIDACION PRONTO PAGO · PENDIENTE DE CONFIRMACIÓN"
                : "ABONO (PLAN NO VIGENTE) · PENDIENTE DE CONFIRMACIÓN";
            // Un abono con la marca de pronto pago; el trigger central condonará si califica.
            return new[]
            {
                new OperacionCobro(m, MovimientoIds.Abono, obs,
                    EsPrincipal: true, Concepto: concepto, SaldoResultante: saldoResultante)
            };
        });
    }

    /// <summary>
    /// Liquidación por ACUERDO sin conexión. Se registran DOS operaciones: (1) el abono por el
    /// monto acordado y (2) un ajuste (IdMovimiento = AJUSTE) que condona el saldo restante.
    /// Ambas se sincronizan como pagos normales. Localmente la cuenta queda liquidada (saldo 0).
    /// </summary>
    public async Task<Guid> GuardarLiquidacionAcuerdoAsync(long venta, decimal montoAcordado, string? observaciones)
    {
        return await RegistrarCobroAsync(venta, montoAcordado, (v, saldo, m) =>
        {
            if (m <= 0 || m > saldo)
                throw new InvalidOperationException("El monto acordado debe ser positivo y no superar el saldo local.");
            var condonado = saldo - m;
            var operaciones = new List<OperacionCobro>
            {
                // (1) Abono por el monto convenido. Es el ticket principal; se muestra como liquidado.
                new(m, MovimientoIds.Abono, observaciones,
                    EsPrincipal: true, Concepto: "LIQUIDACION POR ACUERDO · PENDIENTE DE CONFIRMACIÓN",
                    SaldoResultante: 0m)
            };
            // (2) Ajuste que condona el resto (si lo hay). Resta saldo localmente y se sincroniza como pago.
            if (condonado > 0)
                operaciones.Add(new OperacionCobro(condonado, MovimientoIds.Ajuste,
                    $"CONDONACIÓN POR ACUERDO DE LIQUIDACIÓN. Saldo perdonado de {condonado:C2}.",
                    EsPrincipal: false, Concepto: null, SaldoResultante: 0m));
            return operaciones;
        });
    }

    // Una operación de pago a registrar localmente (abono o ajuste).
    private sealed record OperacionCobro(
        decimal Monto, long IdMovimiento, string? Observaciones,
        bool EsPrincipal, string? Concepto, decimal SaldoResultante);

    // Réplica local de la condición de PRONTO PAGO del trigger central (fn_procesar_abono_cascada):
    // existe un VentaPlan Activo cuyo NumeroMeses >= meses transcurridos del crédito, y el monto
    // pagado alcanza su MontoLiquidacion. Si se cumple, el trigger condona el resto (saldo -> 0).
    // meses transcurridos = EXTRACT(MONTH FROM AGE(now, FechaCompra)) + 1  (misma fórmula del SQL).
    private static bool CalificaProntoPago(VentaDto v, decimal monto)
    {
        var planes = v.PlanesPromocionales;
        if (planes is null || planes.Count == 0) return false;

        var ahora = DateTimeOffset.UtcNow;
        var compra = v.FechaCompra;
        var mesesTranscurridos = ((ahora.Year - compra.Year) * 12 + ahora.Month - compra.Month) + 1;

        // El plan vigente más ajustado: el de menor NumeroMeses que cubra los meses transcurridos.
        var planVigente = planes
            .Where(p => p.Activo && p.NumeroMeses >= mesesTranscurridos)
            .OrderBy(p => p.NumeroMeses)
            .FirstOrDefault();

        return planVigente is not null && monto >= planVigente.MontoLiquidacion;
    }

    // Núcleo común: valida al cobrador/venta y registra una o varias operaciones de pago locales
    // de forma atómica. 'construir' recibe (venta, saldoDisponible, monto) y devuelve las
    // operaciones a guardar; la primera marcada EsPrincipal lleva el ticket imprimible.
    private async Task<Guid> RegistrarCobroAsync(long venta, decimal monto,
        Func<VentaDto, decimal, decimal, IEnumerable<OperacionCobro>> construir)
    {
        await _mutex.WaitAsync();
        Guid uuidPrincipal = Guid.Empty;
        try
        {
            var d = ValidarUsuario();
            if (_api.Sesion!.IdRol != 4 || d.Asignacion.IdCobrador is null)
                throw new InvalidOperationException("El usuario no es cobrador de esta jornada.");
            var v = d.Ventas.FirstOrDefault(x => x.IdVenta == venta)
                ?? throw new InvalidOperationException("La venta no está en la zona descargada.");
            var saldo = SaldoDisponible(v);
            var cliente = ClientesDisponibles().Single(c => c.IdCliente == v.IdCliente);
            var negocio = NombreNegocio();

            var operaciones = construir(v, saldo, monto).ToList();
            if (operaciones.Count == 0)
                throw new InvalidOperationException("No se generó ninguna operación de pago.");

            foreach (var op in operaciones)
            {
                var uuid = Guid.NewGuid();
                var o = new OperacionJornadaLocal
                {
                    Uuid = uuid,
                    IdLocal = -(Estado.Operaciones.Count + 1L),
                    Pago = new RegistrarPagoRequest(uuid, venta, op.Monto, d.Asignacion.IdCobrador,
                        op.IdMovimiento, _usuario, op.Observaciones, DateTimeOffset.UtcNow)
                };
                // Solo la operación principal lleva ticket imprimible (abono visible al cliente).
                if (op.EsPrincipal)
                {
                    var folioPago = CalculoVenta.GenerarFolioPago(d.Asignacion.IdCobrador!.Value);
                    o.TicketPago = new(negocio, folioPago, o.FechaCaptura.EnZonaProyecto().DateTime,
                        DatosTicket(cliente), v.CodigoVenta ?? $"V{v.IdVenta}", v.IdVenta,
                        saldo, op.Monto, op.SaldoResultante,
                        op.Concepto, _api.Sesion?.NombreCompleto, op.SaldoResultante <= 0,
                        Compacto: EmpresaGlobal.TicketPagoCompacto);
                    uuidPrincipal = uuid;
                }
                await Agregar(o);
            }
        }
        finally { _mutex.Release(); Cambio?.Invoke(); }
        AlRecuperarConexion();
        return uuidPrincipal;
    }

    public async Task<ClienteDto> GuardarClienteAsync(CrearClienteRequest cliente)
    {
        await _mutex.WaitAsync();
        ClienteDto dto;
        try
        {
            var d = ValidarUsuario();
            if (_api.Sesion!.IdRol != 3 || d.Asignacion.IdVendedor is null)
                throw new InvalidOperationException("Solo el vendedor puede registrar clientes.");
            var error = ValidacionCliente.Validar(cliente.Nombre, cliente.IdRuta, d.Asignacion.IdVendedor, cliente.IdClasificacion);
            if (error is not null) throw new InvalidOperationException(error);
            if (string.IsNullOrWhiteSpace(cliente.TelefonoPrincipal) || string.IsNullOrWhiteSpace(cliente.Direccion))
                throw new InvalidOperationException("El teléfono y la dirección son obligatorios.");
            var ruta = d.Rutas.FirstOrDefault(r => r.IdRuta == cliente.IdRuta);
            if (ruta is null && cliente.IdRuta != d.Asignacion.IdRuta)
                throw new InvalidOperationException("Selecciona una ruta de la zona descargada.");
            if (cliente.IdClasificacion is { } clas && !d.Clasificaciones.Any(c => c.IdClasificaionCliente == clas && c.Activo))
                throw new InvalidOperationException("La clasificación no pertenece al catálogo descargado.");
            var req = cliente with { IdVendedor = d.Asignacion.IdVendedor, IdUsuarioRegistro = _usuario };
            var o = new OperacionJornadaLocal { Cliente = req, IdLocal = -(Estado.Operaciones.Count + 1L) };
            dto = new(o.IdLocal, req.Nombre.Trim(), req.ApPaterno, req.ApMaterno,
                string.Join(" ", new[] { req.Nombre.Trim(), req.ApPaterno, req.ApMaterno }.Where(s => !string.IsNullOrWhiteSpace(s))),
                req.TelefonoPrincipal, req.TelefonoSecundario, req.Email, req.Direccion, req.CodigoPostal, req.ReferenciaUbicacion,
                req.Latitud, req.Longitud, req.IdRuta, ruta?.Ruta ?? d.Asignacion.Ruta, d.Asignacion.IdZona,
                d.Asignacion.IdVendedor, _api.Sesion.NombreCompleto, req.IdClasificacion,
                d.Clasificaciones.FirstOrDefault(c => c.IdClasificaionCliente == req.IdClasificacion)?.Descripcion,
                1, req.Observaciones, o.FechaCaptura);
            o.ClienteLocal = dto;
            await Agregar(o);
        }
        finally { _mutex.Release(); Cambio?.Invoke(); }
        AlRecuperarConexion();
        return dto;
    }

    public List<ClienteDto> ClientesDisponibles()
    {
        var locales = Estado.Operaciones.Where(o => o.ClienteLocal is not null).ToList();
        var centrales = locales.Where(o => o.ClienteCentral is not null).Select(o => o.ClienteCentral!.IdCliente).ToHashSet();
        return (Estado.Descarga?.Clientes ?? new()).Where(c => !centrales.Contains(c.IdCliente))
            .Concat(locales.Select(o => o.ClienteLocal!)).ToList();
    }

    public List<VentaDto> VentasDisponibles()
    {
        var resultado = (Estado.Descarga?.Ventas ?? new()).ToList();
        foreach (var o in Estado.Operaciones.Where(o => o.Venta is not null))
        {
            if (o.VentaCentral is { } central) resultado.RemoveAll(v => v.IdVenta == central.IdVenta);
            var req = o.Venta!;
            var id = o.VentaCentral?.IdVenta ?? (o.IdLocal != 0 ? o.IdLocal : -(Estado.Operaciones.IndexOf(o) + 1L));
            var cliente = ClientesDisponibles().FirstOrDefault(c => c.IdCliente == req.IdCliente);
            var d = Estado.Descarga!;
            var total = req.TotalFinal + o.SaldoArrastre;
            var financiado = total - req.Enganche;
            var cuota = o.IdVentaVieja.HasValue && req.NumeroCuotas is > 0
                ? Math.Round(financiado / req.NumeroCuotas.Value, 2, MidpointRounding.AwayFromZero) : req.MontoCuotaFija ?? 0;
            resultado.Add(o.VentaCentral is { } confirmada ? confirmada with { IdCliente = req.IdCliente } :
                new VentaDto(id, req.IdCliente, cliente?.NombreCompleto ?? "Cliente", req.IdVendedor, _api.Sesion?.NombreCompleto,
                    CodigoVentaLocal(o), o.FechaCaptura, req.IdCondicion,
                    d.Condiciones.FirstOrDefault(c => c.IdCondicion == req.IdCondicion)?.Descripcion ?? "",
                    req.Subtotal, req.DescuentoGlobal, total, req.Enganche, financiado,
                    req.IdFrecuenciaPago, d.Frecuencias.FirstOrDefault(f => f.Id == req.IdFrecuenciaPago)?.Descripcion,
                    req.NumeroCuotas ?? 0, cuota, req.TasaInteres ?? 0, req.FechaPrimerVence,
                    0, financiado, null, 1, "Guardada en dispositivo", req.Observaciones,
                    req.Detalles.Select(l => new DetalleVentaDto(0, id, l.IdProducto,
                        d.Productos.FirstOrDefault(p => p.Id == l.IdProducto)?.Descripcion ?? $"Producto #{l.IdProducto}",
                        l.Cantidad, l.PrecioCobrado, l.Cantidad * l.PrecioCobrado)).ToList(), new(),
                    (req.PlanesPromocionales ?? new()).Select(p => new VentaPlanDto(0, id, p.NumeroMeses, p.MontoLiquidacion, true)).ToList()));
        }
        return resultado;
    }

    private ClienteTicketDto DatosTicket(ClienteDto c) => new(c.NombreCompleto, c.TelefonoPrincipal, c.Direccion, c.Ruta, _api.Sesion?.Zona);

    // Nombre comercial corto de la empresa para la cabecera de los tickets.
    // Prioriza el EmpresaDto completo (Info) y cae a la Identidad pública (cargada en el login,
    // disponible aun sin haber consultado la empresa operativa). Fallback final genérico.
    private string NombreNegocio()
    {
        var nombreCorto = EmpresaGlobal.Info?.NombreCorto ?? EmpresaGlobal.Identidad?.NombreCorto;
        return string.IsNullOrWhiteSpace(nombreCorto) ? "Sistema de Cobranza" : nombreCorto;
    }

    private static string CodigoVentaLocal(OperacionJornadaLocal o)
        => o.Venta?.CodigoVenta ?? o.TicketVenta?.CodigoVenta ?? $"MOV-{o.Uuid:N}".ToUpperInvariant();

    private TicketVentaDto ConstruirTicketVenta(OperacionJornadaLocal o)
    {
        var req = o.Venta!;
        var d = Estado.Descarga!;
        var c = ClientesDisponibles().Single(c => c.IdCliente == req.IdCliente);

        var ticket = new TicketVentaDto(
            NombreNegocio(),
            CodigoVentaLocal(o), o.FechaCaptura.EnZonaProyecto().DateTime,
            DatosTicket(c), req.Detalles.Select(l => new TicketDetalleDto(
                d.Productos.FirstOrDefault(p => p.Id == l.IdProducto)?.Descripcion ?? $"Producto #{l.IdProducto}",
                l.Cantidad, l.PrecioCobrado, l.Cantidad * l.PrecioCobrado)).ToList(),
            req.Subtotal, req.DescuentoGlobal, req.TotalFinal + o.SaldoArrastre, req.Enganche, req.TotalFinal + o.SaldoArrastre - req.Enganche,
            d.Frecuencias.FirstOrDefault(f => f.Id == req.IdFrecuenciaPago)?.Descripcion,
            o.IdVentaVieja.HasValue && req.NumeroCuotas is > 0
                ? Math.Round((req.TotalFinal + o.SaldoArrastre - req.Enganche) / req.NumeroCuotas.Value, 2, MidpointRounding.AwayFromZero)
                : req.MontoCuotaFija ?? 0, req.NumeroCuotas ?? 0, req.FechaPrimerVence,
            (req.PlanesPromocionales ?? new()).Select(p => new TicketPlanDto(p.NumeroMeses, p.MontoLiquidacion)).ToList());
        return ticket;
    }

    public TicketVentaDto TicketVenta(Guid uuid)
    {
        var o = Estado.Operaciones.Single(o => o.Uuid == uuid && o.Venta is not null);
        // Reconstruimos SIEMPRE el ticket desde o.Venta (datos reales de la operación), en vez de
        // confiar en o.TicketVenta persistido: así cualquier cambio de formato o de contenido del
        // ticket (p. ej. las condiciones de liquidación / VentaPlan) se refleja también en ventas
        // ya guardadas antes de la actualización. o.Venta siempre está presente para una venta.
        return ConstruirTicketVenta(o) with
        { CodigoVenta = o.VentaCentral?.CodigoVenta ?? CodigoVentaLocal(o) };
    }

    public TicketPagoDto TicketPago(Guid uuid)
    {
        var o = Estado.Operaciones.Single(o => o.Uuid == uuid && o.Pago is not null);
        var ticket = o.TicketPago;
        if (ticket is null)
        {
            var v = Estado.Descarga?.Ventas.SingleOrDefault(v => v.IdVenta == o.Pago!.IdVenta)
                ?? throw new InvalidOperationException("La cuenta de este abono no está disponible para reimprimir.");
            var c = ClientesDisponibles().Single(c => c.IdCliente == v.IdCliente);
            var descargado = Estado.Descarga!.Pagos.FirstOrDefault(p => p.PagoUuid == uuid);
            var previos = Estado.Operaciones.TakeWhile(p => p.Uuid != uuid)
                .Where(p => p.Pago?.IdVenta == v.IdVenta && !Estado.Descarga.Pagos.Any(d => d.PagoUuid == p.Uuid))
                .Sum(p => p.Pago!.MontoAbona);
            var anterior = descargado?.SaldoAnterior ?? Math.Max(0, v.SaldoPendiente - previos);
            // Fallback (sin ticket persistido): folio de cobrador con el mismo formato C{id}-...
            var folioPago = CalculoVenta.GenerarFolioPago(o.Pago!.IdCobrador ?? 0);
            ticket = new(NombreNegocio(), folioPago, o.FechaCaptura.EnZonaProyecto().DateTime,
                DatosTicket(c), v.CodigoVenta ?? $"V{v.IdVenta}", v.IdVenta, anterior, o.Pago!.MontoAbona,
                descargado?.SaldoActual ?? Math.Max(0, anterior - o.Pago.MontoAbona),
                "ABONO LOCAL · PENDIENTE DE CONFIRMACIÓN", _api.Sesion?.NombreCompleto, anterior <= o.Pago.MontoAbona,
                Compacto: EmpresaGlobal.TicketPagoCompacto);
        }
        return o.PagoCentral is { } p ? ticket with { FolioPago = p.FolioPago, SaldoAnterior = p.SaldoAnterior,
            SaldoActual = p.SaldoActual, MontoPagado = p.MontoAbona, Liquidada = p.Liquidada, Movimiento = "ABONO" } : ticket;
    }

    public List<ClienteDto> BuscarClientes(string? texto)
    {
        var terminos = (texto ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ClientesDisponibles().Where(c => terminos.All(t =>
            $"{c.IdCliente} {c.NombreCompleto} {c.TelefonoPrincipal} {c.TelefonoSecundario} {c.Direccion} {c.Ruta} {_api.Sesion?.Zona}"
                .Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public decimal SaldoDisponible(VentaDto venta)
    {
        if (Estado.Operaciones.Any(o => o.IdVentaVieja == venta.IdVenta ||
            o.VentaViejaUuid is { } uuid && Estado.Operaciones.Any(v => v.Uuid == uuid && v.VentaCentral?.IdVenta == venta.IdVenta)))
            return 0;

        var pagosDescargados = Estado.Descarga?.Pagos.Select(p => p.PagoUuid).ToHashSet() ?? new();
        var abonos = Estado.Operaciones.Where(o => o.Pago?.IdVenta == venta.IdVenta && !pagosDescargados.Contains(o.Uuid))
            .Sum(o => o.Pago!.MontoAbona);
        return Math.Max(0, venta.SaldoPendiente - abonos);
    }

    public List<ExistenciaZonaDto> InventarioDisponible()
    {
        if (Estado.Descarga is not { } d) return new();
        var tomadas = Estado.Operaciones.Where(o => o.Venta is not null &&
                (o.VentaCentral is null || !d.Ventas.Any(v => v.IdVenta == o.VentaCentral.IdVenta)))
            .SelectMany(o => o.Venta!.Detalles).GroupBy(l => l.IdProducto)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));
        return d.Inventario.Select(i => i with { Cantidad = Math.Max(0, i.Cantidad - tomadas.GetValueOrDefault(i.IdProducto)) }).ToList();
    }

    public async Task SincronizarAsync()
    {
        if (_api.Sesion is { } sesion && sesion.Expira <= DateTimeOffset.UtcNow)
        {
            UltimoError = "La sesión expiró. Inicia sesión con internet para enviar los pendientes; permanecen guardados en el dispositivo.";
            Cambio?.Invoke();
            return;
        }
        if (!Disponible || !ConInternet || !EsUsuarioCampo || !await _mutex.WaitAsync(0)) return;
        try
        {
            var d = ValidarUsuario();
            Sincronizando = true;
            UltimoError = null;
            Cambio?.Invoke();
            foreach (var o in Estado.Operaciones.Where(o => !o.Aplicada).OrderBy(o => o.FechaCaptura).ToList())
            {
                if (_api.Sesion?.IdUsuario != _usuario) break;
                using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                (ResultadoOperacionJornada? datos, string? error) r;
                if (o.Cliente is { } cliente)
                    r = await _api.SincronizarClienteJornadaAsync(new(o.Uuid, o.FechaCaptura, cliente), limite.Token);
                else if (o.Venta is { } venta)
                {
                    var idCliente = venta.IdCliente;
                    if (idCliente < 0)
                    {
                        var padre = Estado.Operaciones.FirstOrDefault(p => p.ClienteLocal?.IdCliente == idCliente);
                        if (padre?.ClienteCentral is null || !padre.Aplicada)
                            r = (new(false, false, "Pendiente de sincronizar el alta del cliente."), null);
                        else
                            r = await _api.SincronizarVentaJornadaAsync(d.Asignacion.IdRuta,
                                new VentaJornadaRequest(o.Uuid, o.FechaCaptura, venta with { IdCliente = padre.ClienteCentral.IdCliente })
                                { IdVentaVieja = o.IdVentaVieja, VentaViejaUuid = o.VentaViejaUuid, SaldoArrastre = o.SaldoArrastre }, limite.Token);
                    }
                    else r = await _api.SincronizarVentaJornadaAsync(d.Asignacion.IdRuta, new VentaJornadaRequest(o.Uuid, o.FechaCaptura, venta)
                        { IdVentaVieja = o.IdVentaVieja, VentaViejaUuid = o.VentaViejaUuid, SaldoArrastre = o.SaldoArrastre }, limite.Token);
                }
                else r = await _api.SincronizarPagoJornadaAsync(new(d.Asignacion.IdRuta, o.Pago!, o.VentaLocalUuid), limite.Token);
                if (r.error is not null || r.datos is null)
                {
                    UltimoError = r.error ?? "No hubo confirmación de central. Los pendientes siguen guardados.";
                    break;
                }
                var copia = JsonSerializer.Deserialize<JornadaLocal>(JsonSerializer.Serialize(Estado))!;
                var guardada = copia.Operaciones.Single(x => x.Uuid == o.Uuid);
                guardada.Aplicada = r.datos.Aplicado && (o.Cliente is not null ? r.datos.Cliente is not null : o.Venta is not null ? r.datos.Venta is not null : r.datos.Pago is not null);
                guardada.Error = guardada.Aplicada ? null : r.datos.Error ?? "Central no confirmó la operación.";
                guardada.ClienteCentral = r.datos.Cliente;
                guardada.VentaCentral = r.datos.Venta;
                guardada.PagoCentral = r.datos.Pago;
                await Persistir(copia);
                Estado = copia;
                Cambio?.Invoke();
                if (!guardada.Aplicada) break;
            }
            // Solo sustituir el corte local cuando central confirmó todas las operaciones.
            if (Pendientes == 0 && _api.Sesion?.IdUsuario == _usuario)
            {
                var (actualizada, error) = await _api.DescargarJornadaAsync(d.Asignacion.IdRuta);
                if (actualizada is not null && error is null)
                {
                    var nuevo = new JornadaLocal { Descarga = actualizada, Operaciones = Estado.Operaciones };
                    await Persistir(nuevo);
                    Estado = nuevo;
                }
                else UltimoError = error;
            }
        }
        catch (Exception ex) { UltimoError = ex.Message; }
        finally { Sincronizando = false; _mutex.Release(); Cambio?.Invoke(); }
    }

    private JornadaDescargaDto ValidarUsuario()
    {
        if (!Disponible || _api.Sesion?.IdUsuario != _usuario || !EsUsuarioCampo)
            throw new InvalidOperationException("Inicia sesión con el usuario de la jornada.");
        if (Estado.Descarga is { } descargada && _api.Sesion?.IdZona is { } zona && descargada.Asignacion.IdZona != zona)
            throw new InvalidOperationException("Tu zona cambió. Los pendientes se conservan; solicita su conciliación en central antes de descargar otra jornada.");
        return Estado.Descarga ?? throw new InvalidOperationException("Descarga tu zona con internet antes de trabajar sin conexión.");
    }

    private Task Persistir(JornadaLocal estado) => _almacen.GuardarAsync(_usuario, JsonSerializer.Serialize(estado));

    private async Task Agregar(OperacionJornadaLocal operacion)
    {
        var nuevo = new JornadaLocal { Descarga = Estado.Descarga, Operaciones = [.. Estado.Operaciones, operacion] };
        await Persistir(nuevo);
        Estado = nuevo; // Mostrar éxito únicamente después del commit local.
    }

    private void AlRecuperarConexion()
    {
        _ = RenovarYSincronizarAsync();
        Cambio?.Invoke();
    }

    /// <summary>
    /// Al recuperar señal: primero renueva el token (extiende la ventana offline sin
    /// re-login), luego sincroniza los pendientes de la jornada si corresponde. El refresh
    /// es silencioso; si falla (p. ej. token ya vencido) no se pierde nada y la
    /// sincronización simplemente no avanzará hasta un nuevo login con internet.
    /// </summary>
    private async Task RenovarYSincronizarAsync()
    {
        if (ConInternet) await _api.RefreshAsync();
        if (Estado.Descarga is not null && ConInternet && EsUsuarioCampo) await SincronizarAsync();
    }

    public void Dispose()
    {
        _almacen.ConexionRecuperada -= AlRecuperarConexion;
        _reintentos.Dispose();
    }
}
