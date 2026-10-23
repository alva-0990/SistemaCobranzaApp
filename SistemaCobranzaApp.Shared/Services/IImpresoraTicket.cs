
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public interface IImpresoraTicket
{
    bool Disponible { get; }

    Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync();

    Task<bool> ImprimirAsync(
        ImpresoraBluetoothDto impresora,
        TicketVentaDto ticket);

    /// <summary>Imprime un comprobante de pago (abono) con su QR del código de venta.</summary>
    Task<bool> ImprimirPagoAsync(
        ImpresoraBluetoothDto impresora,
        TicketPagoDto ticket);

    /// <summary>Corte de jornada de COBRO — REPORTE DE RECAUDACIÓN (detalle de movimientos + totales).</summary>
    Task<bool> ImprimirCorteCobroReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket);

    /// <summary>Corte de jornada de COBRO — RESUMEN DE JORNADA (solo totales).</summary>
    Task<bool> ImprimirCorteCobroResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket);

    /// <summary>Corte de jornada de VENTA — REPORTE DE COLOCACIÓN (detalle de ventas/enganches + liquidación).</summary>
    Task<bool> ImprimirCorteVentaReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket);

    /// <summary>Corte de jornada de VENTA — RESUMEN DE JORNADA (solo totales).</summary>
    Task<bool> ImprimirCorteVentaResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket);
}

public sealed record ImpresoraBluetoothDto(
    string Nombre,
    string Direccion);