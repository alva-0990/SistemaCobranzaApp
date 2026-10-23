namespace SistemaCobranzaApp.Shared.Dtos;

// =====================================================================
// 1. RECORDS / DTOS PARA LOS CATÁLOGOS DE CONTROL
// =====================================================================

// ---- Catálogo Categorías ----
public record CrearCategoriaRequest(string Descripcion);
public record ActualizarCategoriaRequest(string Descripcion, bool Activo = true);
public record CategoriaDto(long Id, string Descripcion, bool Activo);

// ---- Catálogo Marcas ----
public record CrearMarcaRequest(string Descripcion);
public record ActualizarMarcaRequest(string Descripcion, bool Activo = true);
public record MarcaDto(long Id, string Descripcion, bool Activo);

// ---- Catálogo Colores ----
public record CrearColorRequest(string Descripcion);
public record ActualizarColorRequest(string Descripcion, bool Activo = true);
public record ColorDto(long Id, string Descripcion, bool Activo);

// ---- Catálogo Tallas ----
public record CrearTallaRequest(string Descripcion, string Tipo); // Tipo: 'ROPA', 'CALZADO', 'GENERAL'
public record ActualizarTallaRequest(string Descripcion, string Tipo, bool Activo = true);
public record TallaDto(long Id, string Descripcion, string Tipo, bool Activo);


// =====================================================================
// 2. RECORDS / DTOS PARA LA TABLA PRINCIPAL DE PRODUCTOS
// =====================================================================

// ---- Catálogo Productos Ampliado ----
public record CrearProductoRequest(
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini,
    long IdCategoria,
    long IdMarca,
    long? IdColor,
    long? IdTalla,
    long IdCatalogoComision
);

public record ActualizarProductoRequest(
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini,
    long IdCategoria,
    long IdMarca,
    long? IdColor,
    long? IdTalla,
    long IdCatalogoComision,
    bool Activo = true
);

public record ProductoDto(
    long Id,
    string CodigoClave,
    string Descripcion,
    decimal Costo,
    decimal PrecioSugerido,
    decimal PrecioMinimo,
    int StockMini,
    long IdCategoria,
    string CategoriaDescripcion,
    long IdMarca,
    string MarcaDescripcion,
    long? IdColor,
    string? ColorDescripcion,
    long? IdTalla,
    string? TallaDescripcion,
    long IdCatalogoComision,
    string ComisionEsquemaNombre,
    string ComisionTipoCalculo, // 'PORCENTAJE' o 'FIJO'
    decimal ComisionContado,
    decimal ComisionCredito,
    bool Activo
);
