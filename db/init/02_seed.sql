
-- =====================================================================
-- Sistema de Cobranza - Datos semilla
-- PostgreSQL
-- =====================================================================
--
-- ESTATUS GENERAL:
--   0 = Inactivo
--   1 = Activo
--
-- Los IDs de estos catálogos forman parte de las reglas actuales
-- del sistema y se conservan explícitamente.
--
-- Por eso se utiliza:
--   OVERRIDING SYSTEM VALUE
--
-- El script es idempotente:
--   puede ejecutarse nuevamente sin duplicar registros.
--
-- IMPORTANTE:
-- Los IDs físicos de estos catálogos se mantienen porque actualmente
-- el código de negocio los utiliza como identificadores conocidos.
-- =====================================================================

INSERT INTO CatalogoMeses
    (IdMes, NumMeses, Descripcion, Estatus)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 1, ' MES',   1),
    (2, 2, ' MESES', 1),
    (3, 3, ' MESES', 1),
    (4, 4, ' MESES', 1),
    (5, 5, ' MESES', 1),
    (6, 6, ' MESES', 1)
ON CONFLICT (IdMes) DO UPDATE
SET
    NumMeses    = EXCLUDED.NumMeses,
    Descripcion = EXCLUDED.Descripcion,
    Estatus     = EXCLUDED.Estatus;


-- ---------------------------------------------------------------------
-- Catálogo de estatus de venta
-- ---------------------------------------------------------------------

INSERT INTO CatalogoEstatusVenta
    (IdEstatusVenta, Descripcion, Estatus)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 'PENDIENTE', 1),
    (2, 'ABONANDO',  1),
    (3, 'PAGADA',    1),
    (4, 'CANCELADA', 1)
ON CONFLICT (IdEstatusVenta) DO UPDATE
SET
    Descripcion = EXCLUDED.Descripcion,
    Estatus     = EXCLUDED.Estatus;


-- ---------------------------------------------------------------------
-- Roles
-- ---------------------------------------------------------------------

INSERT INTO Rol
    (IdRol, Descripcion, Estatus)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 'Administrador', 1),
    (2, 'Supervisor',    1),
    (3, 'Vendedor',      1),
    (4, 'Cobrador',      1),
    (5, 'Consulta',      1)
ON CONFLICT (IdRol) DO UPDATE
SET
    Descripcion = EXCLUDED.Descripcion,
    Estatus     = EXCLUDED.Estatus;


-- ---------------------------------------------------------------------
-- Catálogo de movimientos
-- ---------------------------------------------------------------------

INSERT INTO CatalogoMovimientos
(IdMovimiento, Descripcion, Estatus)
    OVERRIDING SYSTEM VALUE
VALUES
    (1, 'ABONO',                     1),
    (2, 'ENGANCHE',                  1),
    (3, 'PAGO COMPLETO',             1),
    (4, 'LIQUIDACION PRONTO PAGO',   1), -- Nombre actualizado para Pronto Pago
    (5, 'AJUSTE',                    1),
    (6, 'BONIFICACION PROMOCION',    1), -- Nombre oficial para Racha de Pagos
    (7, 'LIQUIDADA POR RENOVACION',  1)  -- Nombre oficial para Unificación/Renovación
    ON CONFLICT (IdMovimiento) DO UPDATE
                                      SET
                                          Descripcion = EXCLUDED.Descripcion,
                                      Estatus     = EXCLUDED.Estatus;



-- ---------------------------------------------------------------------
-- Catálogo de condiciones de venta
-- ---------------------------------------------------------------------

INSERT INTO CatalogoCondicion
    (IdCondicion, Descripcion, Estatus)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 'CRÉDITO', 1),
    (2, 'CONTADO', 1)
ON CONFLICT (IdCondicion) DO UPDATE
SET
    Descripcion = EXCLUDED.Descripcion,
    Estatus     = EXCLUDED.Estatus;


-- ---------------------------------------------------------------------
-- Catálogo de clasificación de clientes
-- ---------------------------------------------------------------------

INSERT INTO CatalogoClasificacion
    (IdClasificacion, Descripcion, Estatus)
OVERRIDING SYSTEM VALUE
VALUES
    (1, 'NUEVO',   1),
    (2, 'BUENO',   1),
    (3, 'REGULAR', 1),
    (4, 'MALO',    1),
    (5, 'MOROSO',  1)
ON CONFLICT (IdClasificacion) DO UPDATE
SET
    Descripcion = EXCLUDED.Descripcion,
    Estatus     = EXCLUDED.Estatus;

INSERT INTO CatalogoFrecuenciaPago
(IdFrecuenciaPago, Descripcion, DiasPeriodo, Estatus)
    OVERRIDING SYSTEM VALUE
VALUES
    (1, 'DIARIO',     1,  1),
    (2, 'SEMANAL',    7,  1),
    (3, 'CATORCENAL', 14, 1),
    (4, 'QUINCENAL',  15, 1),
    (5, 'MENSUAL',    30, 1)
    ON CONFLICT (IdFrecuenciaPago) DO UPDATE
    SET
        Descripcion = EXCLUDED.Descripcion,
        DiasPeriodo = EXCLUDED.DiasPeriodo,
        Estatus     = EXCLUDED.Estatus;

-- Parámetro dinámico en su propia tabla

INSERT INTO ParametrosSistema
(Clave, Descripcion, ValorNumerico)
VALUES
    ('PROMO_PAGOS_CONSECUTIVOS_META', 'Pagos consecutivos requeridos para bonificación', 5.00),
    ('CLIENTE_DIAS_REGULAR','Días sin pago para clasificar al cliente como REGULAR',15),
    ('CLIENTE_DIAS_MALO','Días sin pago para clasificar al cliente como MALO', 31),
    ('CLIENTE_DIAS_MOROSO','Días sin pago para clasificar al cliente como MOROSO',61)
    ON CONFLICT (Clave) DO NOTHING;

-- Formato del comprobante de PAGO: 'SI' = ticket compacto; 'NO'/ausente = ticket detallado.
-- Es un valor de TEXTO, por eso se inserta explícitamente en ValorTexto.
INSERT INTO ParametrosSistema (Clave, Descripcion, ValorTexto)
VALUES
    ('TICKET_PAGO_COMPACTO', 'Formato del comprobante de pago: SI = compacto, NO = detallado', 'NO')
    ('PROMO_PAGOS_CONSECUTIVOS_ACTIVA', 'Indica si la promoción de pagos consecutivos está encendida (SI/NO)', 'SI')
ON CONFLICT (Clave) DO NOTHING;
-- =====================================================================
-- REALINEACIÓN DE SECUENCIAS
-- =====================================================================
--
-- Como los IDs anteriores se insertaron explícitamente, dejamos cada
-- secuencia preparada para que el siguiente registro automático utilice
-- un ID posterior al máximo existente.
--
-- El segundo parámetro es TRUE para que el siguiente nextval() genere
-- MAX + 1.
-- =====================================================================

SELECT setval(
    pg_get_serial_sequence('catalogomeses', 'idmes'),
    COALESCE((SELECT MAX(IdMes) FROM CatalogoMeses), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('catalogoestatusventa', 'idestatusventa'),
    COALESCE((SELECT MAX(IdEstatusVenta) FROM CatalogoEstatusVenta), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('rol', 'idrol'),
    COALESCE((SELECT MAX(IdRol) FROM Rol), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('catalogomovimientos', 'idmovimiento'),
    COALESCE((SELECT MAX(IdMovimiento) FROM CatalogoMovimientos), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('catalogocondicion', 'idcondicion'),
    COALESCE((SELECT MAX(IdCondicion) FROM CatalogoCondicion), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('catalogoclasificacion', 'idclasificacion'),
    COALESCE((SELECT MAX(IdClasificacion) FROM CatalogoClasificacion), 1),
    true
);

SELECT setval(
    pg_get_serial_sequence('catalogofrecuenciapago', 'idfrecuenciapago'),
    COALESCE((SELECT MAX(IdFrecuenciaPago) FROM CatalogoFrecuenciaPago), 1),
    true
);