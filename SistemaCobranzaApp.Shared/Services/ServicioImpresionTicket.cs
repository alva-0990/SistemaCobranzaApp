using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public sealed class ServicioImpresionTicket
{
    private readonly IImpresoraTicket _impresora;

    public ServicioImpresionTicket(IImpresoraTicket impresora)
    {
        _impresora = impresora;
    }

    public bool Disponible => _impresora.Disponible;

    public Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync()
    {
        return _impresora.ObtenerImpresorasAsync();
    }

    public Task<bool> ImprimirAsync(
        ImpresoraBluetoothDto impresora,
        TicketVentaDto ticket)
    {
        return _impresora.ImprimirAsync(impresora, ticket);
    }

    public Task<bool> ImprimirPagoAsync(
        ImpresoraBluetoothDto impresora,
        TicketPagoDto ticket)
    {
        return _impresora.ImprimirPagoAsync(impresora, ticket);
    }

    public Task<bool> ImprimirCorteCobroReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return _impresora.ImprimirCorteCobroReporteAsync(impresora, ticket);
    }

    public Task<bool> ImprimirCorteCobroResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return _impresora.ImprimirCorteCobroResumenAsync(impresora, ticket);
    }

    public Task<bool> ImprimirCorteVentaReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return _impresora.ImprimirCorteVentaReporteAsync(impresora, ticket);
    }

    public Task<bool> ImprimirCorteVentaResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return _impresora.ImprimirCorteVentaResumenAsync(impresora, ticket);
    }
}