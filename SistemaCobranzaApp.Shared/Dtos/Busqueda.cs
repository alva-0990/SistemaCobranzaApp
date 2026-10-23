namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Filtros de búsqueda de cliente, todos por texto (LIKE, case-insensitive):
/// nombre, apellidos, teléfono, email, dirección, código postal, ruta y zona.
/// La ruta y la zona se buscan por su descripción (texto), no por Id.
/// </summary>
public record ClienteBusquedaFiltro(
    string? Nombre, 
    string? ApPaterno, 
    string? ApMaterno,
    string? Telefono, 
    string? Email,
    string? Direccion,
    string? CodigoPostal,
    string? Ruta,
    string? Zona
);

/// <summary>Resultado de clientes agrupados por una ruta.</summary>
public record ClientesPorRutaDto(
    long IdRuta,
    string? Ruta,
    long? IdZona,
    string? Zona,
    IReadOnlyList<ClienteDto> Clientes);

/// <summary>Resultado de clientes agrupados por una zona (con sus rutas).</summary>
public record ClientesPorZonaDto(
    long IdZona,
    string? Zona,
    IReadOnlyList<ClientesPorRutaDto> Rutas);

/// <summary>Representación global y unificada de un cliente para lectura, listados y búsquedas.</summary>
public record ClienteDto(
    long IdCliente,
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    string NombreCompleto,
    string TelefonoPrincipal,     
    string? TelefonoSecundario,   
    string? Email,                
    string Direccion,             
    string? CodigoPostal,         
    string? ReferenciaUbicacion,  
    decimal? Latitud,             
    decimal? Longitud,            
    long IdRuta,
    string? Ruta,
    long? IdZona,
    long? IdVendedor,
    string? VendedorNombre,       // Añadido: Para legibilidad en la administración
    long? IdClasificacion,
    string? ClasificacionNombre,  // Añadido: Para legibilidad en la administración
    int Estatus,                  
    string? Observaciones,
    DateTimeOffset FechaAlta      
);

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
    DateTimeOffset? FechaUltimoPago,
    string? TelefonoPrincipal,     
    string? Direccion,             
    decimal? Latitud,              
    decimal? Longitud              
);

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
    long IdMovimiento,
    string? Movimiento,
    string? Observaciones,
    DateTimeOffset FechaPago,
    decimal? LatitudPago,          
    decimal? LongitudPago          
);
