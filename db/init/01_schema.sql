-- =====================================================================
-- Sistema de Cobranza - Esquema PostgreSQL (portado desde DB.sql SQLite)
-- =====================================================================
-- Cambios frente al esquema SQLite original:
--   * INTEGER PRIMARY KEY AUTOINCREMENT -> BIGINT GENERATED ALWAYS AS IDENTITY
--   * Dinero REAL -> NUMERIC(14,2)  (exacto, sin errores de punto flotante)
--   * TEXT de fechas -> TIMESTAMPTZ  (con zona horaria)
--   * COLLATE NOCASE -> CITEXT       (texto case-insensitive)
--   * Estatus general -> INTEGER:
--       0 = Inactivo
--       1 = Activo
--   * Se ELIMINAN las tablas y roles de Telegram (bot descartado)
--   * Se agregan columnas de sincronización offline:
--       - Pagos.PagoUuid  (idempotencia de sync)
--       - <tablas sincronizables>.FechaModificacion
-- =====================================================================

-- Zona horaria persistente para la base y su usuario de inicialización.
-- Los clientes pueden sobrescribirla explícitamente en su propia sesión.
DO $$
BEGIN
    EXECUTE format('ALTER DATABASE %I SET timezone TO %L',
                   current_database(), 'America/Mexico_City');
    EXECUTE format('ALTER ROLE %I IN DATABASE %I SET timezone TO %L',
                   current_user, current_database(), 'America/Mexico_City');
END
$$;

SET TIME ZONE 'America/Mexico_City';

CREATE
EXTENSION IF NOT EXISTS citext;
CREATE
EXTENSION IF NOT EXISTS "pgcrypto";  -- gen_random_uuid()


-- ------------------------- Catálogos ---------------------------------

CREATE TABLE CatalogoCondicion
(
    IdCondicion BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE CatalogoClasificacion
(
    IdClasificacion BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion     TEXT    NOT NULL UNIQUE,
    Estatus         INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE CatalogoMovimientos
(
    IdMovimiento BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion  TEXT    NOT NULL UNIQUE,
    Estatus      INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE Zona
(
    IdZona      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE Rol
(
    IdRol       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE CatalogoEstatusVenta
(
    IdEstatusVenta BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion    TEXT    NOT NULL UNIQUE,
    Estatus        INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE CatalogoMeses
(
    IdMes       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    NumMeses    INTEGER NOT NULL,
    Descripcion TEXT    NOT NULL,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

CREATE TABLE CatalogoFrecuenciaPago
(
    IdFrecuenciaPago BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion      TEXT    NOT NULL UNIQUE, -- Ej: 'SEMANAL', 'QUINCENAL', 'MENSUAL'
    DiasPeriodo      INT     NOT NULL,        -- Útil para calcular fechas automáticamente (ej: 7, 15, 30, 1)
    Estatus          INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);


-- ------------------------- Seguridad ---------------------------------

CREATE TABLE Usuarios
(
    IdUsuario      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdRol          BIGINT,
    Usuario        CITEXT      NOT NULL UNIQUE,
    PasswordHash   TEXT        NOT NULL,
    NombreCompleto TEXT        NOT NULL,
    Estatus        INTEGER     NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    FechaAlta      TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (IdRol) REFERENCES Rol (IdRol) ON DELETE SET NULL
);


-- ------------------------- Personas ----------------------------------

CREATE TABLE Cobradores
(
    IdCobrador BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Nombre     TEXT    NOT NULL,
    IdZona     BIGINT,
    IdUsuario  BIGINT,
    Estatus    INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    FOREIGN KEY (IdZona) REFERENCES Zona (IdZona) ON DELETE RESTRICT
);

CREATE TABLE Vendedores
(
    IdVendedor BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Nombre     TEXT    NOT NULL,
    IdZona     BIGINT,
    IdUsuario  BIGINT,
    Estatus    INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    FOREIGN KEY (IdZona) REFERENCES Zona (IdZona) ON DELETE RESTRICT
);
-- ------------------------- Productos ----------------------------------

-- 1. Categorías Globales (Electrodomésticos, Calzado, Ropa, etc.)
CREATE TABLE CatalogoCategorias
(
    IdCategoria BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

-- 2. Marcas (Mabe, Whirlpool, Nike, Levi's, etc.)
CREATE TABLE CatalogoMarcas
(
    IdMarca     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

-- 3. Colores (Negro, Blanco, Rojo, Azul, etc.)
CREATE TABLE CatalogoColores
(
    IdColor     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL UNIQUE,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1))
);

-- 4. Tallas (CH, M, G / 24, 25, 26 / S, M, L)
CREATE TABLE CatalogoTallas
(
    IdTalla     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL, -- Ej: '26', 'M', 'CH'
    Tipo        TEXT    NOT NULL, -- Para agrupar: 'CALZADO', 'ROPA', 'GENERAL'
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    CONSTRAINT UQ_Talla_Tipo UNIQUE (Descripcion, Tipo)
);

CREATE TABLE Productos
(
    IdProducto     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    CodigoClave    CITEXT         NOT NULL UNIQUE,
    Descripcion    TEXT           NOT NULL,
    Costo          NUMERIC(14, 2) NOT NULL DEFAULT 0,
    PrecioSugerido NUMERIC(14, 2) NOT NULL DEFAULT 0,
    PrecioMinimo   NUMERIC(14, 2) NOT NULL DEFAULT 0,
    StockMini      INTEGER        NOT NULL DEFAULT 0,

    -- Campos de control y clasificación nuevos
    IdCategoria    BIGINT         NOT NULL,
    IdMarca        BIGINT         NOT NULL,
    IdColor        BIGINT NULL, -- Nullable porque un refri o estufa puede no requerir variantes estrictas
    IdTalla        BIGINT NULL, -- Nullable porque los electrodomésticos no llevan talla

    Estatus        INTEGER        NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),

    -- Restricciones de integridad (Llaves Foráneas)
    CONSTRAINT FK_Productos_Categorias FOREIGN KEY (IdCategoria) REFERENCES CatalogoCategorias (IdCategoria),
    CONSTRAINT FK_Productos_Marcas FOREIGN KEY (IdMarca) REFERENCES CatalogoMarcas (IdMarca),
    CONSTRAINT FK_Productos_Colores FOREIGN KEY (IdColor) REFERENCES CatalogoColores (IdColor),
    CONSTRAINT FK_Productos_Tallas FOREIGN KEY (IdTalla) REFERENCES CatalogoTallas (IdTalla)
);


CREATE TABLE Ruta
(
    IdRuta      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Descripcion TEXT    NOT NULL,
    IdZona      BIGINT,
    Estatus     INTEGER NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    FOREIGN KEY (IdZona) REFERENCES Zona (IdZona) ON DELETE SET NULL
);

CREATE TABLE Clientes
(
    IdCliente           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Nombre              VARCHAR(150) NOT NULL,
    ApPaterno           VARCHAR(100),
    ApMaterno           VARCHAR(100),

    -- Nuevos campos de contacto y localización
    TelefonoPrincipal   VARCHAR(20)  NOT NULL, -- Obligatorio para cobranza/notificaciones
    TelefonoSecundario  VARCHAR(20),           -- Teléfono de respaldo o del trabajo
    Email               VARCHAR(100),
    Direccion           VARCHAR(250) NOT NULL, -- Calle, número exterior/interior, colonia
    CodigoPostal        VARCHAR(10),
    ReferenciaUbicacion VARCHAR(250),          -- Ej: "Frente a la tienda escolar", "Fachada azul"
    Latitud             NUMERIC(10, 8),        -- Para geolocalización del cobrador en mapa
    Longitud            NUMERIC(11, 8),        -- Para geolocalización del cobrador en mapa

    IdRuta              BIGINT       NOT NULL,
    IdVendedor          BIGINT,
    IdClasificacion     BIGINT,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaAlta           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    Estatus             SMALLINT     NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    Observaciones       VARCHAR(500),
    IdUsuarioRegistro   BIGINT       NOT NULL,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaModificacion   TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT fk_clientes_usuarios FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE RESTRICT,
    CONSTRAINT fk_clientes_ruta FOREIGN KEY (IdRuta) REFERENCES Ruta (IdRuta) ON DELETE RESTRICT,
    CONSTRAINT fk_clientes_vendedores FOREIGN KEY (IdVendedor) REFERENCES Vendedores (IdVendedor) ON DELETE RESTRICT,
    CONSTRAINT fk_clientes_clasificacion FOREIGN KEY (IdClasificacion) REFERENCES CatalogoClasificacion (IdClasificacion) ON DELETE RESTRICT
);



-- ------------------------- Ventas ------------------------------------

-- ===========================================================================
-- SCRIPT DEFINITIVO: TABLAS TRANSACCIONALES PRINCIPALES (PostgreSQL)
-- ===========================================================================
CREATE TABLE CatalogoComisiones (
    IdCatalogoComision BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    NombreEsquema TEXT NOT NULL UNIQUE,          -- Ej: 'PRODUCTO CHICO', 'LINEA BLANCA', 'SERVICIOS'
    TipoCalculo TEXT NOT NULL CHECK (TipoCalculo IN ('PORCENTAJE', 'FIJO')),
    ComisionContado NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    ComisionCredito NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    Activo BOOLEAN NOT NULL DEFAULT TRUE,
    FechaRegistro TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- 1. TABLA PRINCIPAL DE VENTAS
CREATE TABLE Venta
(
    IdVenta                    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdCliente                  BIGINT         NOT NULL,
    IdVendedor                 BIGINT,
    CodigoVenta                TEXT UNIQUE,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaCompra                TIMESTAMPTZ    NOT NULL DEFAULT now(),
    IdCondicion                BIGINT         NOT NULL,

    -- --- SECCIÓN FINANCIERA ---
    Subtotal                   NUMERIC(14, 2) NOT NULL,
    DescuentoGlobal            NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    TotalFinal                 NUMERIC(14, 2) NOT NULL,
    Enganche                   NUMERIC(14, 2) NOT NULL DEFAULT 0.00,

    -- Expresión guardada físicamente en BD (Total real a financiar)
    MontoFinanciado            NUMERIC(14, 2) GENERATED ALWAYS AS (TotalFinal - Enganche) STORED,

    TotalComisionVenta NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    -- --- SECCIÓN DE CRÉDITO ACTUALIZADA ---
    IdFrecuenciaPago           BIGINT,
    NumeroCuotas               INT                     DEFAULT 1,
    MontoCuotaFija             NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    TasaInteres                NUMERIC(5, 2)  NOT NULL DEFAULT 0.00,
    FechaPrimerVence           TIMESTAMPTZ, -- Se calcula dinámicamente desde C#

    -- --- CONTROL DE SALDOS EN TIEMPO REAL ---
    TotalAbonado               NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    SaldoPendiente             NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    FechaUltimoPago            TIMESTAMPTZ, -- Se actualiza desde C# con cada abono

    -- --- ADICIONES EXCLUSIVAS PARA TUS REQUERIMIENTOS ---
    PagosConsecutivosPuntuales INT            NOT NULL DEFAULT 0, -- Para contar la racha dinámica
    VentasUnificadas           TEXT,                              -- Para guardar el rastro de las cuentas sumadas

    -- --- ESTADOS Y AUDITORÍA ---
    IdEstatusVenta             BIGINT         NOT NULL DEFAULT 1,
    Observaciones              TEXT,
    IdUsuarioRegistro          BIGINT,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaModificacion          TIMESTAMPTZ    NOT NULL DEFAULT now(),
    ventauuid UUID,
    -- --- RESTRICCIONES DE LLAVES FORÁNEAS ---
    CONSTRAINT fk_usuario FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE RESTRICT,
    CONSTRAINT fk_cliente FOREIGN KEY (IdCliente) REFERENCES Clientes (IdCliente) ON DELETE RESTRICT,
    CONSTRAINT fk_vendedor FOREIGN KEY (IdVendedor) REFERENCES Vendedores (IdVendedor) ON DELETE RESTRICT,
    CONSTRAINT fk_condicion FOREIGN KEY (IdCondicion) REFERENCES CatalogoCondicion (IdCondicion) ON DELETE RESTRICT,
    CONSTRAINT fk_frecuencia FOREIGN KEY (IdFrecuenciaPago) REFERENCES CatalogoFrecuenciaPago (IdFrecuenciaPago) ON DELETE RESTRICT,
    CONSTRAINT fk_estatus FOREIGN KEY (IdEstatusVenta) REFERENCES CatalogoEstatusVenta (IdEstatusVenta) ON DELETE RESTRICT
);

-- 2. DETALLE DE ARTÍCULOS VENDIDOS
CREATE TABLE DetallesVenta
(
    IdDetalle     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdVenta       BIGINT         NOT NULL REFERENCES Venta (IdVenta) ON DELETE CASCADE,
    IdProducto    BIGINT         NOT NULL REFERENCES Productos (IdProducto) ON DELETE RESTRICT,
    Cantidad      INTEGER        NOT NULL CHECK (Cantidad > 0),
    PrecioCobrado NUMERIC(14, 2) NOT NULL,
    Importe       NUMERIC(14, 2) NOT NULL,
    PorcentajeComisionAplicado NUMERIC(5, 2) NOT NULL DEFAULT 0.00,
    MontoComisionGenerado NUMERIC(14, 2) NOT NULL DEFAULT 0.00
);

-- 3. CONFIGURACIÓN DE COMISIONES
CREATE TABLE ConfiguracionComisiones (
    IdConfiguracion BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdProducto BIGINT NOT NULL UNIQUE REFERENCES Productos (IdProducto) ON DELETE CASCADE,
    IdCatalogoComision BIGINT NOT NULL REFERENCES CatalogoComisiones (IdCatalogoComision) ON DELETE RESTRICT,
    FechaAsignacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    IdUsuarioRegistro BIGINT
);


-- 4. NUEVA: CONTROL E HISTORIAL DE PAGOS DE COMISIONES (RECOMENDADO)
CREATE TABLE ComisionesPorPagar (
    IdComision BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdVenta BIGINT NOT NULL REFERENCES Venta (IdVenta) ON DELETE CASCADE,
    IdDetalle BIGINT NOT NULL REFERENCES DetallesVenta (IdDetalle) ON DELETE CASCADE,
    IdVendedor BIGINT NOT NULL REFERENCES Vendedores (IdVendedor) ON DELETE RESTRICT,
    MontoComision NUMERIC(14, 2) NOT NULL,
    EstatusComision TEXT NOT NULL CHECK (EstatusComision IN ('PENDIENTE', 'DISPONIBLE', 'PAGADA', 'CANCELADA')) DEFAULT 'PENDIENTE',
    FechaCalculo TIMESTAMPTZ NOT NULL DEFAULT now(),
    FechaPago TIMESTAMPTZ,
    IdUsuarioPago BIGINT
);



-- 3. PLAN DE AMORTIZACIÓN (Cuotas Individuales / Tabla de Amortización)
CREATE TABLE VentaCuotas
(
    IdCuota          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdVenta          BIGINT         NOT NULL REFERENCES Venta (IdVenta) ON DELETE CASCADE,
    NumeroCuota      INT            NOT NULL,
    FechaVencimiento TIMESTAMPTZ    NOT NULL, -- Se calcula y guarda desde C# (ej: sumando 7 o 15 días según frecuencia)
    MontoCuota       NUMERIC(14, 2) NOT NULL,
    MontoInteres     NUMERIC(14, 2) NOT NULL DEFAULT 0.00,
    SaldoCuota       NUMERIC(14, 2) NOT NULL,
    EstatusCuota     TEXT           NOT NULL DEFAULT 'PENDIENTE' CHECK (EstatusCuota IN ('PENDIENTE', 'PAGADA', 'VENCIDA')),

    CONSTRAINT uq_venta_cuota UNIQUE (IdVenta, NumeroCuota)
);

-- 4. OPCIONES DE LIQUIDACIÓN ADICIONALES (Planes promocionales por meses)
CREATE TABLE VentaPlan
(
    IdPlan           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdVenta          BIGINT         NOT NULL REFERENCES Venta (IdVenta) ON DELETE CASCADE,
    NumeroMeses      INTEGER        NOT NULL CHECK (NumeroMeses > 0),
    MontoLiquidacion NUMERIC(14, 2) NOT NULL CHECK (MontoLiquidacion >= 0),
    Activo           BOOLEAN        NOT NULL DEFAULT TRUE,

    CONSTRAINT uq_venta_plan UNIQUE (IdVenta, NumeroMeses)
);

-- 5. HISTORIAL DE PAGOS APLICADOS
CREATE TABLE Pagos
(
    IdPago            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    PagoUuid          UUID           NOT NULL UNIQUE DEFAULT gen_random_uuid(),
    IdVenta           BIGINT         NOT NULL REFERENCES Venta (IdVenta) ON DELETE CASCADE,
    IdCliente         BIGINT         NOT NULL REFERENCES Clientes (IdCliente) ON DELETE RESTRICT,
    SaldoAnterior     NUMERIC(14, 2) NOT NULL        DEFAULT 0,
    SaldoActual       NUMERIC(14, 2) NOT NULL        DEFAULT 0,
    MontoAbona        NUMERIC(14, 2) NOT NULL CHECK (MontoAbona > 0),
    IdCobrador        BIGINT REFERENCES Cobradores (IdCobrador) ON DELETE RESTRICT,
    FechaPago         TIMESTAMPTZ NOT NULL DEFAULT now(),
    IdMovimiento      BIGINT REFERENCES CatalogoMovimientos (IdMovimiento) ON DELETE RESTRICT,
    Observaciones     TEXT,
    IdUsuarioRegistro BIGINT REFERENCES Usuarios (IdUsuario) ON DELETE RESTRICT,
    FolioPago         TEXT,
    ConsecutivoPago   INTEGER,
    FechaModificacion TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE PropuestaEnvio
(
    IdPropuesta       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdZonaDestino     BIGINT      NOT NULL,
    Estatus           TEXT        NOT NULL DEFAULT 'PENDIENTE',
    Observaciones     TEXT,
    IdUsuarioRegistro BIGINT,
    IdUsuarioConfirma BIGINT,
    FechaCreacion     TIMESTAMPTZ NOT NULL DEFAULT now(),
    FechaResolucion   TIMESTAMPTZ,
    FOREIGN KEY (IdZonaDestino) REFERENCES Zona (IdZona) ON DELETE RESTRICT,
    FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE SET NULL,
    FOREIGN KEY (IdUsuarioConfirma) REFERENCES Usuarios (IdUsuario) ON DELETE SET NULL
);
CREATE TABLE InventarioCentral
(
    IdInventarioCentral BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdProducto          BIGINT      NOT NULL UNIQUE,
    Cantidad            INTEGER     NOT NULL DEFAULT 0,
    FechaActualizacion  TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (IdProducto) REFERENCES Productos (IdProducto) ON DELETE CASCADE
);

CREATE TABLE InventarioZona
(
    IdInventarioZona   BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdZona             BIGINT      NOT NULL,
    IdProducto         BIGINT      NOT NULL,
    Cantidad           INTEGER     NOT NULL DEFAULT 0,
    FechaActualizacion TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (IdZona, IdProducto),
    FOREIGN KEY (IdZona) REFERENCES Zona (IdZona) ON DELETE CASCADE,
    FOREIGN KEY (IdProducto) REFERENCES Productos (IdProducto) ON DELETE CASCADE
);

CREATE TABLE InventarioMovimiento
(
    IdMovimientoInv    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdProducto         BIGINT      NOT NULL,
    TipoMovimiento     TEXT        NOT NULL,
    Cantidad           INTEGER     NOT NULL,
    ExistenciaAnterior INTEGER     NOT NULL DEFAULT 0,
    ExistenciaNueva    INTEGER     NOT NULL DEFAULT 0,
    IdZonaOrigen       BIGINT,
    IdZonaDestino      BIGINT,
    IdPropuesta        BIGINT,
    IdVenta            BIGINT,
    Observaciones      TEXT,
    IdUsuarioRegistro  BIGINT,
    FechaMovimiento    TIMESTAMPTZ NOT NULL DEFAULT now(),
    FOREIGN KEY (IdProducto) REFERENCES Productos (IdProducto) ON DELETE RESTRICT,
    FOREIGN KEY (IdZonaOrigen) REFERENCES Zona (IdZona) ON DELETE SET NULL,
    FOREIGN KEY (IdZonaDestino) REFERENCES Zona (IdZona) ON DELETE SET NULL,
    FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE SET NULL
);

CREATE TABLE PropuestaEnvioDetalle
(
    IdPropuestaDetalle BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdPropuesta        BIGINT  NOT NULL,
    IdProducto         BIGINT  NOT NULL,
    Cantidad           INTEGER NOT NULL,
    FOREIGN KEY (IdPropuesta) REFERENCES PropuestaEnvio (IdPropuesta) ON DELETE CASCADE,
    FOREIGN KEY (IdProducto) REFERENCES Productos (IdProducto) ON DELETE RESTRICT
);

-- =========================================================================
-- 1. TABLA NUEVA: PARÁMETROS CONFIGURABLES DEL SISTEMA
-- =========================================================================
CREATE TABLE ParametrosSistema
(
    Clave             TEXT PRIMARY KEY,
    Descripcion       TEXT        NOT NULL,
    ValorNumerico     NUMERIC(14, 4),
    ValorTexto        TEXT,
    -- CORRECTO: Mantiene consistencia de zona horaria local
    FechaModificacion TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE ClientesFotos
(
    IdFoto            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdCliente         BIGINT      NOT NULL,
    TipoFoto          VARCHAR(50) NOT NULL DEFAULT 'PERFIL', -- Ej: 'PERFIL', 'FACHADA', 'INE'

    -- Opción A: Guardar el archivo binario directamente en la BD
    FotoBytes         BYTEA,

    -- Opción B: Guardar sólo la ruta/URL si las almacenas en un servidor de archivos o la nube
    FotoUrl           VARCHAR(500),

    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaRegistro     TIMESTAMPTZ NOT NULL DEFAULT now(),
    IdUsuarioRegistro BIGINT      NOT NULL,

    -- Llaves foráneas para mantener la integridad total
    CONSTRAINT fk_fotos_cliente FOREIGN KEY (IdCliente) REFERENCES Clientes (IdCliente) ON DELETE CASCADE,
    CONSTRAINT fk_fotos_usuario FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE RESTRICT
);


-- Crear tabla empresas en PostgreSQL
CREATE TABLE empresas
(
    id                  INT GENERATED ALWAYS AS IDENTITY,
    razon_social        VARCHAR(250)   NOT NULL,
    nombre_comercial    VARCHAR(200)   NOT NULL,
    nombre_corto        VARCHAR(50) NULL,
    rfc_o_nit           VARCHAR(20)    NOT NULL,
    regimen_fiscal      VARCHAR(150) NULL,
    anio_fundacion      INT            NOT NULL,

    -- Identidad de Marca
    lema                VARCHAR(250) NULL,
    logo_url            TEXT NULL,
    logo_blanco_url     TEXT NULL,

    -- Información de Contacto
    telefono_principal  VARCHAR(20) NULL,
    telefono_secundario VARCHAR(20) NULL,
    correo_contacto     VARCHAR(150) NULL,
    correo_soporte      VARCHAR(150) NULL,
    sitio_web           VARCHAR(250) NULL,

    -- Redes Sociales
    facebook_url        VARCHAR(250) NULL,
    instagram_url       VARCHAR(250) NULL,
    linkedin_url        VARCHAR(250) NULL,

    -- Dirección Física
    calle               VARCHAR(150) NULL,
    numero_exterior     VARCHAR(20) NULL,
    numero_interior     VARCHAR(20) NULL,
    colonia             VARCHAR(100) NULL,
    codigo_postal       VARCHAR(15) NULL,
    ciudad              VARCHAR(100) NULL,
    estado_o_provincia  VARCHAR(100) NULL,
    pais                VARCHAR(100) NULL,

    -- Configuración de Operación
    moneda_por_defecto  VARCHAR(5) NULL DEFAULT 'MXN',
    simbolo_moneda      VARCHAR(5) NULL DEFAULT '$',
    porcentaje_impuesto DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    horario_atencion    VARCHAR(100) NULL,

    -- Restricciones
    CONSTRAINT pk_empresas PRIMARY KEY (id),
    CONSTRAINT uq_empresas_rfc UNIQUE (rfc_o_nit)
);

-- ---------------------------------------------------------------------
-- 1. CATÁLOGO DE CATEGORÍAS DE GASTOS
-- ---------------------------------------------------------------------

CREATE TABLE CatalogoCategoriasGasto
(
    IdCategoriaGasto    BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Nombre              VARCHAR(100) NOT NULL,
    Descripcion         VARCHAR(250),
    RequiereComprobante BOOLEAN      NOT NULL DEFAULT FALSE,
    Estatus             INTEGER      NOT NULL DEFAULT 1 CHECK (Estatus IN (0, 1)),
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaAlta           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    FechaModificacion   TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX UX_CategoriaGasto_Nombre
    ON CatalogoCategoriasGasto (LOWER(Nombre));


-- ---------------------------------------------------------------------
-- 2. CUENTAS Y CAJAS
-- ---------------------------------------------------------------------

CREATE TABLE CuentasGasto
(
    IdCuentaGasto     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    Nombre            VARCHAR(100)   NOT NULL,

    TipoCuenta        VARCHAR(20)    NOT NULL
        CHECK (TipoCuenta IN (
                              'EFECTIVO',
                              'CAJA_CHICA',
                              'BANCO',
                              'OTRO'
            )),

    Moneda            VARCHAR(5)     NOT NULL DEFAULT 'MXN',

    SaldoInicial      NUMERIC(14, 2) NOT NULL DEFAULT 0
        CHECK (SaldoInicial >= 0),

    Estatus           INTEGER        NOT NULL DEFAULT 1
        CHECK (Estatus IN (0, 1)),

    Observaciones     VARCHAR(250),
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaAlta         TIMESTAMPTZ    NOT NULL DEFAULT now(),
    FechaModificacion TIMESTAMPTZ    NOT NULL DEFAULT now(),

    CONSTRAINT UQ_CuentaGasto_Nombre UNIQUE (Nombre)
);


-- ---------------------------------------------------------------------
-- 3. REGISTRO PRINCIPAL DE GASTOS
-- ---------------------------------------------------------------------

CREATE TABLE Gastos
(
    IdGasto              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    IdCategoriaGasto     BIGINT         NOT NULL,
    FolioGasto           VARCHAR(30)    NOT NULL UNIQUE,

    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaGasto           TIMESTAMPTZ    NOT NULL DEFAULT now(),
    Concepto             VARCHAR(250)   NOT NULL,
    Descripcion          TEXT,

    Proveedor            VARCHAR(200),
    RFCProveedor         VARCHAR(20),
    Referencia           VARCHAR(100),

    Importe              NUMERIC(14, 2) NOT NULL
        CHECK (Importe > 0),

    Estatus              VARCHAR(20)    NOT NULL DEFAULT 'PENDIENTE'
        CHECK (Estatus IN (
                           'PENDIENTE',
                           'AUTORIZADO',
                           'PARCIAL',
                           'PAGADO',
                           'CANCELADO'
            )),

    -- Clasificación operativa opcional.
    IdZona               BIGINT,
    IdCobrador           BIGINT,
    IdVendedor           BIGINT,

    -- Cuenta prevista/sugerida de la que saldrá el dinero (informativa).
    IdCuentaPrevista     BIGINT,

    Observaciones        TEXT,

    -- Auditoría del registro.
    IdUsuarioRegistro    BIGINT         NOT NULL,
    IdUsuarioAutoriza    BIGINT,
    FechaAutorizacion    TIMESTAMPTZ, -- Se queda NULL, se asignará desde C#

    IdUsuarioCancelacion BIGINT,
    FechaCancelacion     TIMESTAMPTZ, -- Se queda NULL, se asignará desde C#
    MotivoCancelacion    VARCHAR(500),

    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaAlta            TIMESTAMPTZ    NOT NULL DEFAULT now(),
    FechaModificacion    TIMESTAMPTZ    NOT NULL DEFAULT now(),

    CONSTRAINT FK_Gasto_Categoria
        FOREIGN KEY (IdCategoriaGasto)
            REFERENCES CatalogoCategoriasGasto (IdCategoriaGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_Zona
        FOREIGN KEY (IdZona)
            REFERENCES Zona (IdZona)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_Cobrador
        FOREIGN KEY (IdCobrador)
            REFERENCES Cobradores (IdCobrador)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_Vendedor
        FOREIGN KEY (IdVendedor)
            REFERENCES Vendedores (IdVendedor)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_CuentaPrevista
        FOREIGN KEY (IdCuentaPrevista)
            REFERENCES CuentasGasto (IdCuentaGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_UsuarioRegistro
        FOREIGN KEY (IdUsuarioRegistro)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_UsuarioAutoriza
        FOREIGN KEY (IdUsuarioAutoriza)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT,

    CONSTRAINT FK_Gasto_UsuarioCancelacion
        FOREIGN KEY (IdUsuarioCancelacion)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT
);


-- ---------------------------------------------------------------------
-- 4. PAGOS DE GASTOS
-- Permite pagar un gasto en una o varias exhibiciones.
-- ---------------------------------------------------------------------

CREATE TABLE GastosPagos
(
    IdPagoGasto       BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    IdGasto           BIGINT         NOT NULL,
    IdCuentaGasto     BIGINT         NOT NULL,

    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaPago         TIMESTAMPTZ    NOT NULL DEFAULT now(),

    Importe           NUMERIC(14, 2) NOT NULL
        CHECK (Importe > 0),

    FormaPago         VARCHAR(20)    NOT NULL
        CHECK (FormaPago IN (
                             'EFECTIVO',
                             'TRANSFERENCIA',
                             'TARJETA',
                             'CHEQUE',
                             'OTRO'
            )),

    Referencia        VARCHAR(100),
    Observaciones     TEXT,

    IdUsuarioRegistro BIGINT         NOT NULL,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaAlta         TIMESTAMPTZ    NOT NULL DEFAULT now(),

    CONSTRAINT FK_GastoPago_Gasto
        FOREIGN KEY (IdGasto)
            REFERENCES Gastos (IdGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_GastoPago_Cuenta
        FOREIGN KEY (IdCuentaGasto)
            REFERENCES CuentasGasto (IdCuentaGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_GastoPago_Usuario
        FOREIGN KEY (IdUsuarioRegistro)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT
);

-- ---------------------------------------------------------------------
-- 5. MOVIMIENTOS DE CUENTAS
-- Entradas por depósitos/reposiciones y salidas por pagos de gastos.
-- ---------------------------------------------------------------------

CREATE TABLE MovimientosCuentaGasto
(
    IdMovimientoCuenta BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    IdCuentaGasto      BIGINT         NOT NULL,
    IdPagoGasto        BIGINT,

    TipoMovimiento     VARCHAR(10)    NOT NULL
        CHECK (TipoMovimiento IN ('ENTRADA', 'SALIDA')),

    Importe            NUMERIC(14, 2) NOT NULL
        CHECK (Importe > 0),

    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaMovimiento    TIMESTAMPTZ    NOT NULL DEFAULT now(),
    Concepto           VARCHAR(250)   NOT NULL,
    Referencia         VARCHAR(100),
    Observaciones      TEXT,

    IdUsuarioRegistro  BIGINT         NOT NULL,

    CONSTRAINT FK_MovCuentaGasto_Cuenta
        FOREIGN KEY (IdCuentaGasto)
            REFERENCES CuentasGasto (IdCuentaGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_MovCuentaGasto_Pago
        FOREIGN KEY (IdPagoGasto)
            REFERENCES GastosPagos (IdPagoGasto)
            ON DELETE RESTRICT,

    CONSTRAINT FK_MovCuentaGasto_Usuario
        FOREIGN KEY (IdUsuarioRegistro)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT,

    CONSTRAINT UQ_MovCuentaGasto_Pago
        UNIQUE (IdPagoGasto)
);


-- ---------------------------------------------------------------------
-- 6. COMPROBANTES DE GASTOS
-- ---------------------------------------------------------------------

CREATE TABLE GastosComprobantes
(
    IdComprobante     BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    IdGasto           BIGINT      NOT NULL,

    TipoComprobante   VARCHAR(30) NOT NULL DEFAULT 'TICKET'
        CHECK (TipoComprobante IN (
                                   'TICKET',
                                   'FACTURA',
                                   'RECIBO',
                                   'FOTOGRAFIA',
                                   'OTRO'
            )),

    NombreArchivo     VARCHAR(250),
    MimeType          VARCHAR(100),
    ArchivoBytes      BYTEA,
    ArchivoUrl        VARCHAR(1000),

    Observaciones     VARCHAR(250),

    IdUsuarioRegistro BIGINT      NOT NULL,
    -- TIMESTAMPTZ conserva el instante; la zona horaria se aplica al consultar o mostrar.
    FechaRegistro     TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FK_GastoComprobante_Gasto
        FOREIGN KEY (IdGasto)
            REFERENCES Gastos (IdGasto)
            ON DELETE CASCADE,

    CONSTRAINT FK_GastoComprobante_Usuario
        FOREIGN KEY (IdUsuarioRegistro)
            REFERENCES Usuarios (IdUsuario)
            ON DELETE RESTRICT,

    CONSTRAINT CK_GastoComprobante_Archivo
        CHECK (
            ArchivoBytes IS NOT NULL
                OR ArchivoUrl IS NOT NULL
            )
);

CREATE TABLE VentaUnificaciones (
    IdUnificacion      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    IdVentaNueva       BIGINT NOT NULL,
    IdVentaVieja       BIGINT NOT NULL,
    MontoTraspasado    NUMERIC(14, 2) NOT NULL,
    CodigoLiquidacion  TEXT NOT NULL, -- El código/folio con el que se mató la anterior
    FechaUnificacion   TIMESTAMPTZ NOT NULL DEFAULT now(),
    IdUsuarioRegistro  BIGINT NOT NULL,

    CONSTRAINT fk_unif_venta_nueva FOREIGN KEY (IdVentaNueva) REFERENCES Venta (IdVenta) ON DELETE RESTRICT,
    CONSTRAINT fk_unif_venta_vieja FOREIGN KEY (IdVentaVieja) REFERENCES Venta (IdVenta) ON DELETE RESTRICT,
    CONSTRAINT fk_unif_usuario FOREIGN KEY (IdUsuarioRegistro) REFERENCES Usuarios (IdUsuario) ON DELETE RESTRICT
);


