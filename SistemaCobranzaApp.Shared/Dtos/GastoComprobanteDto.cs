namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Comprobantes de Gastos ----

public record CrearGastoComprobanteRequest(
    string TipoComprobante,
    string? NombreArchivo,
    string? MimeType,
    byte[]? ArchivoBytes,
    string? ArchivoUrl,
    string? Observaciones
);

public record GastoComprobanteDto(
    long IdComprobante,
    long IdGasto,
    string TipoComprobante,
    string? NombreArchivo,
    string? MimeType,
    string? ArchivoUrl,
    string? Observaciones,
    long IdUsuarioRegistro,
    DateTimeOffset FechaRegistro
);

// ---- Movimientos de Cuentas de Gastos ----

public record CrearMovimientoCuentaGastoRequest(
    string TipoMovimiento,
    decimal Importe,
    string Concepto,
    string? Referencia = null,
    string? Observaciones = null
);

public record MovimientoCuentaGastoDto(
    long IdMovimientoCuenta,
    long IdCuentaGasto,
    long? IdPagoGasto,
    string TipoMovimiento,
    decimal Importe,
    DateTimeOffset FechaMovimiento,
    string Concepto,
    string? Referencia,
    string? Observaciones,
    long IdUsuarioRegistro
);

// ---- Pagos de Gastos ----

public record RegistrarGastoPagoRequest(
    long IdCuentaGasto,
    decimal Importe,
    string FormaPago,
    DateTimeOffset? FechaPago = null,
    string? Referencia = null,
    string? Observaciones = null
);

public record GastoPagoDto(
    long IdPagoGasto,
    long IdGasto,
    long IdCuentaGasto,
    string NombreCuenta,
    DateTimeOffset FechaPago,
    decimal Importe,
    string FormaPago,
    string? Referencia,
    string? Observaciones,
    long IdUsuarioRegistro,
    DateTimeOffset FechaAlta
);
// ---- Gastos ----

public record CrearGastoRequest(
    long IdCategoriaGasto,
    DateTimeOffset FechaGasto,
    string Concepto,
    decimal Importe,
    string? Descripcion,
    string? Proveedor,
    string? RFCProveedor,
    string? Referencia,
    long? IdZona,
    long? IdCobrador,
    long? IdVendedor,
    long? IdCuentaPrevista,
    string? Observaciones
);

public record ActualizarGastoRequest(
    long IdCategoriaGasto,
    DateTimeOffset FechaGasto,
    string Concepto,
    decimal Importe,
    string? Descripcion,
    string? Proveedor,
    string? RFCProveedor,
    string? Referencia,
    long? IdZona,
    long? IdCobrador,
    long? IdVendedor,
    long? IdCuentaPrevista,
    string? Observaciones
);

public record AutorizarGastoRequest(
    long IdUsuarioAutoriza
);

public record CancelarGastoRequest(
    long IdUsuarioCancelacion,
    string Motivo
);

public record GastoDto(
    long IdGasto,
    long IdCategoriaGasto,
    string NombreCategoria,
    string FolioGasto,
    DateTimeOffset FechaGasto,
    string Concepto,
    string? Descripcion,
    string? Proveedor,
    string? RFCProveedor,
    string? Referencia,
    decimal Importe,
    decimal TotalPagado,
    decimal SaldoPendiente,
    string Estatus,
    long? IdZona,
    string? NombreZona,
    long? IdCobrador,
    string? NombreCobrador,
    long? IdVendedor,
    string? NombreVendedor,
    long? IdCuentaPrevista,
    string? NombreCuentaPrevista,
    string? Observaciones,
    long IdUsuarioRegistro,
    long? IdUsuarioAutoriza,
    DateTimeOffset? FechaAutorizacion,
    long? IdUsuarioCancelacion,
    DateTimeOffset? FechaCancelacion,
    string? MotivoCancelacion,
    DateTimeOffset FechaAlta
);

// ---- Cuentas de Gasto ----

public record CrearCuentaGastoRequest(
    string Nombre,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    string? Observaciones
);

public record ActualizarCuentaGastoRequest(
    string Nombre,
    string TipoCuenta,
    string Moneda,
    string? Observaciones,
    bool Activo = true
);

public record CuentaGastoDto(
    long IdCuentaGasto,
    string Nombre,
    string TipoCuenta,
    string Moneda,
    decimal SaldoInicial,
    decimal SaldoActual,
    string? Observaciones,
    bool Activo
);


// ---- Catálogo Categorías de Gasto ----

public record CrearCatalogoCategoriaGastoRequest(
    string Nombre,
    string? Descripcion,
    bool RequiereComprobante
);

public record ActualizarCatalogoCategoriaGastoRequest(
    string Nombre,
    string? Descripcion,
    bool RequiereComprobante,
    bool Activo = true
);

public record CatalogoCategoriaGastoDto(
    long IdCategoriaGasto,
    string Nombre,
    string? Descripcion,
    bool RequiereComprobante,
    bool Activo
);

// ---- Cambios de estatus (activar / desactivar) ----

public record CambiarEstatusCuentaGastoRequest(bool Activo);

public record CambiarEstatusCategoriaGastoRequest(bool Activo);
