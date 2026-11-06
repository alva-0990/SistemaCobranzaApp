# Guía: WhatsApp con dos flujos de envío (Evolution API y API oficial de Meta)

Esta guía describe **cómo quedó** el envío de notificaciones de WhatsApp después de agregar la API oficial.
La instalación de Postgres + Evolution API (QR, webhook, `.env`) sigue en [`GUIA-WHATSAPP.md`](GUIA-WHATSAPP.md).

## 1. Resumen

| | Flujo `EVOLUTION` (por defecto) | Flujo `OFICIAL` |
|---|---|---|
| API | Evolution API (sesión de WhatsApp vinculada por QR) | WhatsApp Cloud API de Meta |
| Mensaje | **Comprobante completo** de venta/pago, armado por el sistema (`MensajesWhatsApp`) | **Mensaje corto** definido en una **plantilla aprobada en Meta**; el sistema envía el nombre de la plantilla y las variables |
| Secretos | `Whatsapp__BaseUrl`, `Whatsapp__Instancia`, `Whatsapp__ApiKey`, `Whatsapp__WebhookSecret` | `WhatsappOficial__PhoneNumberId`, `WhatsappOficial__AccessToken`, `WhatsappOficial__VersionApi` |
| Pausa entre mensajes | 15 s (anti-baneo) | 1 s |
| Respuesta "BAJA" | Sí (webhook de Evolution) | **No implementado** (ver sección 9) |

Qué se envía y cuándo **no cambia**: se encola un mensaje al registrar una venta o un pago (dentro de su transacción),
un servicio en segundo plano (`WorkerWhatsApp`) lo envía con reintentos (máximo 3) y la pantalla **Mensajes WhatsApp**
muestra el estado. El flujo se elige **al momento de enviar**, así que cambiarlo aplica también a lo que ya esté pendiente.

## 2. Parámetros (tabla `ParametrosSistema`)

Se editan en caliente desde la pantalla **Parámetros**. La configuración se cachea ~30 s, así que el cambio se aplica
en menos de un minuto, sin reiniciar.

| Clave | Valores | Por defecto | Para qué sirve |
|---|---|---|---|
| `WHATSAPP_ENVIO_ACTIVO` | `SI` / `NO` | `SI` | Interruptor maestro. Con `NO` no se encola nada. |
| `WHATSAPP_NOTIFICAR_VENTA` | `SI` / `NO` | `SI` | Enviar al registrar una venta. |
| `WHATSAPP_NOTIFICAR_PAGO` | `SI` / `NO` | `SI` | Enviar al registrar un pago. |
| `WHATSAPP_PROVEEDOR` | `EVOLUTION` / `OFICIAL` | `EVOLUTION` | **Flujo de envío.** Cualquier valor distinto de `OFICIAL` usa Evolution. |
| `WHATSAPP_OFICIAL_PLANTILLA_VENTA_CREDITO` | texto | `venta_credito` | Nombre de la plantilla aprobada en Meta para venta a crédito. |
| `WHATSAPP_OFICIAL_PLANTILLA_VENTA_CONTADO` | texto | `venta_contado` | Nombre de la plantilla aprobada en Meta para venta de contado. |
| `WHATSAPP_OFICIAL_PLANTILLA_PAGO` | texto | `pago_registrado` | Nombre de la plantilla aprobada en Meta para pago realizado. |
| `WHATSAPP_OFICIAL_IDIOMA` | código Meta | `es_MX` | Idioma con el que se aprobaron las plantillas. |
| `WHATSAPP_PURGA_ACTIVA` | `SI` / `NO` | `SI` | Borrado automático de mensajes ya enviados. |
| `WHATSAPP_PURGA_DIAS` | número | `7` | Días que se conservan los enviados antes de borrarlos. |

Los parámetros nuevos están sembrados en `db/init/02_seed.sql`. Las plantillas de texto `WHATSAPP_PLANTILLA_VENTA` y
`WHATSAPP_PLANTILLA_PAGO` **ya no existen** (nunca se usaron al armar el mensaje).

> Para dejar de enviar sin tocar nada más: `WHATSAPP_ENVIO_ACTIVO = NO`.
> Para cambiar de flujo: `WHATSAPP_PROVEEDOR`.

## 3. Dónde se colocan los secretos de la API oficial

`WhatsappOficial__AccessToken` y `WhatsappOficial__PhoneNumberId` **no** van en la tabla de parámetros: son secretos y
se leen de la configuración de la API (variables de entorno). El doble guion bajo (`__`) representa la anidación:
`WhatsappOficial__AccessToken` = sección `WhatsappOficial`, clave `AccessToken`.

| Escenario | Dónde se colocan |
|---|---|
| **Docker (Mac / Windows / Ubuntu)** | En `.env` (producción) o `.env.local` (desarrollo), junto a `WHATSAPP_APIKEY`. El `docker-compose.yml` ya las pasa a la API. |
| **API en Rider (desarrollo)** | `dotnet user-secrets` en `SistemaCobranzaApp.Api` (ver abajo). |
| **Ubuntu sin Docker (systemd)** | En `/etc/cobranza/api.env` (plantilla: `deploy/cobranza-api.env.example`), luego `sudo systemctl restart cobranza-api`. |

### Docker: `.env` / `.env.local`

```
WHATSAPP_OFICIAL_PHONE_NUMBER_ID=<Phone number ID de Meta>
WHATSAPP_OFICIAL_ACCESS_TOKEN=<token permanente>
WHATSAPP_OFICIAL_VERSION_API=v21.0
```

Después: `docker compose up -d api` (recrea el contenedor con las variables nuevas).
El compose las entrega como `WhatsappOficial__PhoneNumberId`, `WhatsappOficial__AccessToken` y `WhatsappOficial__VersionApi`.

### Rider (desarrollo)

```bash
cd SistemaCobranzaApp.Api
dotnet user-secrets set "WhatsappOficial:PhoneNumberId" "<Phone number ID>"
dotnet user-secrets set "WhatsappOficial:AccessToken" "<token permanente>"
dotnet user-secrets list
```

(En `user-secrets` se usa `:` en lugar de `__`.)

### systemd (Ubuntu sin Docker)

En `/etc/cobranza/api.env`:

```
WhatsappOficial__PhoneNumberId=<Phone number ID>
WhatsappOficial__AccessToken=<token permanente>
WhatsappOficial__VersionApi=v21.0
```

Mantén el archivo con `chmod 600` y propietario `root`.

> **Seguridad:** el token permanente permite enviar mensajes a nombre del negocio. No lo subas al repositorio ni lo
> pegues en la tabla de parámetros (queda en texto plano, visible en la pantalla y en los respaldos).
> Nota: `.env.docker.example` contiene una `WHATSAPP_APIKEY` de ejemplo con aspecto de valor real; conviene rotarla y
> dejar el ejemplo vacío.

## 4. Configurar la API oficial en Meta

1. Crea (o usa) una cuenta de **Meta Business** y una app de tipo **Business** en <https://developers.facebook.com>.
2. Agrega el producto **WhatsApp** y registra tu número en **WhatsApp Business Platform (Cloud API)**.
   El número no puede estar activo en la app normal de WhatsApp.
3. En **WhatsApp → Configuración de la API** copia el **Identificador del número de teléfono** → `PhoneNumberId`.
4. Genera un **token permanente**: Business Settings → Usuarios → **Usuarios del sistema** → crea uno con rol de
   administrador → asígnale la app y la cuenta de WhatsApp con permiso `whatsapp_business_messaging` → **Generar token**.
   Ese es el `AccessToken`. (El token temporal de 24 h de la pantalla de prueba **no sirve** para producción.)
5. Crea y aprueba las **tres plantillas** (sección 5).
6. Con el número de prueba que da Meta solo se puede escribir a destinatarios previamente verificados. Para enviar a
   cualquier cliente necesitas tu propio número verificado.

## 5. Plantillas de mensaje en Meta (mensajes cortos)

Las crea y aprueba el negocio en **WhatsApp Manager → Plantillas de mensajes**. Categoría sugerida: **Utilidad**
(*Utility*), idioma **Español (México)** (`es_MX`). Los nombres deben coincidir con los parámetros
`WHATSAPP_OFICIAL_PLANTILLA_*`. Las variables del **cuerpo** (`{{1}}`, `{{2}}`, …) llegan **en este orden exacto**:

| Plantilla (nombre sugerido) | Se usa cuando | Variables |
|---|---|---|
| `venta_credito` | Venta a crédito | `{{1}}` cliente · `{{2}}` folio de venta · `{{3}}` total · `{{4}}` plan (ej. "10 cuotas de $250.00 (semanal)") |
| `venta_contado` | Venta de contado | `{{1}}` cliente · `{{2}}` folio de venta · `{{3}}` total |
| `pago_registrado` | Pago realizado | `{{1}}` cliente · `{{2}}` folio de pago · `{{3}}` monto abonado · `{{4}}` saldo actual |

Textos de ejemplo para el cuerpo (puedes redactarlos distinto mientras respeten el orden y la cantidad de variables):

- **venta_credito:** `Hola {{1}}, registramos tu compra {{2}} por {{3}}. Plan de pagos: {{4}}. Gracias por tu preferencia.`
- **venta_contado:** `Hola {{1}}, registramos tu compra {{2}} de contado por {{3}}. Gracias por tu preferencia.`
- **pago_registrado:** `Hola {{1}}, recibimos tu pago {{2}} por {{3}}. Tu saldo actual es {{4}}. Gracias.`

Reglas de Meta que ya cuida el sistema: las variables se envían sin saltos de línea ni espacios repetidos, y una
variable vacía se manda como `-`. Si cambias el **número** de variables de una plantilla en Meta, hay que ajustar el
código (`MensajesWhatsApp.ArmarVenta` / `ArmarPago`); cambiar solo el texto no requiere nada.

## 6. Cómo cambiar de flujo

1. Verifica los secretos del flujo destino (sección 3) y, para `OFICIAL`, que las plantillas estén **aprobadas**.
2. En **Parámetros**, cambia `WHATSAPP_PROVEEDOR` a `OFICIAL` (o a `EVOLUTION` para volver).
3. En menos de un minuto el siguiente lote usa el nuevo flujo. Registra una venta o un pago de prueba y revisa la
   pantalla **Mensajes WhatsApp**.

Si el flujo elegido **no tiene credenciales**, el worker **no falla ni gasta reintentos**: deja los mensajes en
`Pendiente` y escribe un aviso en el log (`el flujo OFICIAL no está configurado`). Al configurarlo, salen solos.

## 7. Qué cambió en la base de datos

Cambios ya incluidos en `db/init` (base nueva):

- `ColaMensajesWhatsapp.plantilla` (`VARCHAR(30)`): `VENTA_CREDITO` | `VENTA_CONTADO` | `PAGO`.
- `ColaMensajesWhatsapp.variables` (`TEXT`): arreglo JSON con las variables posicionales, p. ej. `["Juan Pérez","V123","$1,500.00","10 cuotas de $250.00 (semanal)"]`.
- Parámetros nuevos en `02_seed.sql` (sección 2).

Cada mensaje encolado guarda **ambas** versiones: el texto completo (`mensaje`, para Evolution) y las variables
(`variables`, para la API oficial). Por eso se puede cambiar de flujo con mensajes pendientes.

> Los mensajes encolados **antes** de este cambio no tienen plantilla ni variables: con `OFICIAL` fallarán con el
> error "El mensaje no tiene plantilla/variables…" (por Evolution se envían normal).

## 8. Verificación y solución de problemas

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| Mensajes quedan en `Pendiente` | El flujo elegido no tiene credenciales | Revisa los secretos (sección 3) y el log de la API |
| `Fallido` con `API oficial de WhatsApp respondió 401` | Token inválido o vencido (¿usaste el temporal de 24 h?) | Genera el token permanente de un usuario del sistema |
| `Fallido` con `... 400 ... template ... does not exist` | Nombre o idioma de plantilla no coincide, o no está aprobada | Compara con `WHATSAPP_OFICIAL_PLANTILLA_*` e `..._IDIOMA`; revisa el estado en WhatsApp Manager |
| `Fallido` con `... 400 ... number of parameters does not match` | La plantilla en Meta tiene distinto número de variables | Ajusta la plantilla (sección 5) |
| `Fallido` con `... (#131030) Recipient phone number not in allowed list` | Estás con el número de prueba de Meta | Agrega el destinatario como verificado o usa tu número propio |
| `Fallido` con `El mensaje no tiene plantilla/variables` | Mensaje encolado antes de este cambio | Reenvía con `EVOLUTION` o ignóralo |
| No llega nada y no hay errores | `WHATSAPP_ENVIO_ACTIVO` o `WHATSAPP_NOTIFICAR_*` en `NO`, o el cliente tiene `NotificarWhatsApp` apagado / sin teléfono | Revisa los parámetros y la ficha del cliente |

Detalle del error: pantalla **Mensajes WhatsApp** (columna de error) o la columna `error_log` de `colamensajeswhatsapp`.

## 9. Pendientes y límites conocidos

- **Respuesta "BAJA" con la API oficial:** el webhook actual (`/api/whatsapp/webhook`) está hecho para Evolution. Con
  `OFICIAL` no se procesan las respuestas entrantes, así que un cliente que responda "BAJA" **no** se da de baja
  automáticamente. Hay que implementar el webhook de Meta (verificación `hub.challenge` y firma `X-Hub-Signature-256`)
  si se necesita. Mientras tanto, la baja se hace manualmente (`NotificarWhatsApp` del cliente).
- **Cumplimiento de Meta:** con plantillas de utilidad, el cliente debe haber aceptado recibir mensajes (opt-in).
- **Pruebas:** el código compila, pero el envío real a la API oficial depende de credenciales y plantillas aprobadas;
  prueba primero con el número de prueba de Meta.
- **Token en base de datos:** no implementado por seguridad. Si se necesita cambiarlo sin tocar el servidor, habría que
  guardarlo cifrado y mostrarlo enmascarado en la pantalla de Parámetros.

## 10. Mapa de archivos

| Archivo | Qué hace |
|---|---|
| `SistemaCobranzaApp.Api/Whatsapp/WorkerWhatsApp.cs` | Envía la cola; elige Evolution u oficial según `WHATSAPP_PROVEEDOR`. |
| `SistemaCobranzaApp.Api/Whatsapp/WhatsAppOficialOptions.cs` | Secretos de la API oficial (`WhatsappOficial__*`). |
| `SistemaCobranzaApp.Api/Whatsapp/EvolutionApiOptions.cs` | Secretos de Evolution (`Whatsapp__*`). |
| `SistemaCobranzaApp.Infrastructure/Whatsapp/MensajesWhatsApp.cs` | Arma el comprobante completo (Evolution) y las variables (oficial). |
| `SistemaCobranzaApp.Infrastructure/Whatsapp/ParametrosWhatsApp.cs` | Claves de parámetros y configuración resuelta. |
| `SistemaCobranzaApp.Infrastructure/Whatsapp/LectorParametrosWhatsApp.cs` | Lee los parámetros con caché de 30 s. |
| `SistemaCobranzaApp.Infrastructure/Whatsapp/ServicioColaWhatsApp.cs` | Encola el mensaje al registrar venta o pago. |
| `db/init/01_schema.sql`, `db/init/02_seed.sql` | Columnas nuevas de la cola y parámetros nuevos. |
| `docker-compose.yml`, `.env.docker.example`, `deploy/cobranza-api.env.example` | Variables `WHATSAPP_OFICIAL_*` / `WhatsappOficial__*`. |
