
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
    ('CLIENTE_DIAS_MOROSO','Días sin pago para clasificar al cliente como MOROSO',61),
    ('SEGUIMIENTO_DIAS_GRACIA_ALERTA','Días sin pago para clasificar al cliente como ATRASADO',15),
    ('SEGUIMIENTO_DIAS_GRACIA_CRITICO','Días sin pago para clasificar al cliente como ATRASADO',31)
    ON CONFLICT (Clave) DO NOTHING;

-- Formato del comprobante de PAGO: 'SI' = ticket compacto; 'NO'/ausente = ticket detallado.
-- Es un valor de TEXTO, por eso se inserta explícitamente en ValorTexto.
INSERT INTO ParametrosSistema (Clave, Descripcion, ValorTexto)
VALUES
    ('TICKET_PAGO_COMPACTO', 'Formato del comprobante de pago: SI = compacto, NO = detallado', 'NO'),
    ('PROMO_PAGOS_CONSECUTIVOS_ACTIVA', 'Indica si la promoción de pagos consecutivos está encendida (SI/NO)', 'SI'),
    ('WHATSAPP_ENVIO_ACTIVO', 'Indica si el flujo de envio de notificaciones whatsapp está encendida (SI/NO)', 'SI'),
    ('WHATSAPP_NOTIFICAR_VENTA', 'Indica si se envia la notificacion en alta de ventas encendida (SI/NO)', 'SI'),
    ('WHATSAPP_NOTIFICAR_PAGO', 'Indica si se envia la notificacion en alta de pagos encendida (SI/NO)', 'SI')
ON CONFLICT (Clave) DO NOTHING;

-- =====================================================================
-- Notificaciones WhatsApp (interruptores y flujo de envío, editables en caliente)
-- =====================================================================
-- Interruptores 'SI' / 'NO' que se administran desde la pantalla de Parámetros.
-- Secretos de conexión (URL/instancia/apikey de Evolution y credenciales de Meta) NO van aquí:
-- viven en variables de entorno del contenedor por seguridad.
--
-- Proveedor (flujo de envío):
--   WHATSAPP_PROVEEDOR = 'EVOLUTION' -> API externa ya implementada (Evolution API): comprobante completo
--                                       armado por el sistema (MensajesWhatsApp).
--   WHATSAPP_PROVEEDOR = 'OFICIAL'   -> WhatsApp Cloud API de Meta, con MENSAJES CORTOS definidos en plantillas
--                                       APROBADAS en Meta (WHATSAPP_OFICIAL_PLANTILLA_*). El sistema envía el
--                                       nombre de la plantilla y las variables posicionales del cuerpo:
--                                       VENTA_CREDITO {{1}} cliente, {{2}} folio, {{3}} total, {{4}} plan
--                                       VENTA_CONTADO {{1}} cliente, {{2}} folio, {{3}} total
--                                       PAGO          {{1}} cliente, {{2}} folio, {{3}} monto, {{4}} saldo actual
INSERT INTO ParametrosSistema (Clave, Descripcion, ValorTexto)
VALUES
    ('WHATSAPP_ENVIO_ACTIVO',    'Interruptor maestro de notificaciones WhatsApp (SI/NO)', 'SI'),
    ('WHATSAPP_NOTIFICAR_VENTA', 'Enviar WhatsApp al registrar una venta (SI/NO)',        'SI'),
    ('WHATSAPP_NOTIFICAR_PAGO',  'Enviar WhatsApp al registrar un pago (SI/NO)',          'SI'),
    ('WHATSAPP_PROVEEDOR', 'Flujo de envío de WhatsApp: EVOLUTION (API externa) u OFICIAL (API oficial de Meta)', 'EVOLUTION'),
    ('WHATSAPP_OFICIAL_PLANTILLA_VENTA_CREDITO', 'Nombre de la plantilla aprobada en Meta para venta a crédito (flujo OFICIAL). Variables: 1 cliente, 2 folio, 3 total, 4 plan', 'venta_credito'),
    ('WHATSAPP_OFICIAL_PLANTILLA_VENTA_CONTADO', 'Nombre de la plantilla aprobada en Meta para venta de contado (flujo OFICIAL). Variables: 1 cliente, 2 folio, 3 total', 'venta_contado'),
    ('WHATSAPP_OFICIAL_PLANTILLA_PAGO', 'Nombre de la plantilla aprobada en Meta para pago realizado (flujo OFICIAL). Variables: 1 cliente, 2 folio, 3 monto, 4 saldo actual', 'pago_registrado'),
    ('WHATSAPP_OFICIAL_IDIOMA', 'Código de idioma de las plantillas aprobadas en Meta (flujo OFICIAL), ej. es_MX', 'es_MX')
ON CONFLICT (Clave) DO NOTHING;

-- Purga de la tabla de paso cola_mensajes_whatsapp (interruptor + retención).
-- WHATSAPP_PURGA_ACTIVA es texto 'SI'/'NO'; WHATSAPP_PURGA_DIAS es numérico (días
-- que se conservan los mensajes ya 'Enviado' antes de borrarlos).
INSERT INTO ParametrosSistema (Clave, Descripcion, ValorTexto)
VALUES
    ('WHATSAPP_PURGA_ACTIVA', 'Borrado automático de mensajes WhatsApp ya enviados (SI/NO)', 'SI')
ON CONFLICT (Clave) DO NOTHING;

INSERT INTO ParametrosSistema (Clave, Descripcion, ValorNumerico)
VALUES
    ('WHATSAPP_PURGA_DIAS', 'Días que se conservan los mensajes WhatsApp enviados antes de borrarlos', 7)
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