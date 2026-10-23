namespace SistemaCobranzaApp.Shared.Dtos;

// =============================================================================
// CORTE DE JORNADA
// DTOs para los tickets de corte del día que captura un usuario de campo.
// Cada DTO lleva TODOS los datos de la jornada; el constructor ESC/POS decide
// qué imprime según el ticket:
//   - "REPORTE" (recaudación / colocación): imprime el detalle línea por línea.
//   - "RESUMEN": imprime solo los totales.
// La ZONA se toma de la sesión y la RUTA del cliente (se envían ya resueltas).
// No se usa logo gráfico: el encabezado es el nombre del negocio en texto.
// =============================================================================

/// <summary>
/// Corte de jornada de COBRO (reporte de recaudación / resumen).
/// </summary>
public sealed record TicketCorteCobroDto(
    string NombreNegocio,
    DateTime Fecha,
    string? Zona,
    string Cobrador,
    IReadOnlyList<TicketCorteCobroLineaDto> Movimientos,
    int TotalTransacciones,
    decimal TotalEfectivoRecaudado);

/// <summary>Una transacción de cobro en el detalle del corte de recaudación.</summary>
public sealed record TicketCorteCobroLineaDto(
    string Folio,
    decimal Monto);

/// <summary>
/// Corte de jornada de VENTA (reporte de colocación / resumen).
/// </summary>
public sealed record TicketCorteVentaDto(
    string NombreNegocio,
    DateTime Fecha,
    string? Zona,
    string Vendedor,
    IReadOnlyList<TicketCorteVentaLineaDto> Ventas,
    int TotalVentasRealizadas,
    decimal TotalValorComercial,
    decimal TotalEnganches)
{
    /// <summary>Cartera financiada = valor comercial menos enganches recaudados.</summary>
    public decimal TotalCarteraFinanciada => TotalValorComercial - TotalEnganches;

    /// <summary>Efectivo a entregar = total de enganches recaudados en ruta.</summary>
    public decimal TotalEfectivoAEntregar => TotalEnganches;
}

/// <summary>Una venta en el detalle del corte de colocación.</summary>
public sealed record TicketCorteVentaLineaDto(
    string Folio,
    decimal ValorVenta,
    decimal Enganche);
