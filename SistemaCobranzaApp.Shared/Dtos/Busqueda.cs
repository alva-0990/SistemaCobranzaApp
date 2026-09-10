namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Filtros de búsqueda de cliente por nombre/apellidos (LIKE, case-insensitive).
/// Replica EjecutarBusquedaCliente de la app WPF.
/// </summary>
public record ClienteBusquedaFiltro(string? Nombre, string? ApPaterno, string? ApMaterno);

public record ClienteDto(
    long IdCliente,
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    string NombreCompleto,
    long IdRuta,
    string? Ruta,
    long? IdZona,
    long? IdVendedor,
    long? IdClasificacion,
    string? Observaciones);

/// <summary>Detalle de una venta con su saldo y datos del cliente/ruta/zona.</summary>
public record VentaDetalleDto(
    long IdVenta,
    long IdCliente,
    string Cliente,
    string? Ruta,
    long? IdZona,
    string? CodigoVenta,
    decimal TotalFinal,
    decimal Enganche,
    decimal TotalAbonos,
    decimal SaldoPendiente,
    decimal? PagoRequerido,
    long IdEstatusVenta,
    string? Estatus,
    DateTimeOffset FechaCompra,
    DateTimeOffset? FechaUltimoPago);

/// <summary>Renglón del historial de pagos de una venta.</summary>
public record PagoHistoricoDto(
    long IdPago,
    string? FolioPago,
    int? ConsecutivoPago,
    decimal MontoAbona,
    decimal SaldoAnterior,
    decimal SaldoActual,
    long? IdCobrador,
    string? Cobrador,
    string? Movimiento,
    string? Observaciones,
    DateTimeOffset FechaPago);
