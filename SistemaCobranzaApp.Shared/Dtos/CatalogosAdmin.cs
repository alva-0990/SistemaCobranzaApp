namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Zona ----
public record CrearZonaRequest(string Descripcion);
public record ActualizarZonaRequest(string Descripcion);
public record ZonaDto(long IdZona, string Descripcion, bool Activo);

// ---- Ruta ----
public record CrearRutaRequest(string Descripcion, long? IdZona);
public record ActualizarRutaRequest(string Descripcion, long? IdZona);
public record RutaAdminDto(long IdRuta, string Descripcion, long? IdZona, string? Zona, bool Activo);

// ---- Cobrador ----
public record CrearCobradorRequest(string Nombre, long? IdZona);
public record ActualizarCobradorRequest(string Nombre, long? IdZona);
public record CobradorDto(long IdCobrador, string Nombre, long? IdZona, string? Zona, bool Activo);

// ---- Vendedor ----
public record CrearVendedorRequest(string Nombre, long? IdZona);
public record ActualizarVendedorRequest(string Nombre, long? IdZona);
public record VendedorDto(long IdVendedor, string Nombre, long? IdZona, string? Zona, bool Activo);
