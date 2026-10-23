using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Shared.Services;

public class EstadoEmpresaGlobal
{
    // Propiedad global de solo lectura hacia afuera
    public EmpresaDto? Info { get; private set; }
    
    public IdentidadEmpresaDto? Identidad { get; private set; }

    /// <summary>
    /// Formato del comprobante de pago: true = ticket compacto, false = detallado (actual).
    /// Se resuelve del parámetro de sistema TICKET_PAGO_COMPACTO cuando hay internet y se
    /// conserva en memoria para que el flujo offline lo use al construir el ticket.
    /// </summary>
    public bool TicketPagoCompacto { get; private set; }

    public void ActualizarFormatoTicketPago(bool compacto)
    {
        TicketPagoCompacto = compacto;
        OnCambio?.Invoke();
    }

    public void ActualizarIdentidad(IdentidadEmpresaDto datos)
    {
        Identidad = datos;
        OnCambio?.Invoke();
    }

    // Bandera rápida para saber si ya cargó
    public bool Cargada => Info is not null;

    // Evento opcional por si una pantalla necesita enterarse cuando cambie de nombre
    public event Action? OnCambio;

    /// <summary>
    /// Setea o actualiza la información de la empresa en toda la aplicación.
    /// </summary>
    public void Actualizar(EmpresaDto datos)
    {
        Info = datos;
        Identidad = new(datos.NombreComercial, datos.NombreCorto, datos.LogoUrl, datos.LogoBlancoUrl);
        OnCambio?.Invoke();
    }

    /// <summary>
    /// Limpia el estado (por ejemplo, al cerrar sesión).
    /// </summary>
    public void Limpiar()
    {
        Info = null;
        Identidad = null;
        OnCambio?.Invoke();
    }
}