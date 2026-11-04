# Guía de configuración: Postgres + Evolution API (WhatsApp)

Configuración desde cero para tres escenarios:

- **A.** Desarrollo en Mac
- **B.** Desarrollo en Windows
- **C.** Producción en Ubuntu

## Cómo está armado

| Pieza | Dev (Mac / Windows) | Prod (Ubuntu) |
|---|---|---|
| Postgres y Evolution | Docker, con `.env.local` | Docker, con `.env` |
| API | Rider (para depurar) | Docker, con `.env` |
| Secretos de la API | `appsettings.Development.json` + user-secrets | Variables de `.env` |
| Contraseña de BD | `cobranza_dev` | La real del servidor |

Dos claves que **inventas tú**. Cada una debe ser **igual en los dos lados** donde se usa:

| Clave | Quién la envía → quién la valida | Dónde se configura |
|---|---|---|
| `WHATSAPP_APIKEY` | API → Evolution (header `apikey`) | `.env` / `.env.local` y la API |
| `WHATSAPP_WEBHOOK_SECRET` | Evolution → API (header `X-Webhook-Secret`, aviso de BAJA) | `.env` / `.env.local`, la API **y** el webhook de Evolution |

Notas importantes:

- El **Manager web de Evolution no tiene campo de headers**. El webhook con `X-Webhook-Secret` solo se puede registrar por comando (paso 8). Si después guardas el webhook desde el Manager, puede borrar el header: repite el paso 8.
- `POSTGRES_PASSWORD` solo se aplica la **primera vez** que se crea `cobranza_pgdata`. Si luego la cambias en el archivo, también hay que cambiarla en la base (`ALTER ROLE cobranza PASSWORD '...'`) o nada conectará.
- `db/init` solo se ejecuta cuando `cobranza_pgdata` está vacío.

---

## A. Desarrollo en Mac

**1. Clona el repo** y abre una terminal en la carpeta raíz.

**2. Crea `.env.local`:**

```bash
cp .env.docker.example .env.local
```

Edítalo con estos valores:

```
POSTGRES_PASSWORD=cobranza_dev
POSTGRES_PASSWORD_URL=cobranza_dev
WHATSAPP_BASEURL=http://evolution:8080
WHATSAPP_INSTANCIA=cobranza
WHATSAPP_APIKEY=<resultado de: openssl rand -hex 32>
WHATSAPP_WEBHOOK_SECRET=<resultado de: openssl rand -hex 24>
```

`JWT_KEY`, `TUNNEL_TOKEN` y `WEB_API_BASEURL` no se usan en dev (cualquier valor).

**3. Levanta Postgres y Evolution:**

```bash
docker compose --env-file .env.local up -d postgres evolution
docker compose --env-file .env.local ps
```

Espera a que ambos digan `Up`. La primera vez se crea la base desde `db/init`.

**4. Dale los secretos a la API de Rider:**

```bash
cd SistemaCobranzaApp.Api
dotnet user-secrets set "Whatsapp:ApiKey" "<mismo WHATSAPP_APIKEY>"
dotnet user-secrets set "Whatsapp:WebhookSecret" "<mismo WHATSAPP_WEBHOOK_SECRET>"
dotnet user-secrets list
cd ..
```

`BaseUrl` (`http://localhost:8083`) e `Instancia` (`cobranza`) ya vienen en `appsettings.Development.json`.

**5. Arranca la API en Rider.** Usuario inicial: `admin` / `Admin123!` (cámbiala).

**6. Crea la instancia de WhatsApp:**

```bash
APIKEY=$(grep '^WHATSAPP_APIKEY=' .env.local | cut -d= -f2-)
curl -s -X POST http://127.0.0.1:8083/instance/create \
  -H "apikey: $APIKEY" -H "Content-Type: application/json" \
  -d '{"instanceName":"cobranza","integration":"WHATSAPP-BAILEYS","qrcode":true}'
```

**7. Escanea el QR:**

1. Abre `http://127.0.0.1:8083/manager` e inicia sesión con tu `WHATSAPP_APIKEY`.
2. Abre la instancia `cobranza` → **Connect**.
3. Celular: WhatsApp → Dispositivos vinculados → Vincular un dispositivo → escanea.

**8. Registra el webhook** (solo por comando):

```bash
SECRETO=$(grep '^WHATSAPP_WEBHOOK_SECRET=' .env.local | cut -d= -f2-)
curl -s -X POST http://127.0.0.1:8083/webhook/set/cobranza \
  -H "apikey: $APIKEY" -H "Content-Type: application/json" \
  -d "{\"webhook\":{\"enabled\":true,\"url\":\"http://host.docker.internal:8081/api/whatsapp/webhook\",\"events\":[\"MESSAGES_UPSERT\"],\"headers\":{\"X-Webhook-Secret\":\"$SECRETO\"}}}"
```

`host.docker.internal` es como Evolution (dentro de Docker) llega a la API que corre en Rider.

**9. Verifica:**

```bash
# Debe decir "state":"open"
curl -s http://127.0.0.1:8083/instance/connectionState/cobranza -H "apikey: $APIKEY"

# Debe mostrar la URL, enabled:true y X-Webhook-Secret en headers
curl -s http://127.0.0.1:8083/webhook/find/cobranza -H "apikey: $APIKEY"

# Debe llegar el mensaje (52 + 10 dígitos, sin espacios)
curl -s -X POST http://127.0.0.1:8083/message/sendText/cobranza \
  -H "apikey: $APIKEY" -H "Content-Type: application/json" \
  -d '{"number":"52XXXXXXXXXX","text":"Prueba"}'
```

---

## B. Desarrollo en Windows

Mismos pasos que en Mac, con estas diferencias:

- Usa **Docker Desktop** (`host.docker.internal` funciona igual).
- Usa **PowerShell** en la carpeta del repo.
- Los user-secrets se guardan en `%APPDATA%\Microsoft\UserSecrets\`: **no se copian** desde la Mac; repite el paso 4.

**Paso 2:**

```powershell
Copy-Item .env.docker.example .env.local
# Generar una clave:
-join ((1..32) | ForEach-Object { '{0:x2}' -f (Get-Random -Max 256) })
```

**Pasos 3 y 4:** los mismos comandos (`docker compose ...`, `dotnet user-secrets ...`).

**Pasos 6, 8 y 9:**

```powershell
$APIKEY  = (Select-String '^WHATSAPP_APIKEY=' .env.local).Line.Split('=',2)[1]
$SECRETO = (Select-String '^WHATSAPP_WEBHOOK_SECRET=' .env.local).Line.Split('=',2)[1]
$H = @{ apikey = $APIKEY }

# 6. Crear instancia
Invoke-RestMethod -Method Post http://127.0.0.1:8083/instance/create -Headers $H -ContentType 'application/json' `
  -Body '{"instanceName":"cobranza","integration":"WHATSAPP-BAILEYS","qrcode":true}'

# 8. Webhook
$body = @{ webhook = @{ enabled = $true; url = 'http://host.docker.internal:8081/api/whatsapp/webhook';
          events = @('MESSAGES_UPSERT'); headers = @{ 'X-Webhook-Secret' = $SECRETO } } } | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post http://127.0.0.1:8083/webhook/set/cobranza -Headers $H -ContentType 'application/json' -Body $body

# 9. Verificar
Invoke-RestMethod http://127.0.0.1:8083/instance/connectionState/cobranza -Headers $H
Invoke-RestMethod http://127.0.0.1:8083/webhook/find/cobranza -Headers $H
```

Puedes vincular el mismo número en Mac y en Windows: cada uno es un "dispositivo vinculado" distinto.

---

## C. Producción en Ubuntu

**1. Crea `.env`** con valores reales (distintos a los de dev):

```bash
cp .env.docker.example .env
nano .env
```

```
POSTGRES_PASSWORD=<real>
POSTGRES_PASSWORD_URL=<la misma, codificada para URL>
WHATSAPP_BASEURL=http://evolution:8080
WHATSAPP_INSTANCIA=cobranza
WHATSAPP_APIKEY=<nueva>
WHATSAPP_WEBHOOK_SECRET=<nueva>
JWT_KEY=...   TUNNEL_TOKEN=...   WEB_API_BASEURL=...
```

Para codificar la contraseña (escríbela y presiona Enter):

```bash
python3 -c "import urllib.parse;print(urllib.parse.quote(input(),safe=''))"
```

**2. Levanta todo:**

```bash
docker compose up -d
```

En Ubuntu no hay user-secrets: la API toma todo de `.env` (el compose lo pasa como `Whatsapp__ApiKey`, `Whatsapp__WebhookSecret`, etc.).

**3. Crea la instancia y escanea el QR**: igual que A6 y A7, leyendo de `.env`. Para abrir el Manager desde tu computadora:

```bash
ssh -L 8083:127.0.0.1:8083 usuario@servidor
# luego abre http://localhost:8083/manager
```

**4. Registra el webhook**: igual que A8, leyendo de `.env` y con esta URL (la API es otro contenedor):

```
http://api:8081/api/whatsapp/webhook
```

**5. Verifica**: igual que A9.

---

## Solución de problemas

| Síntoma | Causa | Solución |
|---|---|---|
| `401 Unauthorized` al llamar a Evolution | `$APIKEY` vacía en esa terminal | Ejecuta la línea `APIKEY=...` en la misma terminal |
| `exists:false` al enviar | Número inexistente o mal escrito | Usa 52 + 10 dígitos reales |
| Evolution reinicia: `invalid port number in database URL` | Contraseña con `#`, `*`, `!` sin codificar | Revisa `POSTGRES_PASSWORD_URL` |
| `password authentication failed` | La contraseña del archivo no coincide con la de la base | `ALTER ROLE cobranza PASSWORD '...'` o recrea `cobranza_pgdata` |
| BAJA no funciona | Secreto distinto entre Evolution y la API, o API sin reiniciar | Repite paso 4/8 y reinicia la API |
| Mensajes en `Pendiente` | Evolution sin configurar o desconectado | Revisa `connectionState` y `error_log` en `colamensajeswhatsapp` |
