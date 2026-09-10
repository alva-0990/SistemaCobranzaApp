namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>Datos para crear un cliente.</summary>
public record CrearClienteRequest(
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    long IdRuta,
    long? IdVendedor,
    long? IdClasificacion,
    string? Observaciones,
    long? IdUsuarioRegistro);

/// <summary>Datos para actualizar un cliente existente.</summary>
public record ActualizarClienteRequest(
    string Nombre,
    string? ApPaterno,
    string? ApMaterno,
    long IdRuta,
    long? IdVendedor,
    long? IdClasificacion,
    string? Observaciones);

/// <summary>Página de resultados de clientes.</summary>
public record ClientesPaginaResponse(
    IReadOnlyList<ClienteDto> Items,
    int Total,
    int Pagina,
    int TamanoPagina);
