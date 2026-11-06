# Manual de uso — CobrAgil (Web y Android)

Guía paso a paso para el personal de oficina, vendedores y cobradores. La app es la misma en el navegador y en Android;
donde cambia, se indica con **[Web]** o **[Android]**.

> Los nombres de botones y campos están tomados de las pantallas. Los módulos de **Gastos**, **Pago de comisiones**,
> **Zonas y rutas** y **Roles y usuarios** se describen de forma general porque no se revisaron en detalle: valida esos
> apartados con la pantalla antes de repartir el manual.

## Contenido

1. [Entrar al sistema](#1-entrar-al-sistema)
2. [Quién puede hacer qué](#2-quién-puede-hacer-qué)
3. [Clientes](#3-clientes)
4. [Registrar una venta](#4-registrar-una-venta)
5. [Registrar un pago (cobranza)](#5-registrar-un-pago-cobranza)
6. [Estado de cuenta en PDF](#6-estado-de-cuenta-en-pdf)
7. [Renovar, editar, cancelar y revertir](#7-renovar-editar-cancelar-y-revertir)
8. [Productos e inventario](#8-productos-e-inventario)
9. [Reportes y estadísticas](#9-reportes-y-estadísticas)
10. [Gastos y comisiones](#10-gastos-y-comisiones)
11. [Zonas, personal, usuarios y empresa](#11-zonas-personal-usuarios-y-empresa)
12. [Parámetros del sistema](#12-parámetros-del-sistema)
13. [Mensajes de WhatsApp](#13-mensajes-de-whatsapp)
14. [Android: trabajar sin internet (Mi jornada)](#14-android-trabajar-sin-internet-mi-jornada)
15. [Imprimir tickets](#15-imprimir-tickets)
16. [Problemas frecuentes](#16-problemas-frecuentes)

---

## 1. Entrar al sistema

1. Abre la app (o la dirección web) y escribe tu **Usuario** y **Contraseña**. El ícono del ojo muestra u oculta la contraseña.
2. Pulsa **Entrar**. Si los datos son incorrectos verás un mensaje en rojo.
3. Para salir, usa **Cerrar sesión**. **[Android]** al cerrar sesión se necesita internet para volver a entrar.

La primera vez en una base nueva, el administrador inicial es `admin` (cambia su contraseña de inmediato).

## 2. Quién puede hacer qué

| Rol | Uso típico |
|---|---|
| **Administrador** | Todo: catálogos, usuarios, parámetros, reportes. |
| **Supervisor** | Seguimiento de cartera y operación diaria. |
| **Vendedor** | Registra clientes y ventas. **[Android]** trabaja con *Mi jornada*. |
| **Cobrador** | Registra abonos. **[Android]** trabaja con *Mi jornada*. |
| **Consulta** | Solo consulta. |

El detalle fino de permisos por pantalla lo define la configuración de roles; si una opción no aparece o está
desactivada, tu rol no la tiene.

## 3. Clientes

**Ruta:** Operación diaria → **Cartera de Clientes**.

**Buscar:** escribe el *nombre* o el *teléfono*; la búsqueda se hace mientras escribes. Pulsa un cliente para abrir su detalle.

**Nuevo cliente:** botón **Nuevo cliente** (en el celular, **Nuevo**). Captura *Nombre*, *Apellido paterno*, *Apellido materno*,
*Teléfono principal* y *Teléfono secundario*, además de la dirección y la **ruta** que le corresponde. Guarda.
- El teléfono se usa para los avisos de WhatsApp (se toman los últimos 10 dígitos con lada de México).
- Un cliente puede darse de baja de los avisos; en ese caso no recibirá mensajes.

**Detalle del cliente:** muestra sus datos, sus ventas y el botón **Estado de cuenta**. En cada venta hay acciones: *Ver detalle*,
*Renovar cuenta*, *Editar venta* y *Cancelar venta*.

## 4. Registrar una venta

Desde el detalle del cliente, inicia una **Nueva venta**. El formulario tiene dos zonas.

**Izquierda — Artículos**
1. En **Seleccionar artículo** busca por nombre o categoría (se muestra *Disp.* = existencia en la zona del cliente; solo hay productos con existencia).
2. Ajusta **Cant.** y **Precio** (se propone el sugerido) y pulsa **Agregar artículo**. Puedes quitar un renglón con el bote de basura.
3. *(Opcional)* **Promoción / Condiciones de liquidación:** elige un **Plazo** y un **Monto de liquidación** y pulsa **Agregar condición**
   (ej.: 1 mes → $1,200 · 2 meses → $1,500). Son los planes de pronto pago que luego se ofrecen al cobrar.

**Derecha — Resumen y plan**
1. **Vendedor** y **Tipo de venta** (*Crédito* o *Contado*).
2. Revisa **Subtotal**, captura **Descuento global** si aplica y verifica el **TOTAL FINAL**.
3. **Contado:** no hay enganche ni cuotas; se registra como pago completo y la venta queda **PAGADA**.
4. **Crédito:** captura el **Enganche (Pronto pago)**, elige la **Frecuencia de cobro** (diario, semanal, catorcenal, quincenal o mensual)
   y la **Cuota fija exigida/pactada**. El sistema muestra el **Monto neto a financiar** y calcula el plan.
5. Guarda la venta con el botón de confirmación al pie del formulario.

Al guardar: se genera el **código de venta**, se descuenta el inventario de la zona, se calcula la comisión, se puede **imprimir el ticket**
y se manda el **WhatsApp** al cliente (si está activo).

## 5. Registrar un pago (cobranza)

**Ruta:** Operación diaria → **Registro de Pagos**.

**Buscar la cuenta** (3 pestañas):
- **Cliente:** escribe el nombre (busca mientras escribes) o el *Id del cliente* y pulsa **Enter / Buscar**.
- **Venta:** escribe el *Id o código de la venta* y pulsa **Buscar**.
- **QR:** apunta la cámara al QR del folio de la venta; la busca sola.

**Seleccionar la venta:** si el cliente tiene varias, elige la que va a pagar. Arriba verás *Datos de la venta* y, si ya no debe nada,
un aviso verde **"Venta liquidada"** (no se puede abonar).

**Registrar pago:**
1. Pulsa el botón grande **Registrar pago**.
2. Revisa el contexto: *Cliente, Venta, Saldo pendiente* y *Cuota exigida*.
3. Elige el tipo:
   - **Abono normal** (el más común).
   - **Liquidación anticipada (plan de pronto pago)** — si la venta tiene planes; elige el plan y, si el monto lo cubre, se aplica el descuento y se liquida.
   - **Liquidación por acuerdo** — el cliente paga el monto convenido y el resto se **condona**; la cuenta queda en $0.00.
4. Captura **Monto del abono** (máximo el saldo), opcionalmente el **Cobrador** y **Observaciones**.
5. Pulsa **Registrar pago**. Aparece el ticket para imprimir y se manda el WhatsApp.

**Promoción de racha:** si está activa, un aviso muestra "Racha de pagos consecutivos: X de N". Un pago puntual suma; al completarla,
el sistema aplica automáticamente la **bonificación por promoción**.

## 6. Estado de cuenta en PDF

1. En el detalle del cliente (o en la pantalla de pagos) pulsa **Estado de cuenta**. Requiere internet.
2. Se abre el PDF **dentro de la app** (en **[Android]** a pantalla completa).
3. Botones: **Compartir por WhatsApp** (abre la hoja de compartir; elige WhatsApp), **Descargar** y **Cerrar**.
4. Si el cliente tiene movimientos del dispositivo sin sincronizar **[Android]**, el sistema avisa que no aparecerán hasta sincronizar.

## 7. Renovar, editar, cancelar y revertir

- **Renovar cuenta:** crea una venta nueva que liquida la anterior (movimiento "Liquidada por renovación"). Se abre el formulario de venta en *modo renovación*.
- **Editar venta / Cancelar venta:** desde el detalle del cliente. Cancelar deja la venta en estado *CANCELADA*.
- **Reversión de pagos:** Operación diaria → **Reversión de Pagos**. Busca el pago y revierte. Se hace solo en central (Web).
- **Ajustes, condonaciones y liquidaciones negociadas** se administran en central; **[Android]** solo registra abonos normales.

## 8. Productos e inventario

### Productos
**Ruta:** Productos e inventario → **Productos**. Pestañas: *Productos, Categorías, Marcas, Colores, Tallas*.
- Busca por código o descripción; **Incluir inactivos** muestra los dados de baja.
- **Nuevo producto:** categoría, marca, color/talla, costo, precio sugerido y mínimo, stock mínimo y **esquema de comisión**
  (debe existir al menos uno activo).
- Las columnas se ordenan con un clic (en *Productos* el orden aplica a la página que ves).
- **Exportar a Excel** baja el catálogo completo según la búsqueda.

### Inventario
**Ruta:** Productos e inventario → **Inventario**.
1. **Inventario Central:** existencia por producto, con indicador *Disponible / Bajo mínimo*. Registra entradas o ajustes al seleccionar un producto.
2. **Existencias por Zona:** elige la zona y consulta o ajusta su existencia.
3. **Envío a Zonas:**
   - **Nueva propuesta:** elige la zona destino, agrega productos (solo aparecen los que tienen existencia en central) con su cantidad y **Guardar propuesta**.
   - **Propuestas:** busca por #, zona, estatus, clave o producto; abre una para ver su detalle, **confirmarla** (se convierte en envío y mueve existencias) o **rechazarla**.
   - **Exportar a Excel** del detalle: se titula *Propuesta* (pendiente), *Envío* (confirmada) o *Propuesta rechazada*.
4. **Historial de inventario:** todos los movimientos con filtros de tipo, zona y fechas; buscador, orden por columna y **Exportar a Excel**.

## 9. Reportes y estadísticas

**Ruta:** **Reportes**. Pestañas: *Cartera, Pagos, Ventas, Recuperación, Clientes, Seguimiento*.
1. Elige los filtros de la pestaña (fechas, zona, ruta, cobrador, vendedor, estatus, etc.).
2. Pulsa **Generar reporte**.
3. Usa el **buscador** sobre la tabla (no vuelve a consultar), ordena con clic en los encabezados y pagina.
4. **Exportar a Excel:** libro con empresa, filtros, indicadores, tabla y totales.

**Seguimiento:** muestra por venta los días sin pago con color (verde dentro del período, amarillo en gracia, rojo crítico) y permite
**actualizar el estatus/observaciones** del cliente con el botón **Estatus**.

**Home (Cartera)** y **Estadísticas:** tableros con ventas, cobrado, saldo por cobrar, cobrado hoy y gráficas por zona y por día; filtra por fechas y zona.

## 10. Gastos y comisiones

- **Gastos Operativos:** captura gastos por **categoría** y **cuenta**, adjunta comprobantes y registra pagos; hay reportes por categoría, zona, cobrador y vendedor.
- **Pago Comisión Vendedores:** lista las comisiones generadas según el esquema de cada producto (porcentaje o fijo; contado o crédito) y permite registrar su pago.
- Los esquemas de comisión se definen en **Reglas de Venta y Crédito**.

## 11. Zonas, personal, usuarios y empresa

- **Control de Zonas y Rutas:** crea zonas y las rutas de cada zona.
- **Asignación de Vendedores:** da de alta vendedores y cobradores, **vincúlalos a un usuario** y asígnales su **zona**. Cada usuario debe tener un único perfil activo.
- **Roles y Usuarios:** crea usuarios y asigna rol. Para trabajar en Android sin internet, el usuario debe ser *Vendedor (3)* o *Cobrador (4)*.
- **Datos de la Empresa:** nombre e identidad que aparecen en tickets, PDF, reportes y mensajes.
- **Calificación de Clientes (B/M/R):** clasifica al cliente según días sin pago.
- **Reglas de Venta y Crédito:** condiciones (crédito/contado), frecuencias de pago, esquemas de comisión, plazos y estatus.

## 12. Parámetros del sistema

**Ruta:** Catálogos y tablas → **Parámetros Generales** (solo administración). Los cambios aplican en pocos segundos.

| Parámetro | Efecto |
|---|---|
| `CLIENTE_DIAS_REGULAR` / `_MALO` / `_MOROSO` | Días sin pago para cambiar la clasificación (15 / 31 / 61). |
| `SEGUIMIENTO_DIAS_GRACIA_ALERTA` / `_CRITICO` | Colores de atraso en *Seguimiento* (15 / 31). |
| `PROMO_PAGOS_CONSECUTIVOS_ACTIVA` / `_META` | Enciende la racha de pagos y define cuántos pagos consecutivos dan la bonificación. |
| `TICKET_PAGO_COMPACTO` | `SI` = ticket de pago compacto, `NO` = detallado. |
| `WHATSAPP_ENVIO_ACTIVO` | Interruptor general de WhatsApp (`SI`/`NO`). |
| `WHATSAPP_NOTIFICAR_VENTA` / `_PAGO` | Avisar al registrar ventas / pagos. |
| `WHATSAPP_PROVEEDOR` | `EVOLUTION` (API externa) u `OFICIAL` (API oficial de Meta). |
| `WHATSAPP_OFICIAL_PLANTILLA_*`, `WHATSAPP_OFICIAL_IDIOMA` | Nombres de plantillas aprobadas en Meta e idioma (solo flujo oficial). |
| `WHATSAPP_PURGA_ACTIVA` / `_DIAS` | Borrado automático de mensajes ya enviados y días que se conservan. |

## 13. Mensajes de WhatsApp

**Ruta:** Catálogos y tablas → **Mensajes WhatsApp**.
- Muestra la cola de mensajes: teléfono, cliente, mensaje, estado, intentos, error y fechas.
- Estados: **Pendiente** (en espera), **Procesando**, **Enviado**, **Fallido** (tras 3 intentos).
- Si algo queda *Pendiente* o *Fallido*, revisa el error y los parámetros de la sección 12. Guía técnica:
  [`deploy/GUIA-WHATSAPP-PROVEEDORES.md`](../deploy/GUIA-WHATSAPP-PROVEEDORES.md).
- Con Evolution el cliente puede responder **BAJA** para dejar de recibir avisos.

## 14. Android: trabajar sin internet (Mi jornada)

Solo para usuarios **Vendedor** o **Cobrador**. Después de iniciar sesión llegas a **Mi jornada**.

### Al iniciar el día (con internet)
1. Abre el panel **Descargar zona al inicio de la jornada** (muestra tu zona).
2. Pulsa **Descargar jornada completa**. Baja los clientes y cuentas de todas las rutas de tu zona (y, si eres vendedor, productos e inventario de la zona).
3. Si hay pendientes de una jornada anterior, el botón está deshabilitado: primero **sincroniza**.

### Durante el día (con o sin señal)
- El indicador muestra **Con conexión / Sin conexión** y **Pendientes: N**.
- **Vendedor:** *Clientes* (`/jornada/ventas`) → busca o crea el cliente → registra la venta con el mismo formulario de la sección 4. Los artículos reservan existencia localmente.
- **Cobrador:** *Cobranza* (`/jornada/cobranza`) → busca por cliente, venta o QR → **Registrar pago** (solo abonos normales). El abono descuenta el saldo local.
- Cada captura se confirma solo cuando quedó guardada en el teléfono, y se ofrece **imprimir ticket** (Bluetooth, no necesita internet).
- Las consultas son locales, aunque regrese internet. El **estado de cuenta en PDF** sí requiere internet.

### Sincronizar
- Se envía solo al detectar internet, al abrir la jornada o cada 30 segundos si hay pendientes; o manualmente con **Sincronizar con central**.
- En la tabla **Operaciones de la jornada** cada renglón dice: *Guardada en dispositivo · pendiente*, *Confirmada por central* o el *motivo del rechazo*.
- Un rechazo **no borra** lo capturado: avisa a la oficina para conciliarlo y reintenta.
- Si la sesión caducó, pulsa **Renovar sesión para sincronizar** (tus pendientes siguen guardados).
- Android puede cerrar la app en segundo plano; al volver a abrirla se reanuda la sincronización.

### Qué NO se hace en la jornada
Ajustes, condonaciones, liquidaciones negociadas y reversiones (se hacen en central). Para ver ventas que otro dispositivo acaba de sincronizar, vuelve a descargar.

## 15. Imprimir tickets

- **[Android]** Impresora **Bluetooth**: al guardar una venta o pago se ofrece imprimir; también puedes **Imprimir ticket** desde *Operaciones de la jornada*.
  Empareja la impresora en Ajustes de Android y concede el permiso de Bluetooth cuando la app lo pida.
- **[Web]** La impresión Bluetooth no está disponible; usa el PDF o la impresión del navegador cuando aplique.
- Formato del ticket de pago: compacto o detallado según el parámetro `TICKET_PAGO_COMPACTO`.

## 16. Problemas frecuentes

| Situación | Qué hacer |
|---|---|
| "Esta pantalla requiere un usuario Vendedor o Cobrador" | Entraste con otro rol; la jornada solo es para vendedores y cobradores. |
| No me deja descargar la jornada | Falta internet o hay pendientes por sincronizar. Sincroniza primero. |
| Una operación aparece con error | Lee el motivo en *Operaciones de la jornada*; avisa a oficina. No se pierde la captura. |
| "Renovar sesión para sincronizar" | La sesión caducó: inicia sesión de nuevo y sincroniza. |
| El botón **Estado de cuenta** está deshabilitado | El cliente aún no existe en central (alta sin sincronizar) o no hay internet. |
| No hay productos para vender | La zona del cliente no tiene existencia; pide un envío desde *Inventario → Envío a Zonas*. |
| No llegan los WhatsApp | Revisa `WHATSAPP_ENVIO_ACTIVO` / `_NOTIFICAR_*`, que el cliente tenga teléfono y avisos activos, y la pantalla *Mensajes WhatsApp*. |
| El buscador de clientes no encuentra | Prueba con otro dato (teléfono o parte del nombre); en pagos, un número solo se busca como *Id* con Enter. |
| El PDF no se ve en el visor | Requiere internet para cargar el visor; aun así puedes **Compartir** o **Descargar** el archivo. |
