#!/usr/bin/env bash
# =====================================================================
# Publica API y Web en Release y los deja en ./publish-api y ./publish-web.
# Ejecutar desde la RAÍZ del repo en tu máquina de desarrollo.
#
#   bash deploy/publicar.sh
#
# Luego sube al servidor (ajusta usuario/host):
#   scp -r ./publish-api USUARIO@SERVIDOR:/opt/cobranza/api
#   scp -r ./publish-web USUARIO@SERVIDOR:/opt/cobranza/web
# =====================================================================
set -euo pipefail

echo "==> Publicando API (Release)…"
dotnet publish SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj -c Release -o ./publish-api

echo "==> Publicando Web (Release)…"
dotnet publish SistemaCobranzaApp.Web/SistemaCobranzaApp.Web.csproj -c Release -o ./publish-web

echo "==> Listo. Paquetes en ./publish-api y ./publish-web"
echo "    Secretos y configuración en el servidor (NO en el repo):"
echo "      /etc/cobranza/api.env   (ver deploy/cobranza-api.env.example: contraseña BD + clave JWT)"
echo "      /etc/cobranza/web.env   (ver deploy/cobranza-web.env.example: ApiSettings__BaseUrl)"
echo "    Los servicios systemd los cargan con EnvironmentFile."
