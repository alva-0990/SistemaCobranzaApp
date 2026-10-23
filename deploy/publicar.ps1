# =====================================================================
# Publica API y Web en Release y los deja en .\publish-api y .\publish-web.
# Ejecutar desde la RAÍZ del repo en tu máquina de desarrollo (PowerShell).
#
#   pwsh deploy/publicar.ps1
#   # o en Windows PowerShell:  powershell -ExecutionPolicy Bypass -File deploy\publicar.ps1
#
# Luego copia a tu servidor Windows (ejemplos):
#   - Compartir carpeta / RDP / robocopy a C:\cobranza\api y C:\cobranza\web
# =====================================================================
$ErrorActionPreference = "Stop"

Write-Host "==> Publicando API (Release)..."
dotnet publish SistemaCobranzaApp.Api/SistemaCobranzaApp.Api.csproj -c Release -o ./publish-api

Write-Host "==> Publicando Web (Release)..."
dotnet publish SistemaCobranzaApp.Web/SistemaCobranzaApp.Web.csproj -c Release -o ./publish-web

Write-Host "==> Listo. Paquetes en .\publish-api y .\publish-web"
Write-Host "    Config en el servidor (NO en el repo):"
Write-Host "      C:\cobranza\api\.env   (ver deploy/cobranza-api.env.example)"
Write-Host "      C:\cobranza\web\.env   (ver deploy/cobranza-web.env.example)"
