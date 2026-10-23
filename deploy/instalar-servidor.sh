#!/usr/bin/env bash
# =====================================================================
# Instalador del lado servidor (Ubuntu) para Sistema Cobranza.
# Ejecutar DESDE LA RAÍZ del repo clonado en el servidor (/opt/cobranza):
#
#   cd /opt/cobranza
#   sudo bash deploy/instalar-servidor.sh
#
# Requisitos previos (ver GUIA-DESPLIEGUE.md): Docker, .NET runtime y
# los binarios publicados ya copiados a /opt/cobranza/api y /opt/cobranza/web.
#
# Qué hace:
#   1. Crea /etc/cobranza con los .env (si no existen) a partir de las plantillas.
#   2. Instala los servicios systemd de API y Web.
#   3. Deja los servicios habilitados y arrancados.
# NO toca los .env si ya existen (para no pisar tus secretos).
# =====================================================================
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ETC_DIR="/etc/cobranza"

if [[ "$EUID" -ne 0 ]]; then
  echo "Este script debe ejecutarse con sudo/root." >&2
  exit 1
fi

echo "==> 1/4  Preparando $ETC_DIR y archivos de entorno (.env)…"
mkdir -p "$ETC_DIR"

instalar_env() {
  local ejemplo="$1" destino="$2"
  if [[ -f "$destino" ]]; then
    echo "    - $destino ya existe; NO se sobrescribe."
  else
    cp "$ejemplo" "$destino"
    chmod 600 "$destino"
    chown root:root "$destino"
    echo "    - Creado $destino (EDÍTALO con tus secretos reales)."
  fi
}
instalar_env "$REPO_DIR/deploy/cobranza-api.env.example" "$ETC_DIR/api.env"
instalar_env "$REPO_DIR/deploy/cobranza-web.env.example" "$ETC_DIR/web.env"

echo "==> 2/4  Instalando servicios systemd…"
cp "$REPO_DIR/deploy/cobranza-api.service" /etc/systemd/system/cobranza-api.service
cp "$REPO_DIR/deploy/cobranza-web.service" /etc/systemd/system/cobranza-web.service

echo "==> 3/4  Ajustando propietario de los binarios a www-data…"
[[ -d /opt/cobranza/api ]] && chown -R www-data:www-data /opt/cobranza/api || echo "    (aviso) /opt/cobranza/api no existe todavía"
[[ -d /opt/cobranza/web ]] && chown -R www-data:www-data /opt/cobranza/web || echo "    (aviso) /opt/cobranza/web no existe todavía"

echo "==> 4/4  Recargando systemd y arrancando servicios…"
systemctl daemon-reload
systemctl enable --now cobranza-api
systemctl enable --now cobranza-web

echo ""
echo "==> Listo. Estado de los servicios:"
systemctl --no-pager status cobranza-api | head -n 5 || true
systemctl --no-pager status cobranza-web | head -n 5 || true
echo ""
echo "IMPORTANTE:"
echo "  - Si es la PRIMERA vez, edita los secretos y reinicia:"
echo "      sudo nano /etc/cobranza/api.env    # contraseña BD + clave JWT"
echo "      sudo nano /etc/cobranza/web.env    # URL de la API"
echo "      sudo systemctl restart cobranza-api cobranza-web"
echo "  - Pruebas locales:"
echo "      curl http://127.0.0.1:8081/api/empresa"
echo "      curl -I http://127.0.0.1:8080"
