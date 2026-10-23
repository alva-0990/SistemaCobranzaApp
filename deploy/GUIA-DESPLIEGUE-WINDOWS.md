# Guía de despliegue a producción (Windows Server) — Sistema Cobranza

Despliegue en **Windows Server** con **Cloudflare Tunnel** y dominio **cobragil.com**.

## Arquitectura

```
App Android  → https://api.cobragil.com ─┐
Navegador    → https://app.cobragil.com ─┤→ Cloudflare Tunnel → servidor Windows:
                                          │   • API  .NET  (127.0.0.1:8081)  [Servicio NSSM]
                                          │   • Web  Blazor(127.0.0.1:8080)  [Servicio NSSM]
                                          └→ PostgreSQL (127.0.0.1:5432)
```

Diferencias clave vs. Ubuntu:
- Servicios: **NSSM** (en vez de systemd).
- Secretos: archivos `.env` dentro de cada carpeta, aplicados como variables de entorno del servicio.
- PostgreSQL: **Docker Desktop** o instalación nativa de Postgres para Windows.

---

## Requisitos (instalar en el servidor Windows)

Recomendado usar **Chocolatey** (gestor de paquetes). En PowerShell como Administrador:

```powershell
# Instalar Chocolatey (si no lo tienes)
Set-ExecutionPolicy Bypass -Scope Process -Force; `
[System.Net.ServicePointManager]::SecurityProtocol = 3072; `
iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

# Utilidades
choco install -y git nssm
```

### .NET 10 — ASP.NET Core Hosting Bundle
Descarga e instala el **Hosting Bundle** de .NET 10 (incluye el runtime de ASP.NET Core):
- https://dotnet.microsoft.com/download/dotnet/10.0  → "ASP.NET Core Runtime — Hosting Bundle (Windows)".

Verifica:
```powershell
dotnet --info
```

### PostgreSQL
**Opción A (recomendada): Docker Desktop**
- Instala Docker Desktop para Windows y habilita WSL2.
- Levanta la BD con el mismo compose del repo (ver siguiente sección).

**Opción B: Postgres nativo para Windows**
- `choco install -y postgresql16` y crea la BD/usuario, luego ejecuta los scripts de `db/init/` manualmente con `psql`.

---

## PARTE A — En tu máquina de desarrollo

### A1. Publicar
```powershell
pwsh deploy/publicar.ps1
# o:  powershell -ExecutionPolicy Bypass -File deploy\publicar.ps1
```

### A2. Copiar al servidor
Copia `publish-api\*` a `C:\cobranza\api` y `publish-web\*` a `C:\cobranza\web`
(RDP, carpeta compartida, o `robocopy` si tienes acceso por red).

### A3. APK de producción (igual que en la guía general)
El APK ya apunta a `https://api.cobragil.com`.

---

## PARTE B — En el servidor Windows

### B1. Clonar repo y levantar PostgreSQL (Docker Desktop)
```powershell
cd C:\
git clone <TU-REPO> cobranza-src
cd cobranza-src

# Edita docker-compose.yml: cambia POSTGRES_PASSWORD
notepad docker-compose.yml

docker compose up -d
docker compose ps
```
> Los scripts `db/init/*.sql` corren solo la primera vez que se crea el volumen.

### B2. Instalar servicios (API + Web) con NSSM
Asegúrate de haber copiado los binarios a `C:\cobranza\api` y `C:\cobranza\web` (Parte A2).
```powershell
cd C:\cobranza-src
powershell -ExecutionPolicy Bypass -File deploy\instalar-servidor.ps1
```

### B3. Editar secretos y re-aplicar
```powershell
notepad C:\cobranza\api\.env
#   ConnectionStrings__Cobranza=...Password=LA_MISMA_DE_DOCKER_COMPOSE
#   Jwt__Key=<clave aleatoria de 32+ bytes>

notepad C:\cobranza\web\.env
#   ApiSettings__BaseUrl=https://api.cobragil.com

# Re-aplica el .env a los servicios y reinícialos:
powershell -ExecutionPolicy Bypass -File deploy\instalar-servidor.ps1
```
(El script detecta los servicios existentes; alternativamente `nssm restart cobranza-api`.)

Generar una clave JWT fuerte en PowerShell:
```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
```

### B4. Verificar localmente
```powershell
curl http://127.0.0.1:8081/api/empresa
curl -I http://127.0.0.1:8080
nssm status cobranza-api
nssm status cobranza-web
```

---

## PARTE C — Cloudflare Tunnel (Windows)

### C1. Instalar cloudflared
```powershell
choco install -y cloudflared
```

### C2. Autenticar y crear el túnel
```powershell
cloudflared tunnel login
cloudflared tunnel create cobragil
# Anota el TUNNEL_ID y la ruta de credenciales (C:\Users\<usuario>\.cloudflared\<ID>.json)
```

### C3. Configurar el túnel
Crea `C:\Users\<usuario>\.cloudflared\config.yml` basándote en `deploy/cloudflared-config.example.yml`:
```yaml
tunnel: <TUNNEL_ID>
credentials-file: C:\Users\<usuario>\.cloudflared\<TUNNEL_ID>.json

ingress:
  - hostname: api.cobragil.com
    service: http://127.0.0.1:8081
  - hostname: app.cobragil.com
    service: http://127.0.0.1:8080
  - service: http_status:404
```

### C4. DNS y servicio de Windows
```powershell
cloudflared tunnel route dns cobragil api.cobragil.com
cloudflared tunnel route dns cobragil app.cobragil.com

cloudflared service install
Start-Service cloudflared
Get-Service cloudflared
```

### C5. Verificar desde internet
```powershell
curl https://api.cobragil.com/api/empresa
# navegador: https://app.cobragil.com
```

---

## Actualizaciones futuras

En tu máquina:
```powershell
pwsh deploy/publicar.ps1
# copia publish-api\* y publish-web\* a C:\cobranza\api y C:\cobranza\web
```
En el servidor:
```powershell
nssm restart cobranza-api
nssm restart cobranza-web
```

---

## Comandos útiles (Windows)

```powershell
# Estado y control
nssm status cobranza-api
nssm restart cobranza-api
nssm stop cobranza-web

# Logs: por defecto NSSM no captura stdout; para habilitarlo:
nssm set cobranza-api AppStdout C:\cobranza\api\logs\stdout.log
nssm set cobranza-api AppStderr C:\cobranza\api\logs\stderr.log
nssm restart cobranza-api

# Quitar un servicio (si necesitas reinstalar limpio)
nssm remove cobranza-api confirm
```

---

## Checklist de seguridad

- [ ] Contraseña de PostgreSQL cambiada en `docker-compose.yml` y en `C:\cobranza\api\.env` (deben coincidir).
- [ ] Clave JWT nueva en `C:\cobranza\api\.env`. NO uses la de desarrollo.
- [ ] Firewall de Windows: no expongas 5432, 8080, 8081 a internet (el túnel sale saliente).
- [ ] Los `.env` reales NUNCA se suben al repo (.gitignore ya los excluye).
