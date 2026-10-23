# Jornadas offline en Android

## Preparación en central

1. Crear los usuarios con rol **Vendedor (3)** o **Cobrador (4)** desde Roles y Usuarios.
2. Vincular al vendedor/cobrador mediante **IdUsuario** y asignarle **IdZona**. Debe existir un único perfil activo por usuario.
3. El login devuelve la zona; el servidor vuelve a comprobar el vínculo al descargar y sincronizar. No se requiere asignar rutas por separado.

Los scripts de `db/init` incluyen `07_jornadas_offline.sql` y `08_clientes_jornada.sql`. Para una base ya inicializada, aplicar una vez:

```sh
docker compose exec -T postgres psql -U cobranza -d sistema_cobranza -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d/07_jornadas_offline.sql
docker compose exec -T postgres psql -U cobranza -d sistema_cobranza -v ON_ERROR_STOP=1 -f /docker-entrypoint-initdb.d/08_clientes_jornada.sql
```

El script agrega IdUsuario cuando falte, índices de consulta y un UUID único opcional a las ventas; conserva los datos existentes. Una tabla asignacionjornada de instalaciones anteriores queda sin uso.

## Inicio de jornada

El primer login y la descarga requieren internet. En Android, los roles 3 y 4 llegan a **Mi jornada** después del login.
La jornada corresponde a la zona vinculada al usuario. No se selecciona una ruta.
**Descargar jornada completa** guarda todos los clientes activos de todas las rutas activas de esa zona, sus ventas con detalles, cuotas y planes, y el historial de pagos.
Para vendedores incluye productos, inventario de la zona, condiciones, frecuencias y plazos. Se recorren todas las páginas de clientes y productos.

## Trabajo sin señal

- **Vendedor:** selecciona un cliente descargado y captura la venta con el formulario existente. Los artículos pendientes reservan existencias localmente.
- **Cobrador:** selecciona un cliente, una cuenta y registra un abono. Los abonos pendientes descuentan el saldo disponible localmente.
- Cada captura se confirma en pantalla únicamente después de guardarse en SQLite, en el almacenamiento privado de Android.
- Los datos y la cola están separados por usuario. Cerrar y reabrir la app conserva la jornada del último usuario autenticado.
- Para acceder a la jornada descargada después de reiniciar no se necesita un nuevo login mientras se conserve la sesión local. Al cerrar sesión explícitamente se requiere internet para volver a autenticar.
- Los saldos y las existencias descargadas son un corte de central: pueden cambiar en otros dispositivos mientras se trabaja offline.

La jornada offline permite altas de clientes, ventas a clientes nuevos o descargados y abonos normales. Los ajustes, condonaciones, liquidaciones negociadas y reversiones se administran en central. Un cobrador necesita descargar nuevamente para ver ventas que otro dispositivo acaba de sincronizar.

## Sincronización y conflictos

Con la aplicación ejecutándose, la cola se envía al detectar internet, al abrir la jornada, cada 30 segundos si quedan pendientes, o al pulsar **Sincronizar con central**.
Android puede suspender o finalizar la aplicación en segundo plano; al volver a abrirla se reanudan los pendientes. No se promete sincronización con la aplicación detenida.

Los UUID de venta y pago se conservan al reintentar. Si central aplicó la operación pero se perdió la respuesta, devuelve la operación existente.
El servidor comprueba usuario activo, rol, perfil vinculado, zona, cliente, saldo e inventario. El vendedor/cobrador y el usuario de registro se toman del perfil vinculado por IdUsuario y del token.
La fecha real de captura se conserva en UTC y se presenta en la zona central del proyecto (`America/Mexico_City`).
Las filas de saldo e inventario se bloquean al confirmar para evitar sobrecobros y sobreventas concurrentes.

Cada operación aparece como **pendiente**, **confirmada por central** o con el motivo de rechazo.
Un rechazo no borra el cobro ni la venta capturada: central debe conciliarlo y después se puede reintentar.
No se permite reemplazar la jornada descargada mientras haya pendientes. Cuando todos están confirmados se actualiza el corte local de central.
Si el token expiró, **Renovar sesión para sincronizar** permite autenticarse de nuevo; los pendientes siguen guardados por usuario.

## Verificación

```sh
dotnet build tests/SistemaCobranzaApp.Offline.Tests/SistemaCobranzaApp.Offline.Tests.csproj -m:1
dotnet tests/SistemaCobranzaApp.Offline.Tests/bin/Debug/net10.0/SistemaCobranzaApp.Offline.Tests.dll
```

Las pruebas locales cubren reinicio, aislamiento por usuario y rol, respuesta perdida, errores de disco, idempotencia, saldo, inventario y conservación de conflictos.

Para pruebas reales de PostgreSQL, crear una base vacía cuyo nombre empiece por `codex_jornada_`, aplicar todos los scripts de `db/init` en orden y ejecutar:

```sh
dotnet tests/SistemaCobranzaApp.Offline.Tests/bin/Debug/net10.0/SistemaCobranzaApp.Offline.Tests.dll --postgres codex_jornada_verificacion
```

Estas pruebas insertan datos exclusivamente en esa base temporal y comprueban descarga de más de una página, permisos, captura real, UUID, fechas y concurrencia. Usan las credenciales locales de desarrollo del contenedor.

Si cambia la zona del usuario, la app conserva los pendientes anteriores y bloquea su envío desde la nueva zona para que central los concilie. Los documentos locales anteriores por ruta siguen siendo legibles; la próxima descarga completa incluye toda la zona.

La zona visible se obtiene de `resultado.idZona` y `resultado.zona` del login, no de claims del JWT. Al abrir la jornada con internet, `/api/jornada/zona` actualiza esos datos en la sesión persistida.
Si `/api/jornada/descarga` devuelve HTTP 404, comprobar la dirección indicada en el error y reiniciar la API con la compilación actual. El teléfono debe ejecutar también la versión actualizada de Android.

## Pantallas independientes

Las vistas originales `Clientes.razor`, `DetalleCliente.razor` y `Pagos.razor` mantienen su flujo por API. El módulo offline tiene sus propias páginas y formularios:

- `/jornada`: resumen, descarga, sincronización, totales, pendientes y reimpresión de tickets.
- `/jornada/ventas`: buscador con el diseño de Clientes, alta local de clientes, detalle y alta de ventas.
- `/jornada/cobranza`: buscadores por cliente, venta y QR con el diseño de Pagos, detalle de cuenta y registro de abonos.

Los clientes se guardan con ID local y UUID. La sincronización confirma primero el alta y sustituye ese ID al enviar sus ventas; si el alta falla, sus ventas permanecen pendientes. El servidor garantiza un UUID único para cada alta.
Los tickets de venta y pago se guardan con la captura. Bluetooth no necesita internet: se ofrece imprimir al guardar y se puede reimprimir desde el resumen. La venta usa el mismo generador de central: `V{idVendedor}-{yyMMdd}-{HHmmss}-{sufijo}`, por ejemplo `V1-261019-032528-21Y`. Se guarda al capturar y se conserva en el ticket, los reintentos y central. El abono pendiente muestra referencia `LOCAL-{UUID}` y su condición local; después de confirmarse usa el folio de central. No se borra la operación si falla o se cancela la impresión.

Los formularios offline leen catálogos descargados. El formulario de cliente limita las rutas a la zona del usuario. Las consultas siguen siendo locales aunque regrese internet; solo la sincronización se comunica con central.
