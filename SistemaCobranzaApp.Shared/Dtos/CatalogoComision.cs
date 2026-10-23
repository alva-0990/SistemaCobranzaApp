namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Catálogo de Esquemas de Comisiones ----

/// <summary>
/// Parámetros requeridos para la creación de un nuevo esquema comercial de comisiones.
/// </summary>
public record CrearCatalogoComisionRequest(
    string NombreEsquema, 
    string TipoCalculo, 
    decimal ComisionContado, 
    decimal ComisionCredito
);

/// <summary>
/// Parámetros requeridos para la modificación completa de un esquema existente.
/// </summary>
public record ActualizarCatalogoComisionRequest(
    string NombreEsquema, 
    string TipoCalculo, 
    decimal ComisionContado, 
    decimal ComisionCredito, 
    bool Activo = true
);

/// <summary>
/// Objeto de transferencia de datos inmutable para exponer los esquemas comerciales en la UI o API.
/// </summary>
public record CatalogoComisionDto(
    long Id, 
    string NombreEsquema, 
    string TipoCalculo, 
    decimal ComisionContado, 
    decimal ComisionCredito, 
    bool Activo
);