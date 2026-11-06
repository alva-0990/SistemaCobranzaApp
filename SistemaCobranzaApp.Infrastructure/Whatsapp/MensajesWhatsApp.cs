using System.Globalization;
using System.Text;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Textos corporativos (sin emojis, formato 24 h) de los comprobantes enviados por WhatsApp.
/// Reciben los mismos DTO del ticket impreso (<see cref="TicketPagoDto"/> / <see cref="TicketVentaDto"/>)
/// para que el mensaje y el ticket muestren exactamente los mismos datos.
/// La moneda se formatea siempre en es-MX para no depender de la cultura del servidor/contenedor.
/// </summary>
public static class MensajesWhatsApp
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public static string ConstruirTextoWhatsAppPago(TicketPagoDto ticket)
    {
        // Formatear las cantidades a formato de moneda institucional
        string saldoAnterior = ticket.SaldoAnterior.ToString("C2", Mx);
        string montoPagado = ticket.MontoPagado.ToString("C2", Mx);
        string saldoActual = ticket.SaldoActual.ToString("C2", Mx);

        // Formatear la fecha de forma legible y formal (24 horas)
        string fechaFormateada = ticket.Fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        // Mensaje dinámico institucional según el estado de la cuenta
        string mensajeLiquidada = ticket.MontoCondonado > 0
            ? "*Nota:* Por acuerdo de liquidación se condonó el saldo restante. Esta cuenta ha sido liquidada en su totalidad."
            : ticket.Liquidada
            ? "*Nota:* Esta cuenta ha sido liquidada en su totalidad."
            : "*Nota:* Su cuenta mantiene un saldo pendiente. Agradecemos continuar con sus abonos puntuales.";

        // Construcción del cuerpo del mensaje limpio
        var sb = new StringBuilder();
        sb.AppendLine($"*{ticket.NombreNegocio.ToUpper()}*");
        sb.AppendLine("*COMPROBANTE OFICIAL DE PAGO*");
        sb.AppendLine();
        sb.AppendLine($"Estimado(a) *{ticket.Cliente.NombreCompleto}*, confirmamos que su pago ha sido registrado con éxito en nuestro sistema central. A continuación, se detallan los datos de la transacción:");
        sb.AppendLine();
        sb.AppendLine($"*Folio de Pago:* #{ticket.FolioPago}");
        sb.AppendLine($"*Fecha/Hora:* {fechaFormateada}");
        sb.AppendLine($"*Código de Venta:* {ticket.CodigoVenta}");
        sb.AppendLine($"*Cobrador:* {ticket.Cobrador}");
        sb.AppendLine();
        sb.AppendLine("*ESTADO DE CUENTA ACTUALIZADO*");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"*Saldo Anterior:* {saldoAnterior}");
        sb.AppendLine($"*Monto Abonado:* {montoPagado}");
        if (ticket.MontoBonificado > 0)
            sb.AppendLine($"*Bonificación Aplicada:* {ticket.MontoBonificado.ToString("C2", Mx)}");
        if (ticket.MontoCondonado > 0)
            sb.AppendLine($"*Saldo Condonado:* {ticket.MontoCondonado.ToString("C2", Mx)}");
        sb.AppendLine($"*Saldo Actual:* {saldoActual}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine(mensajeLiquidada);
        sb.AppendLine();
        sb.AppendLine("Agradecemos su confianza y su compromiso por mantener su cuenta al corriente.");
        sb.AppendLine();
        sb.AppendLine("_Este es un mensaje automático de control emitido por el sistema de cobranza. Si desea revocar las notificaciones por este medio, responda con la palabra BAJA._");

        return sb.ToString();
    }

    public static string ConstruirTextoWhatsAppVenta(TicketVentaDto ticket)
    {
        // Formatear las variables financieras a moneda contable
        string total = ticket.Total.ToString("C2", Mx);
        string enganche = ticket.Enganche.ToString("C2", Mx);
        string financiado = ticket.MontoFinanciado.ToString("C2", Mx);
        string cuota = ticket.CuotaFija.ToString("C2", Mx);

        string fechaFormateada = ticket.Fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine($"*{ticket.NombreNegocio.ToUpper()}*");
        sb.AppendLine(ticket.EsContado ? "*COMPROBANTE OFICIAL DE VENTA DE CONTADO*" : "*COMPROBANTE OFICIAL DE VENTA Y CRÉDITO*");
        sb.AppendLine();
        sb.AppendLine($"Estimado(a) *{ticket.Cliente.NombreCompleto}*, confirmamos el registro exitoso de su compra en nuestro sistema. A continuación se detallan los datos de su nota:");
        sb.AppendLine();
        sb.AppendLine($"*Código de Venta:* {ticket.CodigoVenta}");
        sb.AppendLine($"*Fecha/Hora:* {fechaFormateada}");
        sb.AppendLine();

        // DETALLE DE ARTÍCULOS
        sb.AppendLine("*ARTÍCULOS INCLUIDOS:*");
        sb.AppendLine("----------------------------------------");
        foreach (var art in ticket.Detalles)
        {
            // Ejemplo: 2 x Producto Descripcion ($500.00 c/u) = $1,000.00
            sb.AppendLine($"{art.Cantidad} x {art.Descripcion} ({art.Precio.ToString("C2", Mx)} c/u) = {art.Importe.ToString("C2", Mx)}");
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"*TOTAL DE LA VENTA:* {total}");
        if (ticket.EsContado)
        {
            sb.AppendLine("*Forma de pago:* CONTADO (pagado en efectivo)");
        }
        else
        {
            sb.AppendLine($"*Enganche Recibido:* {enganche}");
            sb.AppendLine($"*Monto Financiado:* {financiado}");
        }
        sb.AppendLine();

        // PLAN DE PAGOS (Si aplica financiamiento)
        if (ticket.NumeroCuotas > 0)
        {
            string primerVencimiento = ticket.FechaPrimerVence.HasValue
                ? ticket.FechaPrimerVence.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "N/A";

            sb.AppendLine("*PLAN DE FINANCIAMIENTO:*");
            sb.AppendLine($"*Estructura:* {ticket.NumeroCuotas} cuotas de {cuota}");
            sb.AppendLine($"*Frecuencia de Pago:* {ticket.FrecuenciaPago ?? "CONVENIDA"}");
            sb.AppendLine($"*Primer Vencimiento:* {primerVencimiento}");
            sb.AppendLine("----------------------------------------");
        }

        sb.AppendLine(ticket.EsContado
            ? "Agradecemos su preferencia. Su compra quedó pagada en su totalidad."
            : "Agradecemos su preferencia y le invitamos a mantener sus compromisos de pago al corriente.");
        sb.AppendLine();
        sb.AppendLine("_Este es un mensaje automático de control emitido por el sistema de cobranza. Si desea revocar las notificaciones por este medio, responda con la palabra BAJA._");

        return sb.ToString();
    }
}
