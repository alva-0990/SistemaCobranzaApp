namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Productos ----
public record CrearProductoRequest(
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini);

public record ActualizarProductoRequest(
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini);

public record ProductoDto(
    long IdProducto,
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini,
    bool Activo);

public record ProductosPaginaResponse(
    IReadOnlyList<ProductoDto> Items,
    int Total,
    int Pagina,
    int TamanoPagina);

// ---- Existencias ----
public record ExistenciaCentralDto(
    long IdProducto,
    string CodigoClave,
    string Descripcion,
    int Cantidad,
    int StockMini,
    DateTimeOffset? FechaActualizacion);

public record ExistenciaZonaDto(
    long IdZona,
    long IdProducto,
    string CodigoClave,
    string Descripcion,
    int Cantidad,
    DateTimeOffset? FechaActualizacion);

// ---- Movimientos de inventario (entrada/ajuste) ----

/// <summary>
/// Entrada o ajuste de inventario central.
/// - Entrada: suma Cantidad a la existencia.
/// - Ajuste: fija la existencia a Cantidad (valor absoluto).
/// </summary>
public record MovimientoCentralRequest(
    long IdProducto,
    int Cantidad,
    bool EsAjuste,
    string? Observaciones,
    long? IdUsuarioRegistro);

/// <summary>Entrada o ajuste de inventario de una zona.</summary>
public record MovimientoZonaRequest(
    long IdZona,
    long IdProducto,
    int Cantidad,
    bool EsAjuste,
    string? Observaciones,
    long? IdUsuarioRegistro);

public record MovimientoInventarioDto(
    long IdMovimientoInv,
    long IdProducto,
    string TipoMovimiento,
    int Cantidad,
    int ExistenciaAnterior,
    int ExistenciaNueva,
    long? IdZonaOrigen,
    long? IdZonaDestino,
    DateTimeOffset FechaMovimiento);
