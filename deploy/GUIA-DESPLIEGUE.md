# Guía de despliegue a producción — Sistema Cobranza

Despliegue en **Ubuntu** con **Cloudflare Tunnel** y dominio **cobragil.com**.

## Arquitectura

```
App Android  → https://api.cobragil.com ─┐
Navegador    → https://app.cobragil.com ─┤→ Cloudflare Tunnel → servidor Ubuntu:
                                          │   • API  .NET  (127.0.0.1:8081)
                                          │   • Web  Blazor(127.0.0.1:8080)
                                          └→ PostgreSQL (Docker 127.0.0.1:5432)
```

Cloudflare entrega el HTTPS. El servidor NO expone puertos a internet (el túnel sale saliente).

---

## PARTE A — En tu máquina de desarrollo (Mac)

### A1. Publicar API y Web

```bash
cd /ruta/al/repo
bash deploy/publicar.sh
```
Genera `./publish-api` y `./publish-web`.

### A2. Subir los binarios al servidor

```bash
# Crea las carpetas destino en el servidor
ssh USUARIO@SERVIDOR 'sudo mkdir -p /opt/cobranza/api /opt/cobranza/web && sudo chown -R $USER /opt/cobranza'

scp -r ./publish-api/* USUARIO@SERVIDOR:/opt/cobranza/api/
scp -r ./publish-web/* USUARIO@SERVIDOR:/opt/cobranza/web/
```

### A3. Generar el APK de producción (Android)

Ya configurado para apuntar a `https://api.cobragil.com`.
```bash
export JAVA_HOME="/opt/homebrew/Cellar/openjdk@21/21.0.12.1/libexec/openjdk.jdk/Contents/Home"
dotnet publish SistemaCobranzaApp/SistemaCobranzaApp.csproj -c Release -f net10.0-android -p:JavaSdkDirectory="$JAVA_HOME"
```
El APK firmado queda en `SistemaCobranzaApp/bin/Release/net10.0-android/`.
(Para Google Play, firma el AAB con tu keystore de producción.)

---

## PARTE B — En el servidor Ubuntu (una sola vez)

### B1. Base del sistema
```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y curl git ufw
sudo ufw allow OpenSSH
sudo ufw enable        # NO abrir 80/443: el túnel sale saliente
```

### B2. Docker (para PostgreSQL)
```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
# Cierra y reabre la sesión SSH para aplicar el grupo docker
```

### B3. .NET 10 Runtime (ASP.NET Core)
```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O p.deb
sudo dpkg -i p.deb && rm p.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-10.0
dotnet --info
```

### B4. Clonar el repo y levantar PostgreSQL
```bash
cd /opt
sudo git clone <TU-REPO> cobranza
sudo chown -R $USER:$USER /opt/cobranza
cd /opt/cobranza

# Cambia la contraseña de la BD en docker-compose.yml (POSTGRES_PASSWORD)
nano docker-compose.yml

docker compose up -d
docker compose ps            # debe quedar "healthy"
docker compose logs postgres # confirma que corrieron los scripts de db/init/
```
> Los scripts `db/init/*.sql` solo se ejecutan la PRIMERA vez (al crear el volumen).

### B5. Instalar servicios con el script
Asegúrate de haber copiado los binarios (Parte A2) antes.
```bash
cd /opt/cobranza
sudo bash deploy/instalar-servidor.sh
```

### B6. Editar los secretos
```bash
sudo nano /etc/cobranza/api.env
#   ConnectionStrings__Cobranza=...Password=LA_MISMA_DE_DOCKER_COMPOSE
#   Jwt__Key=<pega aquí: openssl rand -base64 48>

sudo nano /etc/cobranza/web.env
#   ApiSettings__BaseUrl=https://api.cobragil.com

sudo systemctl restart cobranza-api cobranza-web
```

### B7. Verificar localmente
```bash
curl http://127.0.0.1:8081/api/empresa     # API responde
curl -I http://127.0.0.1:8080              # Web responde (200/302)
sudo systemctl status cobranza-api cobranza-web
```

---

## PARTE C — Cloudflare Tunnel

### C1. Instalar cloudflared
```bash
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb -o cloudflared.deb
sudo dpkg -i cloudflared.deb
```

### C2. Autenticar y crear el túnel
```bash
cloudflared tunnel login          # abre un enlace; elige el dominio cobragil.com
cloudflared tunnel create cobragil
# Anota el TUNNEL_ID y la ruta del archivo de credenciales (~/.cloudflared/<ID>.json)
```

### C3. Configurar el túnel
```bash
sudo mkdir -p /etc/cloudflared
sudo cp deploy/cloudflared-config.example.yml /etc/cloudflared/config.yml
sudo nano /etc/cloudflared/config.yml
#   Reemplaza <TUNNEL_ID> y la ruta credentials-file por los reales.
```

### C4. Crear los registros DNS y arrancar
```bash
cloudflared tunnel route dns cobragil api.cobragil.com
cloudflared tunnel route dns cobragil app.cobragil.com

sudo cloudflared service install
sudo systemctl enable --now cloudflared
sudo systemctl status cloudflared
```

### C5. Verificar desde internet
```bash
curl https://api.cobragil.com/api/empresa
# y abre https://app.cobragil.com en el navegador
```

---

## Actualizaciones futuras (re-despliegue)

En tu Mac:
```bash
bash deploy/publicar.sh
scp -r ./publish-api/* USUARIO@SERVIDOR:/opt/cobranza/api/
scp -r ./publish-web/* USUARIO@SERVIDOR:/opt/cobranza/web/
```
En el servidor:
```bash
sudo chown -R www-data:www-data /opt/cobranza/api /opt/cobranza/web
sudo systemctl restart cobranza-api cobranza-web
```

---

## Checklist de seguridad

- [ ] Contraseña de PostgreSQL cambiada en `docker-compose.yml` y `/etc/cobranza/api.env` (deben coincidir).
- [ ] Clave JWT nueva en `/etc/cobranza/api.env` (`openssl rand -base64 48`). NO uses la de desarrollo.
- [ ] `/etc/cobranza/*.env` con permisos `600` y propietario `root`.
- [ ] PostgreSQL solo en `127.0.0.1:5432` (ya está en docker-compose). No exponerlo.
- [ ] Firewall activo; sin 80/443 abiertos (el túnel no los necesita).
- [ ] Los `.env` reales NUNCA se suben al repo (.gitignore ya los excluye).

---

## Comandos útiles de diagnóstico

```bash
# Logs en vivo
sudo journalctl -u cobranza-api -f
sudo journalctl -u cobranza-web -f
sudo journalctl -u cloudflared -f

# Reiniciar
sudo systemctl restart cobranza-api cobranza-web cloudflared

# Estado de la BD
docker compose ps
docker compose logs postgres
```
