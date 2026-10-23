-- =====================================================================
-- Índices - PostgreSQL (portados desde DB.sql, sin índices de Telegram)
-- =====================================================================

CREATE INDEX IX_Clientes_IdVendedor          ON Clientes(IdVendedor);
CREATE INDEX IX_Venta_IdCliente              ON Venta(IdCliente);
CREATE INDEX IX_Venta_FechaCompra            ON Venta(FechaCompra);
CREATE INDEX IX_Pagos_IdVenta                ON Pagos(IdVenta);
CREATE INDEX IX_Pagos_FechaPago              ON Pagos(FechaPago);
CREATE INDEX IX_Clientes_IdRuta              ON Clientes(IdRuta);
CREATE INDEX IX_Pagos_IdCliente              ON Pagos(IdCliente);
CREATE INDEX IX_Venta_IdVendedor             ON Venta(IdVendedor);
CREATE INDEX IX_DetallesVenta_IdVenta        ON DetallesVenta(IdVenta);
CREATE INDEX IX_DetallesVenta_IdProducto     ON DetallesVenta(IdProducto);
CREATE INDEX IX_Cobradores_IdZona            ON Cobradores(IdZona);
CREATE INDEX IX_Vendedores_IdZona            ON Vendedores(IdZona);
CREATE INDEX IX_Pagos_IdCobrador             ON Pagos(IdCobrador);
CREATE INDEX IX_Pagos_IdMovimiento           ON Pagos(IdMovimiento);
CREATE INDEX IX_Clientes_IdClasificacion     ON Clientes(IdClasificacion);
CREATE INDEX IX_Clientes_Nombre              ON Clientes(Nombre);
CREATE INDEX IX_Clientes_ApPaterno           ON Clientes(ApPaterno);
CREATE INDEX IX_Clientes_ApMaterno           ON Clientes(ApMaterno);
CREATE INDEX IX_Clientes_Estatus             ON Clientes(Estatus);
CREATE INDEX IX_Ruta_IdZona                  ON Ruta(IdZona);
CREATE INDEX IX_Clientes_IdRuta_IdCliente    ON Clientes(IdRuta, IdCliente DESC);
CREATE INDEX IX_Ruta_IdZona_Estatus          ON Ruta(IdZona, Estatus);
CREATE INDEX IX_Vendedores_IdZona_Estatus    ON Vendedores(IdZona, Estatus);
CREATE INDEX IX_Venta_IdCliente_IdVenta      ON Venta(IdCliente, IdVenta DESC);
CREATE INDEX IX_Cobradores_IdZona_Est_Nombre ON Cobradores(IdZona, Estatus, Nombre);
CREATE INDEX IX_Pagos_IdVenta_IdPago         ON Pagos(IdVenta, IdPago DESC);
CREATE INDEX IX_DetallesVenta_IdVenta_IdDet  ON DetallesVenta(IdVenta, IdDetalle);
CREATE INDEX IX_Clientes_Estatus_Nombre      ON Clientes(Estatus, Nombre);

-- Inventario
CREATE INDEX IX_PropEnvioDetalle_IdPropuesta ON PropuestaEnvioDetalle(IdPropuesta);
CREATE INDEX IX_PropEnvioDetalle_IdProducto  ON PropuestaEnvioDetalle(IdProducto);
CREATE INDEX IX_PropuestaEnvio_Estatus       ON PropuestaEnvio(Estatus);
CREATE INDEX IX_PropuestaEnvio_IdZonaDestino ON PropuestaEnvio(IdZonaDestino);
CREATE INDEX IX_InvMovimiento_IdProducto     ON InventarioMovimiento(IdProducto);
CREATE INDEX IX_InvMovimiento_Fecha          ON InventarioMovimiento(FechaMovimiento DESC);
CREATE INDEX IX_InvMovimiento_Tipo           ON InventarioMovimiento(TipoMovimiento);
CREATE INDEX IX_InventarioZona_IdZona        ON InventarioZona(IdZona);
CREATE INDEX IX_InventarioZona_IdProducto    ON InventarioZona(IdProducto);
CREATE INDEX IX_InventarioCentral_IdProducto ON InventarioCentral(IdProducto);
-- Índice para acelerar la búsqueda de fotos cuando cargues el perfil del cliente
CREATE INDEX idx_clientesfotos_cliente ON ClientesFotos(IdCliente);

-- ---------------------------------------------------------------------
-- 7. ÍNDICES PARA BÚSQUEDAS Y REPORTES
-- ---------------------------------------------------------------------

CREATE INDEX IX_Gastos_FechaON Gastos (FechaGasto DESC);
CREATE INDEX IX_Gastos_EstatusON Gastos (Estatus);
CREATE INDEX IX_Gastos_Categoria_FechaON Gastos (IdCategoriaGasto, FechaGasto DESC);
CREATE INDEX IX_Gastos_Zona_FechaON Gastos (IdZona, FechaGasto DESC);
CREATE INDEX IX_Gastos_Cobrador_FechaON Gastos (IdCobrador, FechaGasto DESC);
CREATE INDEX IX_Gastos_Vendedor_FechaON Gastos (IdVendedor, FechaGasto DESC);
CREATE INDEX IX_GastosPagos_Gasto_FechaON GastosPagos (IdGasto, FechaPago DESC);
CREATE INDEX IX_MovimientosCuentaGasto_Cuenta_FechaON MovimientosCuentaGasto (IdCuentaGasto, FechaMovimiento DESC);
CREATE INDEX IX_GastosComprobantes_GastoON GastosComprobantes (IdGasto);
CREATE UNIQUE INDEX IF NOT EXISTS UX_CategoriaGasto_Nombre ON CatalogoCategoriasGasto (LOWER(Nombre));
CREATE INDEX idx_unificaciones_venta_nueva ON VentaUnificaciones(IdVentaNueva);
CREATE INDEX idx_unificaciones_venta_vieja ON VentaUnificaciones(IdVentaVieja);
CREATE INDEX IF NOT EXISTS ix_vendedores_usuario_jornada ON vendedores(idusuario);
CREATE INDEX IF NOT EXISTS ix_cobradores_usuario_jornada ON cobradores(idusuario);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ventas_ventauuid ON venta(ventauuid);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ventaunificaciones_origen ON VentaUnificaciones(IdVentaVieja);
