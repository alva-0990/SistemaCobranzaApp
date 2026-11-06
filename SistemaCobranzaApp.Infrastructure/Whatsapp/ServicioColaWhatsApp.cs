using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Pages;

namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Encola mensajes de WhatsApp en la tabla de paso colamensajeswhatsapp.
///
/// El texto se arma con <see cref="MensajesWhatsApp"/> a partir de los mismos DTO del ticket
/// impreso (<see cref="TicketPagoDto"/> / <see cref="TicketVentaDto"/>), construidos aquí con
/// los datos ya guardados en la base dentro de la transacción del llamador.
///
/// Interruptores (tabla parametrossistema, administrables en caliente):
///   * WHATSAPP_ENVIO_ACTIVO    interruptor maestro; si es 'NO', no se encola nada.
///   * WHATSAPP_NOTIFICAR_VENTA / WHATSAPP_NOTIFICAR_PAGO  interruptores por tipo.
///
/// IMPORTANTE sobre la transacción:
///   * EncolarVentaAsync / EncolarPagoAsync solo hacen Add sobre el MISMO DbContext del
///     flujo que las invoca; NO llaman a SaveChanges ni abren/cierran transacción. El
///     SaveChanges/Commit lo controla el servicio dueño (ServicioVentasAdmin /
///     ServicioCobranza), de modo que el mensaje se persiste de forma atómica junto con
///     la venta/pago.
///   * La señal al worker (Senal.Avisar) debe dispararse DESPUÉS del commit; por eso este
///     servicio NO la dispara: expone Senal para que el llamador avise tras confirmar.
///
/// No se encola nada si el cliente tiene NotificarWhatsApp = 0 o no tiene teléfono utilizable.
/// </summary>
public class ServicioColaWhatsApp
{
    private const string NegocioPorDefecto = "Sistema de Cobranza";

    private readonly CobranzaDbContext _db;
    private readonly LectorParametrosWhatsApp _parametros;

    public ServicioColaWhatsApp(CobranzaDbContext db, SenalColaWhatsApp senal, LectorParametrosWhatsApp parametros)
    {
        _db = db;
        Senal = senal;
        _parametros = parametros;
    }

    /// <summary>Detonante compartido; el llamador debe invocar Senal.Avisar() tras el commit.</summary>
    public SenalColaWhatsApp Senal { get; }

    /// <summary>
    /// Encola la notificación de una venta registrada. La venta, sus detalles y su número de
    /// cuotas deben estar ya guardados (SaveChanges) en la transacción del llamador.
    /// Devuelve true si se agregó una fila a la cola (pendiente de SaveChanges por el llamador).
    /// </summary>
    public async Task<bool> EncolarVentaAsync(long idVenta, CancellationToken ct = default)
    {
        var config = await _parametros.ObtenerAsync(ct);
        if (!config.EnvioActivo || !config.NotificarVenta)
            return false;

        var venta = await _db.Ventas.AsNoTracking()
            .Where(v => v.IdVenta == idVenta)
            .Select(v => new
            {
                v.IdVenta, v.IdCliente, v.CodigoVenta, v.FechaCompra, v.Subtotal, v.DescuentoGlobal,
                v.TotalFinal, v.Enganche, v.MontoCuotaFija, v.NumeroCuotas, v.FechaPrimerVence, v.IdFrecuenciaPago, v.IdCondicion
            })
            .FirstOrDefaultAsync(ct);
        if (venta is null) return false;

        var contacto = await ObtenerContactoAsync(venta.IdCliente, ct);
        if (contacto is null) return false;

        var detalles = await (
                from d in _db.DetallesVenta.AsNoTracking()
                join p in _db.Productos.AsNoTracking() on d.IdProducto equals p.IdProducto into gp
                from p in gp.DefaultIfEmpty()
                where d.IdVenta == idVenta
                select new TicketDetalleDto(
                    p != null ? p.Descripcion : "Producto #" + d.IdProducto,
                    d.Cantidad, d.PrecioCobrado, d.Importe))
            .ToListAsync(ct);

        string? frecuencia = venta.IdFrecuenciaPago is null
            ? null
            : await _db.CatalogoFrecuenciasPago.AsNoTracking()
                .Where(f => f.IdFrecuenciaPago == venta.IdFrecuenciaPago)
                .Select(f => f.Descripcion)
                .FirstOrDefaultAsync(ct);

        // Venta de contado: se cobra completa al momento, no hay saldo financiado ni plan de pagos.
        var esContado = await _db.Condiciones.AsNoTracking()
            .Where(c => c.IdCondicion == venta.IdCondicion && c.Descripcion.ToUpper().Contains("CONTADO"))
            .AnyAsync(ct);
        var montoFinanciado = esContado ? 0m : venta.TotalFinal - venta.Enganche;

        var ticket = new TicketVentaDto(
            NombreNegocio: await ObtenerNombreNegocioAsync(ct),
            CodigoVenta: string.IsNullOrWhiteSpace(venta.CodigoVenta) ? $"V{venta.IdVenta}" : venta.CodigoVenta!,
            Fecha: venta.FechaCompra.EnZonaProyecto().DateTime,
            Cliente: contacto.Ticket,
            Detalles: detalles,
            Subtotal: venta.Subtotal,
            Descuento: venta.DescuentoGlobal,
            Total: venta.TotalFinal,
            Enganche: venta.Enganche,
            MontoFinanciado: montoFinanciado,
            FrecuenciaPago: frecuencia,
            CuotaFija: venta.MontoCuotaFija,
            // Sin saldo financiado no hay plan de pagos que mostrar.
            NumeroCuotas: montoFinanciado > 0 ? venta.NumeroCuotas : 0,
            FechaPrimerVence: venta.FechaPrimerVence,
            PlanesPromocionales: Array.Empty<TicketPlanDto>())
        {
            EsContado = esContado
        };

        _db.ColaMensajesWhatsApp.Add(new ColaMensajeWhatsApp
        {
            Telefono = contacto.Telefono,
            Mensaje = MensajesWhatsApp.ConstruirTextoWhatsAppVenta(ticket),
            Estado = EstadoMensajeWhatsApp.Pendiente,
            TipoOrigen = TipoOrigenWhatsApp.Venta,
            IdVenta = idVenta,
            FechaCreacion = DateTimeOffset.UtcNow
        });
        return true;
    }

    /// <summary>
    /// Encola la notificación de un pago/abono aplicado, a partir de la respuesta final del registro
    /// (saldos del trigger y, en liquidación por acuerdo, condonación y bonificación incluidas).
    /// Devuelve true si se agregó una fila a la cola (pendiente de SaveChanges por el llamador).
    /// </summary>
    public async Task<bool> EncolarPagoAsync(RegistrarPagoResponse respuesta, CancellationToken ct = default)
    {
        var config = await _parametros.ObtenerAsync(ct);
        if (!config.EnvioActivo || !config.NotificarPago)
            return false;

        var pago = await (
                from p in _db.Pagos.AsNoTracking()
                join v in _db.Ventas.AsNoTracking() on p.IdVenta equals v.IdVenta
                where p.IdPago == respuesta.IdPago
                select new
                {
                    p.IdPago, p.IdVenta, v.IdCliente, v.CodigoVenta, p.FolioPago, p.FechaPago,
                    p.IdUsuarioRegistro, p.IdCobrador
                })
            .FirstOrDefaultAsync(ct);
        if (pago is null) return false;

        var contacto = await ObtenerContactoAsync(pago.IdCliente, ct);
        if (contacto is null) return false;

        var ticket = new TicketPagoDto(
            NombreNegocio: await ObtenerNombreNegocioAsync(ct),
            FolioPago: string.IsNullOrWhiteSpace(pago.FolioPago) ? pago.IdPago.ToString() : pago.FolioPago!,
            Fecha: pago.FechaPago.EnZonaProyecto().DateTime,
            Cliente: contacto.Ticket,
            CodigoVenta: string.IsNullOrWhiteSpace(pago.CodigoVenta) ? $"V{pago.IdVenta}" : pago.CodigoVenta!,
            IdVenta: pago.IdVenta,
            SaldoAnterior: respuesta.SaldoAnterior,
            MontoPagado: respuesta.MontoAbona,
            SaldoActual: respuesta.SaldoActual,
            Movimiento: "ABONO",
            Cobrador: await ObtenerNombreCobradorAsync(pago.IdUsuarioRegistro, pago.IdCobrador, ct),
            Liquidada: respuesta.Liquidada,
            MontoCondonado: respuesta.MontoCondonado,
            MontoBonificado: respuesta.MontoBonificado);

        _db.ColaMensajesWhatsApp.Add(new ColaMensajeWhatsApp
        {
            Telefono = contacto.Telefono,
            Mensaje = MensajesWhatsApp.ConstruirTextoWhatsAppPago(ticket),
            Estado = EstadoMensajeWhatsApp.Pendiente,
            TipoOrigen = TipoOrigenWhatsApp.Pago,
            IdPago = pago.IdPago,
            IdVenta = pago.IdVenta,
            FechaCreacion = DateTimeOffset.UtcNow
        });
        return true;
    }

    private sealed record Contacto(string Telefono, ClienteTicketDto Ticket);

    /// <summary>
    /// Datos del cliente para el mensaje. Null si el cliente no existe, se dio de baja de
    /// WhatsApp (NotificarWhatsApp = 0) o no tiene un teléfono utilizable.
    /// </summary>
    private async Task<Contacto?> ObtenerContactoAsync(long idCliente, CancellationToken ct)
    {
        var c = await _db.Clientes.AsNoTracking()
            .Where(x => x.IdCliente == idCliente)
            .Select(x => new
            {
                x.Nombre, x.ApPaterno, x.ApMaterno, x.TelefonoPrincipal, x.TelefonoSecundario,
                x.Direccion, x.NotificarWhatsApp, Ruta = x.Ruta != null ? x.Ruta.Descripcion : null
            })
            .FirstOrDefaultAsync(ct);

        if (c is null || c.NotificarWhatsApp != Estatus.Activo) return null;

        var telefono = NormalizarTelefono(c.TelefonoPrincipal) ?? NormalizarTelefono(c.TelefonoSecundario);
        if (telefono is null) return null;

        var nombre = string.Join(" ", new[] { c.Nombre, c.ApPaterno, c.ApMaterno }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(nombre)) nombre = "cliente";

        return new Contacto(telefono,
            new ClienteTicketDto(nombre, c.TelefonoPrincipal, c.Direccion, c.Ruta, Zona: null));
    }

    /// <summary>
    /// Nombre del negocio para el encabezado: NombreCorto y, si no hay, NombreComercial
    /// (mismo criterio que el ticket impreso). "Sistema de Cobranza" si no hay empresa.
    /// </summary>
    private async Task<string> ObtenerNombreNegocioAsync(CancellationToken ct)
    {
        var e = await _db.Empresas.AsNoTracking()
            .Select(x => new { x.NombreCorto, x.NombreComercial })
            .FirstOrDefaultAsync(ct);
        var nombre = !string.IsNullOrWhiteSpace(e?.NombreCorto) ? e!.NombreCorto : e?.NombreComercial;
        return string.IsNullOrWhiteSpace(nombre) ? NegocioPorDefecto : nombre.Trim();
    }

    /// <summary>
    /// Cobrador del comprobante: el usuario que registró el pago (igual que el ticket impreso,
    /// que usa el usuario logueado) y, como respaldo, el cobrador asignado al pago.
    /// </summary>
    private async Task<string?> ObtenerNombreCobradorAsync(long? idUsuario, long? idCobrador, CancellationToken ct)
    {
        if (idUsuario is not null)
        {
            var nombre = await _db.Usuarios.AsNoTracking()
                .Where(u => u.IdUsuario == idUsuario)
                .Select(u => u.NombreCompleto)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(nombre)) return nombre;
        }

        if (idCobrador is not null)
            return await _db.Cobradores.AsNoTracking()
                .Where(c => c.IdCobrador == idCobrador)
                .Select(c => c.Nombre)
                .FirstOrDefaultAsync(ct);

        return null;
    }

    /// <summary>
    /// Deja el número listo para Evolution API: solo dígitos, últimos 10 y prefijo de país 52.
    /// Devuelve null si no hay al menos 10 dígitos.
    /// </summary>
    public static string? NormalizarTelefono(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var digitos = new string(valor.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length < 10) return null;
        return "52" + digitos[^10..];
    }
}
