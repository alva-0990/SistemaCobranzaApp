namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Datos requeridos para registrar un nuevo cliente en el sistema.</summary>
public record CrearClienteRequest(
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    string TelefonoPrincipal,     
    string? TelefonoSecundario,
    string? Email,
    string Direccion,             
    string? CodigoPostal,
    string? ReferenciaUbicacion,  
    decimal? Latitud,             
    decimal? Longitud,            
    long IdRuta,
    long? IdVendedor,
    long? IdClasificacion,
    string? Observaciones,
    long IdUsuarioRegistro        
);

/// <summary>Datos permitidos para actualizar la información de un cliente existente.</summary>
public record ActualizarClienteRequest(
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    string TelefonoPrincipal,
    string? TelefonoSecundario,
    string? Email,
    string Direccion,
    string? CodigoPostal,
    string? ReferenciaUbicacion,
    decimal? Latitud,             
    decimal? Longitud,
    long IdRuta,
    long? IdVendedor,
    long? IdClasificacion,
    int Estatus,                  
    string? Observaciones
);

/// <summary>Estructura de datos paginada para listados eficientes de clientes.</summary>
public record ClientesPaginaResponse(
    IReadOnlyList<ClienteDto> Items, // Utiliza de forma transparente el ClienteDto unificado
    int TotalItems,               
    int PaginaActual,             
    int TamanoPagina)
{
    public int TotalPaginas => TamanoPagina > 0 ? (int)Math.Ceiling((double)TotalItems / TamanoPagina) : 0;
    public bool TienePaginaAnterior => PaginaActual > 1;
    public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;
}
