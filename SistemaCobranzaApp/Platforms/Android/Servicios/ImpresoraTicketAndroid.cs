#if ANDROID

using SistemaCobranzaApp.Shared.Services;
using System.Text;
using Android.Bluetooth;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Servicios; // Mantenemos el namespace exacto exigido por tu IDE

public sealed class ImpresoraTicketAndroid : IImpresoraTicket
{
    // Sigue siendo código C# (.cs), pero usando el tipo que el SDK de Android exige:
    private static readonly Java.Util.UUID SppUuid = 
        Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")!;

    public bool Disponible => true;

    public async Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync()
    {
        // En Android 12+ (API 31) BondedDevices exige el permiso runtime
        // BLUETOOTH_CONNECT. Sin él, la lista llega vacía o lanza SecurityException,
        // que es exactamente el síntoma de "no detecta la impresora".
        if (!await PermisosBluetoothAndroid.SolicitarAsync())
            throw new InvalidOperationException(
                "Se requieren los permisos de Bluetooth para detectar impresoras. " +
                "Concede el permiso de \"Dispositivos cercanos\" en los ajustes de la app.");

        var adapter = BluetoothAdapter.DefaultAdapter;

        if (adapter is null)
            return Array.Empty<ImpresoraBluetoothDto>();

        if (!adapter.IsEnabled)
            throw new InvalidOperationException(
                "El Bluetooth está apagado. Actívalo para detectar la impresora.");

        var resultado = new List<ImpresoraBluetoothDto>();

        foreach (var dispositivo in adapter.BondedDevices)
        {
            resultado.Add(
                new ImpresoraBluetoothDto(
                    dispositivo.Name ?? "Bluetooth",
                    dispositivo.Address));
        }

        return await Task.FromResult(
            resultado
                .OrderBy(x => x.Nombre)
                .ToList());
    }

    public async Task<bool> ImprimirAsync(
        ImpresoraBluetoothDto impresora,
        TicketVentaDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirTicket(ticket));
    }

    public async Task<bool> ImprimirPagoAsync(
        ImpresoraBluetoothDto impresora,
        TicketPagoDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirTicketPago(ticket));
    }

    // ===================== CORTES DE JORNADA (botones independientes) =====================
    // Cada corte se imprime por separado desde su propio botón. El DTO lleva todos los datos;
    // el parámetro 'soloResumen' decide si se imprime el detalle o únicamente los totales.

    public async Task<bool> ImprimirCorteCobroReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirCorteCobro(ticket, soloResumen: false));
    }

    public async Task<bool> ImprimirCorteCobroResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirCorteCobro(ticket, soloResumen: true));
    }

    public async Task<bool> ImprimirCorteVentaReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirCorteVenta(ticket, soloResumen: false));
    }

    public async Task<bool> ImprimirCorteVentaResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return await EnviarAImpresoraAsync(impresora, ConstruirCorteVenta(ticket, soloResumen: true));
    }

    // Abre el socket RFCOMM y envía los bytes ya construidos del ticket a la impresora.
    private static async Task<bool> EnviarAImpresoraAsync(
        ImpresoraBluetoothDto impresora,
        byte[] contenido)
    {
        // Asegura el permiso runtime antes de abrir el socket RFCOMM (BLUETOOTH_CONNECT).
        if (!await PermisosBluetoothAndroid.SolicitarAsync())
            throw new InvalidOperationException(
                "Se requieren los permisos de Bluetooth para imprimir el ticket.");

        var adapter = BluetoothAdapter.DefaultAdapter;

        if (adapter is null || !adapter.IsEnabled)
            throw new InvalidOperationException(
                "El Bluetooth del dispositivo no está activado.");

        var dispositivo = adapter
            .BondedDevices
            .FirstOrDefault(x =>
                string.Equals(
                    x.Address,
                    impresora.Direccion,
                    StringComparison.OrdinalIgnoreCase));

        if (dispositivo is null)
            throw new InvalidOperationException(
                "La impresora Bluetooth ya no está vinculada al dispositivo.");

        // =========================================================================
        // 🛠️ MODIFICACIÓN CRÍTICA PARA MOTO G55 (ANDROID 14)
        // Forzamos al teléfono a detener cualquier búsqueda o escaneo de fondo.
        // Sin esta línea, Android 14 cuelga el socket RFCOMM de inmediato por seguridad.
        // =========================================================================
        adapter.CancelDiscovery(); 

        using var socket = dispositivo.CreateRfcommSocketToServiceRecord(SppUuid);

        try
        {
            // En esta línea el LED verde de tu Xprinter pasará a quedarse ENCENDIDO FIJO
            await socket.ConnectAsync();

            using var stream = socket.OutputStream;

            await stream.WriteAsync(contenido, 0, contenido.Length);
            await stream.FlushAsync();

            return true;
        }
        finally
        {
            try
            {
                // Al cerrar el socket el LED se apagará limpiamente (comportamiento normal para no agotar la batería)
                socket.Close();
            }
            catch
            {
                // Ignorar errores al cerrar.
            }
        }
    }
    

// =========================================================================
// COMPROBANTE DE CRÉDITO (venta financiada) — formato de 40 columnas con
// Fuente B (letra pequeña) para que quepan los importes alineados como en el
// diseño aprobado. Mismo método para offline y en línea.
// =========================================================================
private static byte[] ConstruirTicket(TicketVentaDto ticket)
{
    const int ancho = AnchoPequeno; // 40 columnas (Fuente B)
    var bytes = new List<byte>();

    // Inicializar impresora + Fuente B (letra pequeña) + margen superior (~1 cm).
    bytes.AddRange(new byte[] { 0x1B, 0x40 });
    ActivarFuentePequena(bytes);
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0x35 });

    // ---- Cabecera centrada ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, SeparadorFuerte(ancho));
    AgregarEncabezadoNegocio(bytes, ticket.NombreNegocio);
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarLinea(bytes, "COMPROBANTE DE CREDITO");
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, "OPERACION DE VENTA FINANCIADA");
    AgregarLinea(bytes, "CONSERVE ESTE TICKET PARA");
    AgregarLinea(bytes, "SU CONTROL Y ACLARACION");
    AgregarLinea(bytes, SeparadorFuerte(ancho));
    AgregarLinea(bytes, "");

    // ---- Identificación (alineado a la izquierda) ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    AgregarCampo(bytes, "TICKET DE VENTA:", ticket.CodigoVenta, ancho);
    AgregarCampo(bytes, "FECHA Y HORA:", ticket.Fecha.ToString("dd/MM/yyyy HH:mm"), ancho);
    AgregarLinea(bytes, EncabezadoZonaRuta(ticket.Cliente.Zona, ticket.Cliente.Ruta));
    AgregarLinea(bytes, "");

    // ---- Datos del cliente ----
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "DATOS DEL CLIENTE:");
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, $"CLIENTE: {ticket.Cliente.NombreCompleto}");
    if (!string.IsNullOrWhiteSpace(ticket.Cliente.Telefono))
        AgregarLinea(bytes, $"TELEFONO: {ticket.Cliente.Telefono}");
    if (!string.IsNullOrWhiteSpace(ticket.Cliente.Direccion))
        AgregarLinea(bytes, $"DIRECCION: {ticket.Cliente.Direccion}");
    AgregarLinea(bytes, "");

    // ---- Detalle de la venta ----
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "DETALLE DE LA VENTA");
    AgregarLinea(bytes, Separador(ancho));
    foreach (var detalle in ticket.Detalles)
        AgregarDetalle(bytes, detalle.Descripcion, detalle.Cantidad, detalle.Importe, ancho);
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "");

    // ---- Resumen de cuenta ----
    AgregarLinea(bytes, "RESUMEN DE CUENTA:");
    AgregarLinea(bytes, Separador(ancho));
    AgregarImporte(bytes, "SUBTOTAL:", ticket.Subtotal, ancho);
    if (ticket.Descuento > 0)
        AgregarImporte(bytes, "DESCUENTO:", ticket.Descuento, ancho);
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarImporte(bytes, "TOTAL VENTA:", ticket.Total, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

    if (ticket.Enganche > 0)
        AgregarImporte(bytes, "(-) ENGANCHE:", ticket.Enganche, ancho);

    if (ticket.MontoFinanciado > 0)
    {
        AgregarLinea(bytes, Separador(ancho));
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
        AgregarImporte(bytes, "(=) A FINANCIAR:", ticket.MontoFinanciado, ancho);
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        AgregarLinea(bytes, Separador(ancho));
        AgregarLinea(bytes, "");

        // ---- Plan de pago de crédito ----
        AgregarLinea(bytes, "PLAN DE PAGO DE CREDITO:");
        AgregarLinea(bytes, Separador(ancho));
        if (!string.IsNullOrWhiteSpace(ticket.FrecuenciaPago))
            AgregarCampo(bytes, "FRECUENCIA:", ticket.FrecuenciaPago!.ToUpperInvariant(), ancho);
        AgregarCampo(bytes, "TOTAL DE CUOTAS:", ticket.NumeroCuotas.ToString(), ancho);
        AgregarCampo(bytes, "VALOR DE CUOTA:", ticket.CuotaFija.ToString("C2"), ancho);
        if (ticket.FechaPrimerVence.HasValue)
            AgregarCampo(bytes, "PRIMER VENCIMIENTO:", ticket.FechaPrimerVence.Value.ToString("dd/MM/yyyy"), ancho);
        AgregarLinea(bytes, Separador(ancho));
    }

    if (ticket.PlanesPromocionales.Count > 0)
    {
        AgregarLinea(bytes, "");
        AgregarLinea(bytes, "CONDICIONES DE LIQUIDACION:");
        AgregarLinea(bytes, Separador(ancho));
        foreach (var plan in ticket.PlanesPromocionales)
        {
            AgregarLinea(bytes, $"{plan.NumeroMeses} {(plan.NumeroMeses == 1 ? "mes" : "meses")}");
            AgregarImporte(bytes, "Liquidacion:", plan.MontoLiquidacion, ancho);
        }
        AgregarLinea(bytes, Separador(ancho));
    }

    AgregarLinea(bytes, "");

    // ---- Código QR (centrado) ----
    if (!string.IsNullOrWhiteSpace(ticket.CodigoVenta))
        AgregarQRXprinter(bytes, ticket.CodigoVenta);
    // El QR ejecuta ESC @ internamente y reinicia la impresora (vuelve a Fuente A).
    // Reactivamos la Fuente B para que el resto del ticket conserve el ancho de 40 columnas.
    ActivarFuentePequena(bytes);
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, "");

    // ---- Conformidad de pago + firma (izquierda) ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, "CONFORMIDAD DE PAGO");
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "Acepto los terminos del credito y me");
    AgregarLinea(bytes, "comprometo a cumplir el plan de pagos.");
    AgregarLinea(bytes, "");
    AgregarLinea(bytes, "");
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, "FIRMA DEL CLIENTE");
    AgregarLinea(bytes, "");

    // ---- Cierre ----
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarLinea(bytes, "GRACIAS POR SU COMPRA!");
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, "Mantenga sus pagos al corriente para");
    AgregarLinea(bytes, "evitar cargos por moratoriedad.");
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    // Margen inferior (~1 cm) y corte físico.
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0xC5 });
    bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });

    return bytes.ToArray();
}

// =========================================================================
// COMPROBANTE DE PAGO — FORMATO COMPACTO (parámetro TICKET_PAGO_COMPACTO = SI).
// Versión breve: cabecera con el negocio, referencia, cliente, fecha, cobrador,
// y el bloque de saldos (anterior / pago / actual). Ancho 30 columnas (Fuente B).
// =========================================================================
private static byte[] ConstruirTicketPagoCompacto(TicketPagoDto ticket)
{
    // La Fuente B (letra pequeña) imprime ~40 columnas por línea en papel de 58 mm. Usamos ese
    // ancho real para que los separadores y la columna de importes (derecha) queden alineados.
    const int ancho = AnchoPequeno; // 40 columnas (Fuente B)
    var bytes = new List<byte>();

    // Inicializar impresora + Fuente B (letra pequeña) + margen superior (~1 cm).
    bytes.AddRange(new byte[] { 0x1B, 0x40 });
    ActivarFuentePequena(bytes);
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0x35 });

    // ---- Cabecera centrada: nombre del negocio entre separadores fuertes ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, SeparadorFuerte(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    foreach (var linea in PartirEnLineas(
        string.IsNullOrWhiteSpace(ticket.NombreNegocio) ? "SISTEMA DE COBRANZA" : ticket.NombreNegocio.Trim().ToUpperInvariant(), ancho))
        AgregarLinea(bytes, linea);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    // ---- Datos (alineados a la izquierda) ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    AgregarLinea(bytes, $"REF: {ticket.CodigoVenta}");
    AgregarLinea(bytes, $"CTE: {ticket.Cliente.NombreCompleto}");
    AgregarLinea(bytes, $"FECHA: {ticket.Fecha:dd/MM/yyyy HH:mm}");
    if (!string.IsNullOrWhiteSpace(ticket.Cobrador))
        AgregarLinea(bytes, $"COBRADOR: {ticket.Cobrador}");
    AgregarLinea(bytes, Separador(ancho));

    // ---- Saldos ----
    AgregarImporte(bytes, "SALDO ANT:", ticket.SaldoAnterior, ancho);
    if (ticket.MontoAnterior is { } montoAnterior)
        AgregarImporte(bytes, "MONTO ANT:", montoAnterior, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarImporte(bytes, ticket.MontoAnterior is null ? "PAGO REC:" : "MONTO NVO:", ticket.MontoPagado, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarImporte(bytes, "SALDO ACT:", ticket.SaldoActual, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, Separador(ancho));

    // ---- Mensaje de cierre según el movimiento ----
    var movimiento = (ticket.Movimiento ?? string.Empty).ToUpperInvariant();
    var esReversionOActualizacion = movimiento.Contains("REVERSION") || movimiento.Contains("ACTUALIZACION");
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    if (ticket.Liquidada)
        AgregarLinea(bytes, "** CUENTA LIQUIDADA **");
    else if (esReversionOActualizacion)
        AgregarLinea(bytes, "COMPROBANTE DE AUDITORIA");
    else
    {
        AgregarLinea(bytes, "Efectivo recibido. Guarde este");
        AgregarLinea(bytes, "ticket para su comprobacion.");
    }
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    // Margen inferior (~1 cm) y corte.
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0xC5 });
    bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });

    return bytes.ToArray();
}


// =========================================================================
// COMPROBANTE DE PAGO (ABONO) — distinto del ticket de venta.
// Muestra folio, saldo anterior, monto pagado y saldo actual; el QR lleva el
// código de la venta para poder localizar la cuenta al escanearlo.
// =========================================================================
private static byte[] ConstruirTicketPago(TicketPagoDto ticket)
{
    // Según el parámetro del sistema, el comprobante de pago usa el formato compacto o el detallado.
    if (ticket.Compacto)
        return ConstruirTicketPagoCompacto(ticket);

    const int ancho = AnchoPequeno; // 40 columnas (Fuente B)
    var bytes = new List<byte>();

    // Inicializar impresora + Fuente B (letra pequeña) + margen superior (~1 cm).
    bytes.AddRange(new byte[] { 0x1B, 0x40 });
    ActivarFuentePequena(bytes);
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0x35 });

    // ---- Cabecera centrada ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, SeparadorFuerte(ancho));
    AgregarEncabezadoNegocio(bytes, ticket.NombreNegocio);
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarLinea(bytes, "COMPROBANTE DE PAGO");
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, "CONSERVE ESTE TICKET PARA");
    AgregarLinea(bytes, "SU CONTROL Y ACLARACION");
    AgregarLinea(bytes, SeparadorFuerte(ancho));
    AgregarLinea(bytes, "");

    // ---- Identificación (alineado a la izquierda) ----
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    AgregarCampo(bytes, "FOLIO DE PAGO:", ticket.FolioPago ?? string.Empty, ancho);
    AgregarCampo(bytes, "FECHA Y HORA:", ticket.Fecha.ToString("dd/MM/yyyy HH:mm"), ancho);
    AgregarCampo(bytes, "REF. VENTA:", ticket.CodigoVenta, ancho);
    AgregarLinea(bytes, EncabezadoZonaRuta(ticket.Cliente.Zona, ticket.Cliente.Ruta));
    AgregarLinea(bytes, "");

    // ---- Datos del cliente ----
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "DATOS DEL CLIENTE:");
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, $"CLIENTE: {ticket.Cliente.NombreCompleto}");
    if (!string.IsNullOrWhiteSpace(ticket.Cliente.Telefono))
        AgregarLinea(bytes, $"TELEFONO: {ticket.Cliente.Telefono}");
    AgregarLinea(bytes, "");

    // ---- Detalle del pago ----
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "DETALLE DEL PAGO");
    AgregarLinea(bytes, Separador(ancho));
    if (!string.IsNullOrWhiteSpace(ticket.Movimiento))
        AgregarCampo(bytes, "CONCEPTO:", ticket.Movimiento!.ToUpperInvariant(), ancho);
    AgregarCampo(bytes, "ESTADO:", "REGISTRADO", ancho);
    if (!string.IsNullOrWhiteSpace(ticket.Cobrador))
        AgregarCampo(bytes, "COBRADOR:", ticket.Cobrador!, ancho);
    AgregarLinea(bytes, "");

    // ---- Estado de cuenta ----
    AgregarLinea(bytes, "ESTADO DE CUENTA:");
    AgregarLinea(bytes, Separador(ancho));
    AgregarImporte(bytes, "SALDO ANTERIOR:", ticket.SaldoAnterior, ancho);
    if (ticket.MontoAnterior is { } montoAnterior)
        AgregarImporte(bytes, "MONTO ANTERIOR:", montoAnterior, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarImporte(bytes, ticket.MontoAnterior is null ? "PAGO RECIBIDO:" : "MONTO NUEVO:", ticket.MontoPagado, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
    AgregarImporte(bytes, "SALDO ACTUAL:", ticket.SaldoActual, ancho);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "");

    // ---- Código QR (centrado) ----
    if (!string.IsNullOrWhiteSpace(ticket.CodigoVenta))
        AgregarQRXprinter(bytes, ticket.CodigoVenta);
    // El QR ejecuta ESC @ internamente y reinicia la impresora (vuelve a Fuente A).
    // Reactivamos la Fuente B para que el resto del ticket conserve el ancho de 40 columnas.
    ActivarFuentePequena(bytes);
    bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
    AgregarLinea(bytes, "");

    // ---- Recepción de efectivo + firma (izquierda) ----
    bytes.AddRange(new byte[] {0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, "RECEPCION DE EFECTIVO");
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00});
    AgregarLinea(bytes, Separador(ancho));
    AgregarLinea(bytes, "Este documento avala que el cobrador");
    AgregarLinea(bytes, "recibio el monto indicado en este");
    AgregarLinea(bytes, "ticket.");
    AgregarLinea(bytes, "");
    AgregarLinea(bytes, "");
    AgregarLinea(bytes, Separador(ancho));
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, "FIRMA DEL COBRADOR");
    AgregarLinea(bytes, "");

    // ---- Cierre ----
    AgregarLinea(bytes, Separador(ancho));
    var movimiento = (ticket.Movimiento ?? string.Empty).ToUpperInvariant();
    var esReversionOActualizacion = movimiento.Contains("REVERSION")
                                    || movimiento.Contains("ACTUALIZACION");
    string cierre;
    if (ticket.Liquidada)
        cierre = "** CUENTA LIQUIDADA **";
    else if (esReversionOActualizacion)
        cierre = "COMPROBANTE DE AUDITORIA";
    else
        cierre = "GRACIAS POR SU PAGO!";
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, cierre);
    bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
    AgregarLinea(bytes, SeparadorFuerte(ancho));

    // Margen inferior (~1 cm) y corte.
    bytes.AddRange(new byte[] { 0x1B, 0x4A, 0xC5 });
    bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });

    return bytes.ToArray();
}


    // Imprime el nombre del negocio (tomado del ticket / EmpresaGlobal) centrado, en grande y negrita.
    // En doble ancho+alto el ancho útil es ~16 caracteres: partimos el nombre por palabras para
    // que quepa sin recortarse. Si el negocio no tiene nombre configurado, usa el valor por defecto.
    private static void AgregarEncabezadoNegocio(List<byte> bytes, string? nombreNegocio)
    {
        var nombre = string.IsNullOrWhiteSpace(nombreNegocio)
            ? "SISTEMA DE COBRANZA"
            : nombreNegocio.Trim().ToUpperInvariant();

        bytes.AddRange(new byte[] { 0x1D, 0x21, 0x11 }); // Doble ancho + doble alto
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Negrita

        foreach (var linea in PartirEnLineas(nombre, 16))
            AgregarLinea(bytes, linea);

        bytes.AddRange(new byte[] { 0x1D, 0x21, 0x00 }); // Fuente normal
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 }); // Sin negrita
    }

    // Parte un texto en líneas de como máximo 'ancho' caracteres, respetando palabras.
    // Si una palabra sola excede el ancho, se corta de forma dura para no desbordar el papel.
    private static List<string> PartirEnLineas(string texto, int ancho)
    {
        var resultado = new List<string>();
        var limpio = LimpiarTexto(texto).Trim();
        if (limpio.Length == 0) { resultado.Add(string.Empty); return resultado; }

        var actual = new StringBuilder();
        foreach (var palabra in limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = palabra;
            // Palabra más larga que el ancho: trocearla.
            while (p.Length > ancho)
            {
                if (actual.Length > 0) { resultado.Add(actual.ToString()); actual.Clear(); }
                resultado.Add(p[..ancho]);
                p = p[ancho..];
            }

            if (actual.Length == 0) actual.Append(p);
            else if (actual.Length + 1 + p.Length <= ancho) actual.Append(' ').Append(p);
            else { resultado.Add(actual.ToString()); actual.Clear(); actual.Append(p); }
        }
        if (actual.Length > 0) resultado.Add(actual.ToString());
        return resultado;
    }

    private static void AgregarLinea(List<byte> bytes, string texto)
    {
        bytes.AddRange(
            Encoding.GetEncoding(437)
                .GetBytes(LimpiarTexto(texto)));

        bytes.Add(0x0A);
    }

    private static void AgregarImporte(List<byte> bytes, string etiqueta, decimal importe, int ancho = AnchoNormal)
    {
        var valor = importe.ToString("C2");
        var espacios = Math.Max(1, ancho - etiqueta.Length - valor.Length);

        AgregarLinea(
            bytes,
            etiqueta + new string(' ', espacios) + valor);
    }

private static void AgregarDetalle(
    List<byte> bytes,
    string descripcion,
    int cantidad,
    decimal importe,
    int ancho = AnchoNormal)
{
    var valor = importe.ToString("C2"); // Ejemplo: "$10,000.00"
    var prefijo = $"{cantidad}x ";       // Ejemplo: "1x "
    var descripcionLimpia = LimpiarTexto(descripcion);

    // 1. Espacio disponible en la primera línea restando cantidad, precio y 1 espacio de margen
    var espacioRenglonUno = ancho - prefijo.Length - valor.Length - 1;
    if (espacioRenglonUno < 0) espacioRenglonUno = 0;

    // ESCENARIO 1: Cabe completo en la primera línea
    if (descripcionLimpia.Length <= espacioRenglonUno)
    {
        var espacios = Math.Max(1, ancho - prefijo.Length - descripcionLimpia.Length - valor.Length);
        AgregarLinea(bytes, prefijo + descripcionLimpia + new string(' ', espacios) + valor);
    }
    // ESCENARIO 2: Es largo. El precio se queda arriba y el texto se alinea abajo con sangría
    else
    {
        // Cortamos lo que cabe en el primer renglón
        var parteUno = descripcionLimpia[..espacioRenglonUno];
        var restoTexto = descripcionLimpia[espacioRenglonUno..];

        // Imprimimos la primera línea (El precio nunca se romperá)
        var espaciosRenglonUno = Math.Max(1, ancho - prefijo.Length - parteUno.Length - valor.Length);
        AgregarLinea(bytes, prefijo + parteUno + new string(' ', espaciosRenglonUno) + valor);

        // 2. Aplicamos tu regla: El ancho útil de abajo es 32 menos el tamaño de la cantidad
        var anchoUtilAbajo = ancho - prefijo.Length;
        var sangriaEspacios = new string(' ', prefijo.Length); // Espacios en blanco para alinear abajo

        int renglonesImpresos = 1; 
        const int maxRenglonesTotales = 3; // Límite para topar el producto y no gastar papel de más

        while (restoTexto.Length > 0 && renglonesImpresos < maxRenglonesTotales)
        {
            renglonesImpresos++;

            // Si es el último renglón permitido y el texto aún no termina, le ponemos "..." o lo recortamos
            if (renglonesImpresos == maxRenglonesTotales && restoTexto.Length > anchoUtilAbajo)
            {
                var fragmentoFinal = restoTexto[..(anchoUtilAbajo - 1)];
                AgregarLinea(bytes, sangriaEspacios + fragmentoFinal);
                break;
            }

            if (restoTexto.Length <= anchoUtilAbajo)
            {
                AgregarLinea(bytes, sangriaEspacios + restoTexto);
                break;
            }
            else
            {
                var fragmento = restoTexto[..anchoUtilAbajo];
                AgregarLinea(bytes, sangriaEspacios + fragmento);
                restoTexto = restoTexto[anchoUtilAbajo..];
            }
        }
    }
}



    private static string Separador()
    {
        return new string('-', 32);
    }

    private static string LimpiarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        return texto
            .Replace("á", "a")
            .Replace("é", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ú", "u")
            .Replace("Á", "A")
            .Replace("É", "E")
            .Replace("Í", "I")
            .Replace("Ó", "O")
            .Replace("Ú", "U")
            .Replace("ñ", "n")
            .Replace("Ñ", "N");
    }

    private static void AgregarQRXprinter(List<byte> bytes, string datosQr)
    {
        if (string.IsNullOrWhiteSpace(datosQr))
            return;

        // MEJORA 2: Limpieza en caliente para evitar espacios fantasmas
        string qrLimpio = datosQr.Trim().ToUpper();

        // MEJORA 1: Cambiado a ASCII para optimizar el tamaño de los bytes y asegurar la aritmética
        byte[] datos = Encoding.ASCII.GetBytes(qrLimpio);

        // MEJORA 3: Limpiar el búfer para que los residuos del código de barras no rompan el QR
        bytes.AddRange(new byte[] { 0x1B, 0x40 });

        // Centrar el QR.
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });

        // Seleccionar modelo QR 2.
        bytes.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            0x04, 0x00,
            0x31, 0x41, 0x32, 0x00
        });

        // =========================================================================
        // 📐 MODIFICADO: TAMAÑO DEL MÓDULO QR SE INCREMENTÓ A 7 PUNTOS
        // =========================================================================
        // El último byte cambió de 0x05 a 0x07 para expandir la matriz de puntos.
        bytes.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            0x03, 0x00,
            0x31, 0x43, 0x07
        });

        // Corrección de errores nivel M.
        bytes.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            0x03, 0x00,
            0x31, 0x45, 0x31
        });

        // Guardar datos del QR.
        int longitud = datos.Length + 3;

        bytes.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            (byte)(longitud & 0xFF),
            (byte)((longitud >> 8) & 0xFF),
            0x31, 0x50, 0x30
        });

        bytes.AddRange(datos);

        // Imprimir el QR almacenado.
        bytes.AddRange(new byte[]
        {
            0x1D, 0x28, 0x6B,
            0x03, 0x00,
            0x31, 0x51, 0x30
        });
        
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
    }

    // =========================================================================
    // CORTE DE JORNADA DE COBRO — REPORTE DE RECAUDACIÓN / RESUMEN DE JORNADA
    // soloResumen=false -> imprime el detalle de movimientos + totales.
    // soloResumen=true  -> imprime únicamente el resumen (transacciones + recaudado).
    // Ancho útil 32 caracteres (igual que el resto de los tickets de esta impresora).
    // =========================================================================
    private static byte[] ConstruirCorteCobro(TicketCorteCobroDto ticket, bool soloResumen)
    {
        var bytes = new List<byte>();

        // Inicializar impresora + margen superior (~1 cm).
        bytes.AddRange(new byte[] { 0x1B, 0x40 });
        bytes.AddRange(new byte[] { 0x1B, 0x4A, 0x35 });

        // ---- Cabecera centrada ----
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
        AgregarEncabezadoNegocio(bytes, ticket.NombreNegocio);
        AgregarLinea(bytes, "");

        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 }); // Negrita título
        AgregarLinea(bytes, "CORTE DE JORNADA DE COBRO");
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        AgregarLinea(bytes, soloResumen ? "RESUMEN DE JORNADA" : "REPORTE DE RECAUDACION");
        AgregarLinea(bytes, "CONSERVE ESTE TICKET PARA");
        AgregarLinea(bytes, "SU CONTROL Y ACLARACION");
        AgregarLinea(bytes, SeparadorFuerte());
        AgregarLinea(bytes, "");

        // ---- Datos generales (alineados a la izquierda) ----
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
        AgregarCampo(bytes, "FECHA Y HORA:", ticket.Fecha.ToString("dd/MM/yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(ticket.Zona))
            AgregarCampo(bytes, "ZONA DE RUTA:", ticket.Zona!.ToUpperInvariant());
        AgregarLinea(bytes, "");

        AgregarLinea(bytes, Separador());
        AgregarLinea(bytes, "DATOS DEL COBRADOR:");
        AgregarLinea(bytes, Separador());
        AgregarLinea(bytes, ticket.Cobrador);
        AgregarLinea(bytes, "");

        if (!soloResumen)
        {
            // ---- Detalle de movimientos ----
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "DETALLE DE MOVIMIENTOS EN RUTA");
            AgregarLinea(bytes, Separador());
            AgregarImporte(bytes, "FOLIO", "MONTO");
            AgregarLinea(bytes, Separador());

            foreach (var m in ticket.Movimientos)
                AgregarFolioImporte(bytes, m.Folio, m.Monto);

            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, $"TOTAL TRANSACCIONES: {ticket.TotalTransacciones}");
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "");

            AgregarLinea(bytes, "RESUMEN DE RUTA:");
            AgregarLinea(bytes, Separador());
            AgregarImporte(bytes, "TOTAL EFECTIVO RECAUD.:", ticket.TotalEfectivoRecaudado);
            AgregarLinea(bytes, Separador());
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            AgregarImporte(bytes, "TOTAL RECAUDADO:", ticket.TotalEfectivoRecaudado);
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        }
        else
        {
            // ---- Resumen de jornada ----
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "RESUMEN DE JORNADA");
            AgregarLinea(bytes, Separador());
            AgregarImporteEntero(bytes, "TOTAL TRANSACCIONES:", ticket.TotalTransacciones);
            AgregarLinea(bytes, Separador());
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            AgregarImporte(bytes, "TOTAL RECAUDADO:", ticket.TotalEfectivoRecaudado);
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        }

        // ---- Cierre ----
        AgregarLinea(bytes, "");
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
        AgregarLinea(bytes, SeparadorFuerte());
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
        AgregarLinea(bytes, "FIN DE JORNADA / COMPROBADO");
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        AgregarLinea(bytes, SeparadorFuerte());

        // Margen inferior (~1 cm) y corte físico.
        bytes.AddRange(new byte[] { 0x1B, 0x4A, 0xC5 });
        bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });

        return bytes.ToArray();
    }

    // =========================================================================
    // CORTE DE JORNADA DE VENTA — REPORTE DE COLOCACIÓN / RESUMEN DE JORNADA
    // soloResumen=false -> detalle de ventas/enganches + liquidación.
    // soloResumen=true  -> solo totales (ventas, valor colocado, enganches).
    // =========================================================================
    private static byte[] ConstruirCorteVenta(TicketCorteVentaDto ticket, bool soloResumen)
    {
        var bytes = new List<byte>();

        bytes.AddRange(new byte[] { 0x1B, 0x40 });
        bytes.AddRange(new byte[] { 0x1B, 0x4A, 0x35 });

        // ---- Cabecera centrada ----
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
        AgregarEncabezadoNegocio(bytes, ticket.NombreNegocio);
        AgregarLinea(bytes, "");

        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
        AgregarLinea(bytes, "CORTE DE JORNADA DE VENTA");
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        AgregarLinea(bytes, soloResumen ? "RESUMEN DE JORNADA" : "REPORTE DE COLOCACION");
        AgregarLinea(bytes, "CONSERVE ESTE TICKET PARA");
        AgregarLinea(bytes, "SU CONTROL Y ACLARACION");
        AgregarLinea(bytes, SeparadorFuerte());
        AgregarLinea(bytes, "");

        // ---- Datos generales ----
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x00 });
        AgregarCampo(bytes, "FECHA Y HORA:", ticket.Fecha.ToString("dd/MM/yyyy HH:mm"));
        if (!string.IsNullOrWhiteSpace(ticket.Zona))
            AgregarCampo(bytes, "ZONA DE RUTA:", ticket.Zona!.ToUpperInvariant());
        AgregarLinea(bytes, "");

        AgregarLinea(bytes, Separador());
        AgregarLinea(bytes, "DATOS DEL VENDEDOR:");
        AgregarLinea(bytes, Separador());
        AgregarLinea(bytes, ticket.Vendedor);
        AgregarLinea(bytes, "");

        if (!soloResumen)
        {
            // ---- Detalle de ventas y enganches ----
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "DETALLE DE VENTAS Y ENGANCHES");
            AgregarLinea(bytes, Separador());

            foreach (var v in ticket.Ventas)
            {
                // Folio en una línea; valor y enganche en la siguiente, alineados.
                AgregarLinea(bytes, v.Folio);
                AgregarDosImportes(bytes, "  VT:", v.ValorVenta, "ENG:", v.Enganche);
            }

            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, $"TOTAL VENTAS REALIZADAS: {ticket.TotalVentasRealizadas}");
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "");

            AgregarLinea(bytes, "RESUMEN DE RUTA (LIQUIDACION):");
            AgregarLinea(bytes, Separador());
            AgregarImporte(bytes, "TOTAL VALOR COMERCIAL:", ticket.TotalValorComercial);
            AgregarImporte(bytes, "(-) ENGANCHES RECAUD.:", ticket.TotalEnganches);
            AgregarLinea(bytes, Separador());
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            AgregarImporte(bytes, "CARTERA FINANCIADA:", ticket.TotalCarteraFinanciada);
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
            AgregarLinea(bytes, Separador());
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
            AgregarImporte(bytes, "EFECTIVO A ENTREGAR:", ticket.TotalEfectivoAEntregar);
            bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        }
        else
        {
            // ---- Resumen de jornada ----
            AgregarLinea(bytes, Separador());
            AgregarLinea(bytes, "RESUMEN DE JORNADA");
            AgregarLinea(bytes, Separador());
            AgregarImporteEntero(bytes, "TOTAL VENTAS REALIZADAS:", ticket.TotalVentasRealizadas);
            AgregarLinea(bytes, Separador());
            AgregarImporte(bytes, "TOTAL VALOR COLOCADO:", ticket.TotalValorComercial);
            AgregarImporte(bytes, "TOTAL ENGANCHES RECAUD.:", ticket.TotalEnganches);
        }

        // ---- Cierre ----
        AgregarLinea(bytes, "");
        bytes.AddRange(new byte[] { 0x1B, 0x61, 0x01 });
        AgregarLinea(bytes, SeparadorFuerte());
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });
        AgregarLinea(bytes, "FIN DE JORNADA / COMPROBADO");
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });
        AgregarLinea(bytes, SeparadorFuerte());

        bytes.AddRange(new byte[] { 0x1B, 0x4A, 0xC5 });
        bytes.AddRange(new byte[] { 0x1D, 0x56, 0x00 });

        return bytes.ToArray();
    }

    // ---- Helpers exclusivos de los cortes (ancho 32) ----

    // Campo "ETIQUETA:   VALOR" en una sola línea, valor alineado a la derecha.
    private static void AgregarCampo(List<byte> bytes, string etiqueta, string valor, int ancho = AnchoNormal)
    {
        var val = LimpiarTexto(valor);
        var espacios = Math.Max(1, ancho - etiqueta.Length - val.Length);
        AgregarLinea(bytes, etiqueta + new string(' ', espacios) + val);
    }

    // Dos textos alineados a los extremos (encabezado de columnas: "FOLIO ... MONTO").
    private static void AgregarImporte(List<byte> bytes, string izquierda, string derecha, int ancho = AnchoNormal)
    {
        var der = LimpiarTexto(derecha);
        var espacios = Math.Max(1, ancho - izquierda.Length - der.Length);
        AgregarLinea(bytes, izquierda + new string(' ', espacios) + der);
    }

    // Etiqueta + total entero alineado a la derecha.
    private static void AgregarImporteEntero(List<byte> bytes, string etiqueta, int total, int ancho = AnchoNormal)
    {
        var val = total.ToString();
        var espacios = Math.Max(1, ancho - etiqueta.Length - val.Length);
        AgregarLinea(bytes, etiqueta + new string(' ', espacios) + val);
    }

    // Folio (posiblemente largo) a la izquierda y su importe alineado a la derecha.
    // Si folio + importe no caben, el folio se recorta para no desbordar.
    private static void AgregarFolioImporte(List<byte> bytes, string folio, decimal importe, int ancho = AnchoNormal)
    {
        var valor = importe.ToString("C2");
        var folioLimpio = LimpiarTexto(folio);
        var maxFolio = ancho - valor.Length - 1;
        if (maxFolio < 1) maxFolio = 1;
        if (folioLimpio.Length > maxFolio) folioLimpio = folioLimpio[..maxFolio];
        var espacios = Math.Max(1, ancho - folioLimpio.Length - valor.Length);
        AgregarLinea(bytes, folioLimpio + new string(' ', espacios) + valor);
    }

    // Dos pares etiqueta/importe en una línea: "  VT: $x,xxx.xx   ENG: $xxx.xx".
    private static void AgregarDosImportes(List<byte> bytes, string etqIzq, decimal izq, string etqDer, decimal der, int ancho = AnchoNormal)
    {
        var textoDer = $"{etqDer} {der:C2}";
        var textoIzq = $"{etqIzq} {izq:C2}";
        var espacios = Math.Max(1, ancho - textoIzq.Length - textoDer.Length);
        AgregarLinea(bytes, textoIzq + new string(' ', espacios) + textoDer);
    }

    // ===================== Anchos y fuentes =====================
    // AnchoNormal: Fuente A (letra estándar), ~32 columnas en papel de 58 mm. Lo usan los cortes.
    // AnchoPequeno: Fuente B (letra pequeña), ~40 columnas. Lo usan los comprobantes de crédito y pago.
    private const int AnchoNormal = 32;
    private const int AnchoPequeno = 40;

    // Activa la Fuente B (letra pequeña) de la impresora: ESC M 1.
    private static void ActivarFuentePequena(List<byte> bytes)
        => bytes.AddRange(new byte[] { 0x1B, 0x4D, 0x01 });

    // Línea "ZONA: X | RUTA: Y" (zona de la sesión, ruta del cliente). Omite la parte vacía.
    private static string EncabezadoZonaRuta(string? zona, string? ruta)
    {
        var z = string.IsNullOrWhiteSpace(zona) ? null : $"ZONA: {zona!.ToUpperInvariant()}";
        var r = string.IsNullOrWhiteSpace(ruta) ? null : $"RUTA: {ruta!.ToUpperInvariant()}";
        return (z, r) switch
        {
            (not null, not null) => $"{z} | {r}",
            (not null, null) => z!,
            (null, not null) => r!,
            _ => string.Empty
        };
    }

    // Separador de '-' con el ancho indicado (línea de sección).
    private static string Separador(int ancho) => new string('-', ancho);

    // Separador fuerte de '=' con el ancho indicado (cabecera y cierre).
    private static string SeparadorFuerte(int ancho = AnchoNormal) => new string('=', ancho);

}
#endif