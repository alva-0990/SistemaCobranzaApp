using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Services;

namespace SistemaCobranzaApp.Web.Services;

/// <summary>
/// Implementación de <see cref="IImpresoraTicket"/> para la plataforma Web.
/// Un navegador no tiene acceso directo a impresoras Bluetooth, por lo que
/// esta implementación actúa como un stub que reporta la impresión como no disponible.
/// </summary>
public sealed class ImpresoraTicketWeb : IImpresoraTicket
{
    public bool Disponible => false;

    public Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync()
    {
        return Task.FromResult<IReadOnlyList<ImpresoraBluetoothDto>>(
            Array.Empty<ImpresoraBluetoothDto>());
    }

    public Task<bool> ImprimirAsync(
        ImpresoraBluetoothDto impresora,
        TicketVentaDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ImprimirPagoAsync(
        ImpresoraBluetoothDto impresora,
        TicketPagoDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ImprimirCorteCobroReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ImprimirCorteCobroResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteCobroDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ImprimirCorteVentaReporteAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ImprimirCorteVentaResumenAsync(
        ImpresoraBluetoothDto impresora,
        TicketCorteVentaDto ticket)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ProbarAsync(
        ImpresoraBluetoothDto impresora)
    {
        return Task.FromResult(false);
    }
}
