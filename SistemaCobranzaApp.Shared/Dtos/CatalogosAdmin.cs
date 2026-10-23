namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Zona ----
public record CrearZonaRequest(string Descripcion);
public record ActualizarZonaRequest(string Descripcion, bool Activo = true);
public record ZonaDto(long IdZona, string Descripcion, bool Activo);

// ---- Ruta ----
public record CrearRutaRequest(string Descripcion, long? IdZona);
public record ActualizarRutaRequest(string Descripcion, long? IdZona, bool Activo = true);
public record RutaAdminDto(long IdRuta, string Descripcion, long? IdZona, string? Zona, bool Activo);

// ---- Cobrador ----
public record CrearCobradorRequest(string Nombre, long? IdZona, long? IdUsuario);
public record ActualizarCobradorRequest(string Nombre, long? IdZona,long? IdUsuario, bool Activo = true);
public record CobradorDto(long IdCobrador, string Nombre, long? IdZona, string? Zona, long? IdUsuario, string? Usuario, bool Activo);

// ---- Vendedor ----
public record CrearVendedorRequest(string Nombre, long? IdZona, long? IdUsuario);
public record ActualizarVendedorRequest(string Nombre, long? IdZona, long? IdUsuario, bool Activo = true);
public record VendedorDto(long IdVendedor, string Nombre, long? IdZona, string? Zona, long? IdUsuario, string? Usuario, bool Activo);

// ---- Clasificaion ahora Cliente ----
public record CrearClasificaionClienteRequest(string Descripcion);
public record ActualizarClasificaionClienteRequest(string Descripcion, bool Activo = true);
public record ClasificaionClienteDto(long IdClasificaionCliente, string Descripcion, bool Activo);

// ---- Condicion Credito ahora Cliente ----
public record CrearCondicionCreditoRequest(string Descripcion);
public record ActualizarCondicionCreditoRequest(string Descripcion, bool Activo = true);
public record CondicionCreditoDto(long IdCondicion,string Descripcion, bool Activo);

// ---- Condicion Credito ahora Cliente ----
public record CrearEstatusVentaRequest(string Descripcion);
public record ActualizarEstatusVentaRequest(string Descripcion, bool Activo = true);
public record EstatusVentaDto(long Id, string Descripcion, bool Activo);

// ---- Catálogo Meses ----
public record CrearMesRequest(int NumMeses, string Descripcion);
public record ActualizarMesRequest(int NumMeses, string Descripcion, bool Activo = true);
public record MesDto(long Id, int NumMeses, string Descripcion, bool Activo);

// ---- Catálogo Movimientos ----
public record CrearMovimientoRequest(string Descripcion);
public record ActualizarMovimientoRequest(string Descripcion, bool Activo = true);
public record MovimientoDto(long Id, string Descripcion, bool Activo);

// ---- Catalogo Frecuencia Pago ----
public record CrearCatalogoFrecuenciaPagoRequest(string Descripcion, int DiasPeriodo);
public record ActualizarCatalogoFrecuenciaPagoRequest(string Descripcion, int DiasPeriodo, bool Activo = true);
public record CatalogoFrecuenciaPagoDto(long Id, string Descripcion, int DiasPeriodo, bool Activo );
