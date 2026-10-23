namespace SistemaCobranzaApp.Shared.Dtos;


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
    DateTimeOffset? FechaActualizacion,
    string? Marca = null,
    string? Categoria = null,
    string? Color = null,
    string? Talla = null);

public record ExistenciaZonaDto(
    long IdZona,
    long IdProducto,
    string CodigoClave,
    string Descripcion,
    int Cantidad,
    DateTimeOffset? FechaActualizacion,
    string? Marca = null,
    string? Categoria = null,
    string? Color = null,
    string? Talla = null);

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

// ---- Historial global de inventario (vista con filtros) ----

/// <summary>
/// Renglón del historial global de movimientos de inventario, enriquecido con
/// clave y descripción del producto, nombre de la zona afectada y usuario que
/// registró el movimiento. Pensado para la vista "Historial de inventario".
/// </summary>
public record MovimientoHistorialDto(
    long IdMovimientoInv,
    DateTimeOffset FechaMovimiento,
    string TipoMovimiento,
    long IdProducto,
    string CodigoClave,
    string Descripcion,
    int Cantidad,
    int ExistenciaAnterior,
    int ExistenciaNueva,
    long? IdZona,
    string? Zona,
    string? Usuario,
    string? Observaciones,
    string? Marca = null,
    string? Categoria = null);
