# Guía de despliegue con Docker — Sistema Cobranza (recomendado)

Todo el stack (PostgreSQL + API + Web + túnel Cloudflare) en un solo `docker-compose`.
Funciona igual en Ubuntu o Windows (con Docker Desktop).

## Arquitectura

```
Internet ── Cloudflare ── (túnel) ── contenedor cloudflared ─┐  (red interna de Docker)
                                                              ├── api   : http://api:8081
                                                              ├── web   : http://web:8080
                                                              └── postgres : postgres:5432
```
Ningún puerto se expone al host (salvo, opcionalmente, 5432 para administrar la BD).
Cloudflare entrega el HTTPS; el token del túnel vive en `.env`.

---

## Requisitos en el servidor

- **Docker** + plugin Compose.
  - Ubuntu: `curl -fsSL https://get.docker.com | sudo sh`
  - Windows: Docker Desktop (con WSL2).
- Git.

No necesitas instalar .NET ni nada más: las imágenes lo traen.

---

## PASO 1 — Crear el túnel en Cloudflare (una vez, en el panel)

1. Entra a **Cloudflare → Zero Trust → Networks → Tunnels → Create a tunnel**.
2. Tipo **Cloudflared**, nómbralo `cobragil`. Copia el **token** que te muestra.
3. En **Public Hostnames** del túnel, agrega dos:
   - `api.cobragil.com` → Service: `HTTP://api:8081`
   - `app.cobragil.com` → Service: `HTTP://web:8080`
   > Usa los nombres de servicio de Docker (`api`, `web`), porque `cloudflared` corre en la misma red interna.

---

## PASO 2 — Clonar el repo en el servidor

```bash
cd /opt
sudo git clone <TU-REPO> cobranza
sudo chown -R $USER:$USER /opt/cobranza
cd /opt/cobranza
```

## PASO 3 — Configurar secretos (.env)

```bash
cp .env.docker.example .env
nano .env
```
Rellena:
- `POSTGRES_PASSWORD` → una contraseña fuerte.
- `JWT_KEY` → `openssl rand -base64 48`.
- `WEB_API_BASEURL` → `https://api.cobragil.com`.
- `TUNNEL_TOKEN` → el token del Paso 1.

## PASO 4 — Levantar todo

```bash
docker compose up -d --build
```
Esto construye las imágenes de API y Web, levanta Postgres (ejecuta `db/init/*.sql` la primera vez), y conecta el túnel.

## PASO 5 — Verificar

```bash
docker compose ps                 # todos "running"/"healthy"
docker compose logs -f api        # log de la API
docker compose logs -f cloudflared

# desde internet:
curl https://api.cobragil.com/api/empresa
# navegador: https://app.cobragil.com
```

---

## Actualizaciones (re-desplegar una versión nueva)

```bash
cd /opt/cobranza
git pull
docker compose up -d --build       # reconstruye solo lo que cambió y reinicia
```

---

## Operación y diagnóstico

```bash
docker compose ps                      # estado
docker compose logs -f api | web | cloudflared | postgres
docker compose restart api             # reiniciar un servicio
docker compose down                    # detener todo (conserva el volumen de datos)
docker compose up -d                   # volver a levantar

# Entrar a la BD (si publicaste el puerto 5432):
psql "host=127.0.0.1 port=5432 dbname=sistema_cobranza user=cobranza"
```

---

## Notas importantes

- **WebSockets (Blazor Server)**: Cloudflare los soporta de forma nativa; no requieres configuración extra.
- **Puerto 5432**: el compose lo publica en `127.0.0.1:5432` solo para que administres la BD desde el propio servidor. Si no lo necesitas, comenta ese bloque `ports:` del servicio `postgres`.
- **App Android**: no se dockeriza; se compila a APK y apunta a `https://api.cobragil.com` (ya configurado).
- **Primer arranque de la BD**: los scripts de `db/init/` solo corren al crear el volumen `cobranza_pgdata`. Si ya tenías una BD con otra contraseña, ajústala o recrea el volumen (perderías datos).

---

## Checklist de seguridad

- [ ] `.env` con `POSTGRES_PASSWORD` y `JWT_KEY` reales (NO los de ejemplo).
- [ ] `.env` NO subido al repo (el `.gitignore` ya lo excluye).
- [ ] Token del túnel tratado como secreto.
- [ ] Puerto 5432 sin publicar (o solo en `127.0.0.1`) — nunca abierto a internet.
