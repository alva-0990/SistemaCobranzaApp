# Resumen del funcionamiento del sistema (Web y Android)

Sistema de **ventas a crédito y cobranza** (CobrAgil). Registra clientes, ventas de contado y a crédito, pagos/abonos,
inventario por zona, gastos y comisiones, y manda comprobantes por WhatsApp. La **misma interfaz** corre en navegador
(Web) y en la app móvil (Android); cambia lo que el dispositivo puede hacer (offline, impresora, cámara, archivos).

> Elaborado a partir de la revisión del código y los documentos del repo (`docs/`, `deploy/`, `db/init`).
> Los detalles de permisos por rol y fórmulas exactas de cálculo no se revisaron a fondo: confírmalos en el código antes de
> darlos por definitivos.

---

## 1. Arquitectura

| Proyecto | Qué es |
|---|---|
| `SistemaCobranzaApp.Domain` | Entidades y constantes del negocio. |
| `SistemaCobranzaApp.Application` | Contratos/capa de aplicación. |
| `SistemaCobranzaApp.Infrastructure` | Lógica de negocio (servicios), EF Core + PostgreSQL, PDF del estado de cuenta, WhatsApp, sincronización offline. |
| `SistemaCobranzaApp.Api` | API REST (minimal APIs, autenticación JWT). **Toda** la información pasa por aquí. |
| `SistemaCobranzaApp.Shared` | Biblioteca Blazor compartida: **todas las pantallas**, componentes, DTOs y el cliente de la API (`ApiCliente`). |
| `SistemaCobranzaApp.Web` | Anfitrión **Blazor Server** (versión web). Usa las pantallas de `Shared`. |
| `SistemaCobranzaApp` (MAUI) | App móvil (**Android**) con Blazor Hybrid. Usa las mismas pantallas de `Shared` y agrega servicios nativos. |

```
 Web (Blazor Server) ─┐
                      ├─► ApiCliente ─► API REST (JWT) ─► Servicios ─► PostgreSQL
 Android (MAUI)  ─────┘        │                              │
   └─ SQLite local (jornada)   │                              ├─► Cola WhatsApp ─► Evolution API / API oficial (Meta)
                               └─ sync al reconectar          └─► PDF estado de cuenta
```

Despliegue (Docker Compose): `postgres`, `evolution` (WhatsApp no oficial), `api`, `web` y el túnel de Cloudflare.
Los scripts de `db/init` crean la base (esquema, triggers, índices, semillas y usuario administrador).

## 2. Usuarios y roles

Roles sembrados: **1 Administrador, 2 Supervisor, 3 Vendedor, 4 Cobrador, 5 Consulta**. El acceso es con usuario y
contraseña (JWT). Usuario inicial de la base nueva: `admin`.

- **Web:** uso administrativo y de oficina (todos los módulos del menú).
- **Android:** además del menú, los roles **Vendedor (3)** y **Cobrador (4)** trabajan con **Mi jornada** (modo offline).

## 3. Módulos (menú)

| Grupo | Pantalla | Para qué sirve |
|---|---|---|
| Inicio | **Home (Cartera)** | Dashboard: ventas, cobrado, saldo por cobrar, cobrado hoy, gráficas por zona y por día. |
| Operación diaria | **Cartera de Clientes** | Buscar/alta de clientes, detalle del cliente, ventas y estado de cuenta. |
| | **Registro de Pagos** | Buscar por cliente, por venta o por **QR**, registrar abonos, ver el detalle y el aviso de venta liquidada. |
| | **Reversión de Pagos** | Revertir pagos registrados (se administra en central). |
| | **Gastos Operativos** | Gastos, categorías, cuentas, pagos y comprobantes. |
| | **Pago Comisión Vendedores** | Pago de comisiones según el esquema de cada producto. |
| Zonas y personal | **Asignación de Vendedores / Zonas y Rutas** | Zonas, rutas, vendedores y cobradores (cada perfil se vincula a un usuario y a una zona). |
| Productos e inventario | **Productos** | Catálogo (productos, categorías, marcas, colores, tallas) con exportación a Excel. |
| | **Inventario** | Existencia central, existencia por zona, **envío a zonas** (propuestas → confirmación) e historial de movimientos. |
| Catálogos y tablas | **Calificación de clientes**, **Reglas de venta y crédito**, **Parámetros generales**, **Mensajes WhatsApp** | Clasificación NUEVO/BUENO/REGULAR/MALO/MOROSO por días sin pago, condiciones, frecuencias, comisiones, parámetros del sistema y cola de WhatsApp. |
| Análisis | **Reportes** y **Estadísticas** | Reportes de Cartera, Pagos, Ventas, Recuperación, Clientes y Seguimiento, con buscador, orden por columna y exportación a Excel con formato. |
| Seguridad y empresa | **Roles y Usuarios**, **Datos de la Empresa** | Usuarios y permisos; nombre/identidad de la empresa (se usa en tickets, PDF y mensajes). |

## 4. Flujos principales

### 4.1 Venta
1. Se elige el cliente y los productos; la **condición** es `CRÉDITO` o `CONTADO`.
2. A crédito: enganche, frecuencia (diario, semanal, catorcenal, quincenal, mensual), número de cuotas y tasa; se genera el plan de pagos.
3. Se genera un **código de venta** (`V{idVendedor}-{yyMMdd}-{HHmmss}-{sufijo}`), se descuenta inventario y se calcula la comisión.
4. Se encola el **WhatsApp** al cliente (si lo tiene activo) y se puede imprimir el **ticket**.
5. Estatus de la venta: `PENDIENTE` → `ABONANDO` → `PAGADA` (o `CANCELADA`). También existe **renovar** una venta (liquida la anterior).

### 4.2 Pago / abono
1. Se localiza al cliente, la venta o se escanea el **QR** del folio.
2. Se registra el abono; el saldo se recalcula en la base (triggers) y el estatus avanza. Si el saldo llega a cero se muestra **"Venta liquidada"**.
3. Movimientos posibles: ABONO, ENGANCHE, PAGO COMPLETO, LIQUIDACIÓN PRONTO PAGO, AJUSTE, **BONIFICACIÓN PROMOCIÓN** (racha de pagos consecutivos) y LIQUIDADA POR RENOVACIÓN.
4. Se genera el ticket y se encola el WhatsApp del pago (con saldo anterior, abono y saldo actual).
5. Condonaciones, liquidaciones negociadas, ajustes y reversiones se hacen **solo en central**.

### 4.3 Estado de cuenta (PDF)
El botón **Estado de cuenta** pide el PDF a la API (siempre con la información central: historial completo, bonificaciones y huella de
integridad) y lo **muestra dentro de la app** con botones de **compartir (WhatsApp)** y **descargar**. Requiere internet. En Android el visor ocupa toda la pantalla.

### 4.4 Inventario
Existencia **central** y por **zona**. Flujo de reparto: se arma una **propuesta** de envío a una zona → queda *Pendiente* →
se *Confirma* (se convierte en envío y mueve existencias) o se *Rechaza*. Todo movimiento queda en el **historial**
(entrada/ajuste central, salida/entrada de zona).

### 4.5 Seguimiento y clasificación de clientes
Según los días sin pago (parámetros `CLIENTE_DIAS_*`) el cliente pasa de NUEVO/BUENO a REGULAR, MALO o MOROSO. En **Reportes → Seguimiento**
se ve el atraso por venta (color según el período de pago y los días de gracia) y se puede actualizar el estatus/observaciones del cliente.

### 4.6 Gastos y comisiones
Gastos por categoría y cuenta, con comprobantes y reportes por categoría, zona, cobrador y vendedor. Las comisiones se calculan por
esquema del producto (porcentaje o fijo; contado vs. crédito) y se pagan desde su pantalla.

## 5. WhatsApp (resumen)

- Al registrar una venta o un pago se **encola** un mensaje en la misma transacción; un servicio en segundo plano lo envía con reintentos (máx. 3).
- Parámetros en `ParametrosSistema`: `WHATSAPP_ENVIO_ACTIVO`, `WHATSAPP_NOTIFICAR_VENTA`, `WHATSAPP_NOTIFICAR_PAGO`, y **`WHATSAPP_PROVEEDOR`**
  (`EVOLUTION` u `OFICIAL`).
- **EVOLUTION:** comprobante completo. **OFICIAL:** mensaje corto con plantillas aprobadas en Meta (crédito, contado, pago).
- El cliente puede darse de baja respondiendo **BAJA** (solo con Evolution por ahora).
- Detalle completo en [`deploy/GUIA-WHATSAPP-PROVEEDORES.md`](../deploy/GUIA-WHATSAPP-PROVEEDORES.md) y [`deploy/GUIA-WHATSAPP.md`](../deploy/GUIA-WHATSAPP.md).

## 6. Android: jornada offline

(Detalle técnico en [`JornadasOffline.md`](JornadasOffline.md).)

1. **Con internet:** el vendedor/cobrador inicia sesión y pulsa **Descargar jornada completa**. Se guarda en SQLite (privado de la app) todo lo
   de **su zona**: clientes de todas las rutas activas, ventas con detalles/cuotas, historial de pagos y, para vendedores, productos,
   inventario de la zona, condiciones y frecuencias.
2. **Sin señal:** el **vendedor** captura clientes y ventas; el **cobrador** registra abonos normales. Cada captura se confirma al guardarse localmente,
   reserva existencias o descuenta saldo localmente y genera su ticket (impresión Bluetooth).
3. **Al volver internet:** se sincroniza automáticamente (al detectar red, al abrir la jornada, cada 30 s si hay pendientes o con **Sincronizar con central**).
   Central valida usuario, rol, zona, saldo e inventario; cada operación queda *pendiente*, *confirmada* o *rechazada* con el motivo.
   Los UUID de venta/pago evitan duplicados al reintentar.
4. Los saldos descargados son un **corte** de central: pueden cambiar en otros dispositivos. No se puede reemplazar la jornada con pendientes.

## 7. Web vs. Android

| Aspecto | Web (Blazor Server) | Android (MAUI) |
|---|---|---|
| Pantallas y reglas de negocio | Las mismas (`Shared`) | Las mismas (`Shared`) |
| Conexión | Siempre en línea | En línea, o **offline** con jornada (vendedor/cobrador) |
| Almacenamiento local | Ninguno (`AlmacenJornadaNoDisponible`) | SQLite privado por usuario |
| Impresión de tickets | No disponible (stub) | Impresora **Bluetooth** |
| Cámara / QR | Navegador | Permiso de cámara nativo |
| Archivos (Excel, PDF) | Descarga del navegador / hoja de compartir web | Hoja de compartir del sistema (WhatsApp, guardar, etc.) |
| Estado de cuenta en PDF | Visor + compartir/descargar | Visor a pantalla completa + compartir/descargar |
| Diseño | Menú lateral | Diseño móvil con menú adaptado |
| Mensajes "ya liquidada", buscadores, tablas ordenables, exportaciones | Igual | Igual |

## 8. Parámetros que controlan el comportamiento

Pantalla **Parámetros** (tabla `ParametrosSistema`, cambios en caliente):

- Clasificación de clientes: `CLIENTE_DIAS_REGULAR` (15), `CLIENTE_DIAS_MALO` (31), `CLIENTE_DIAS_MOROSO` (61).
- Seguimiento: `SEGUIMIENTO_DIAS_GRACIA_ALERTA` (15), `SEGUIMIENTO_DIAS_GRACIA_CRITICO` (31).
- Promoción de racha: `PROMO_PAGOS_CONSECUTIVOS_ACTIVA` y `PROMO_PAGOS_CONSECUTIVOS_META` (pagos consecutivos para bonificar).
- Ticket: `TICKET_PAGO_COMPACTO` (compacto o detallado).
- WhatsApp: ver sección 5 (envío, notificaciones, proveedor, plantillas de Meta, purga).

## 9. Zona horaria y datos

- Fechas guardadas en UTC y mostradas en la zona del proyecto (`America/Mexico_City`).
- Saldos y estatus de venta los mantiene la base (triggers); el inventario y el saldo se bloquean al confirmar para evitar sobrecobros o sobreventas concurrentes.
- Las tablas de paso (cola de WhatsApp) se purgan automáticamente según los parámetros de purga.

## 10. Operación y soporte

| Tema | Dónde |
|---|---|
| Despliegue (Linux, Windows, Docker) | `deploy/GUIA-DESPLIEGUE*.md` |
| WhatsApp | `deploy/GUIA-WHATSAPP.md`, `deploy/GUIA-WHATSAPP-PROVEEDORES.md` |
| Jornadas offline | `docs/JornadasOffline.md` |
| Dashboard | `docs/plan-dashboard.md` |
| Identidad de la empresa | `docs/identidad-empresa.md` |
| Esquema y datos iniciales | `db/init/*.sql` |
