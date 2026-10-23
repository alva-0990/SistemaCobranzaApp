using SistemaCobranzaApp.Servicios;
using SistemaCobranzaApp.Shared.Services;
using SistemaCobranzaApp.Shared.Dtos;

#if ANDROID
using Android.Bluetooth;
using Java.Util;
using System.IO;
#endif

namespace SistemaCobranzaApp.Servicios; 

public class AndroidPrinterService : IImpresoraTicket
{
#if ANDROID
    private BluetoothSocket _socket;
    private Stream _outputStream;
    private static readonly UUID SppUuid = UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");

    public bool Disponible => BluetoothAdapter.DefaultAdapter != null && BluetoothAdapter.DefaultAdapter.IsEnabled;

    // --- Implementación de IPrinterService (Control del Socket para mantener LED encendido) ---
    
    public async Task<bool> CheckPermissionsAsync()
    {
        // CORREGIDO: Al estar en el mismo namespace, ahora resuelve perfectamente la clase estática
        return await PermisosBluetoothAndroid.SolicitarAsync();
    }

    public bool Conectar(string macAddress)
    {
        try
        {
            BluetoothAdapter adapter = BluetoothAdapter.DefaultAdapter;
            if (adapter == null || !adapter.IsEnabled) return false;

            BluetoothDevice device = adapter.GetRemoteDevice(macAddress);
            _socket = device.CreateRfcommSocketToServiceRecord(SppUuid);

            adapter.CancelDiscovery(); // Crucial para Moto G55 (Android 14)
            _socket.Connect();         // El LED verde se enciende y queda FIJO aquí
            _outputStream = _socket.OutputStream;

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error de conexión socket: {ex.Message}");
            Desconectar();
            return false;
        }
    }

    public void Imprimir(string texto)
    {
        if (_outputStream != null && _socket is { IsConnected: true })
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(texto + "\n");
            _outputStream.Write(bytes, 0, bytes.Length);
            _outputStream.Flush();
        }
    }

    public void Desconectar()
    {
        _outputStream?.Close();
        _socket?.Close();
        _outputStream = null;
        _socket = null;
    }

    // --- Implementación de IImpresoraTicket (Lógica de Alto Nivel para tus DTOs) ---

    public async Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync()
    {
        var lista = new List<ImpresoraBluetoothDto>();
        
        bool tienePermisos = await CheckPermissionsAsync();
        if (!tienePermisos) return lista;

        BluetoothAdapter adapter = BluetoothAdapter.DefaultAdapter;
        if (adapter?.BondedDevices != null)
        {
            foreach (var device in adapter.BondedDevices)
            {
                lista.Add(new ImpresoraBluetoothDto(device.Name, device.Address));
            }
        }
        return lista;
    }

    public async Task<bool> ImprimirAsync(ImpresoraBluetoothDto impresora, TicketVentaDto ticket)
    {
        // 1. Abrir canal continuo para que no se apague el estatus de la Xprinter
        if (!Conectar(impresora.Direccion)) return false;

        try
        {
            // 2. Mapear y enviar tus datos del DTO usando comandos ESC/POS básicos
            Imprimir("      TICKET DE VENTA       ");
            Imprimir($"Cliente: {ticket.Cliente}"); // Ajusta según las propiedades reales de tu TicketVentaDto
            Imprimir($"Total: {ticket.Total:C}");
            Imprimir("\n\n\n"); // Espacio físico para el corte de papel
            
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // 3. Liberar la impresora de forma limpia
            Desconectar();
        }
    }

    public async Task<bool> ProbarAsync(ImpresoraBluetoothDto impresora)
    {
        if (!Conectar(impresora.Direccion)) return false;
        
        Imprimir("Prueba de Conexión Exitosa");
        Imprimir($"Dispositivo: {impresora.Nombre}");
        Imprimir("\n\n");
        
        Desconectar();
        return true;
    }

    public async Task<bool> ImprimirPagoAsync(ImpresoraBluetoothDto impresora, TicketPagoDto ticket)
    {
        if (!Conectar(impresora.Direccion)) return false;

        try
        {
            Imprimir("     COMPROBANTE DE PAGO     ");
            Imprimir($"Folio: {ticket.FolioPago}");
            Imprimir($"Fecha: {ticket.Fecha:dd/MM/yyyy HH:mm}");
            Imprimir($"Venta: {ticket.CodigoVenta}");
            Imprimir($"Cliente: {ticket.Cliente.NombreCompleto}");
            Imprimir($"Saldo anterior: {ticket.SaldoAnterior:C}");
            Imprimir($"Pago: {ticket.MontoPagado:C}");
            Imprimir($"Saldo actual: {ticket.SaldoActual:C}");
            Imprimir(ticket.Liquidada ? "** CUENTA LIQUIDADA **" : "GRACIAS POR SU PAGO");
            Imprimir("\n\n\n");
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Desconectar();
        }
    }

    // Cortes de jornada: esta implementación alterna (no registrada en DI) no soporta
    // el formato ESC/POS de los cortes; reporta no-impreso para cumplir la interfaz.
    public Task<bool> ImprimirCorteCobroReporteAsync(ImpresoraBluetoothDto impresora, TicketCorteCobroDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteCobroResumenAsync(ImpresoraBluetoothDto impresora, TicketCorteCobroDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteVentaReporteAsync(ImpresoraBluetoothDto impresora, TicketCorteVentaDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteVentaResumenAsync(ImpresoraBluetoothDto impresora, TicketCorteVentaDto ticket) => Task.FromResult(false);
#else
    // Implementación stub requerida por MAUI para compilar de forma cruzada en otras plataformas
    public bool Disponible => false;
    public Task<bool> CheckPermissionsAsync() => Task.FromResult(false);
    public bool Conectar(string macAddress) => false;
    public void Imprimir(string texto) { }
    public void Desconectar() { }
    public Task<IReadOnlyList<ImpresoraBluetoothDto>> ObtenerImpresorasAsync() => Task.FromResult<IReadOnlyList<ImpresoraBluetoothDto>>(new List<ImpresoraBluetoothDto>());
    public Task<bool> ImprimirAsync(ImpresoraBluetoothDto impresora, TicketVentaDto ticket) => Task.FromResult(false);
    public Task<bool> ProbarAsync(ImpresoraBluetoothDto impresora) => Task.FromResult(false);
    public Task<bool> ImprimirPagoAsync(ImpresoraBluetoothDto impresora, TicketPagoDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteCobroReporteAsync(ImpresoraBluetoothDto impresora, TicketCorteCobroDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteCobroResumenAsync(ImpresoraBluetoothDto impresora, TicketCorteCobroDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteVentaReporteAsync(ImpresoraBluetoothDto impresora, TicketCorteVentaDto ticket) => Task.FromResult(false);
    public Task<bool> ImprimirCorteVentaResumenAsync(ImpresoraBluetoothDto impresora, TicketCorteVentaDto ticket) => Task.FromResult(false);
#endif
}
