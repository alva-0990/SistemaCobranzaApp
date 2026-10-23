# =====================================================================
# Instalador del lado servidor (Windows Server) para Sistema Cobranza.
# Registra API y Web como Servicios de Windows usando NSSM, cargando los
# secretos/configuración desde archivos .env.
#
# Ejecutar como ADMINISTRADOR, desde la raíz del repo en el servidor:
#   powershell -ExecutionPolicy Bypass -File deploy\instalar-servidor.ps1
#
# Requisitos previos (ver GUIA-DESPLIEGUE-WINDOWS.md):
#   - .NET 10 ASP.NET Core Runtime (Hosting Bundle) instalado.
#   - PostgreSQL corriendo (Docker Desktop o nativo) en 127.0.0.1:5432.
#   - NSSM instalado y en el PATH (choco install nssm).
#   - Binarios publicados copiados a C:\cobranza\api y C:\cobranza\web.
# =====================================================================
$ErrorActionPreference = "Stop"

$RepoDir   = Split-Path -Parent $PSScriptRoot
$BaseDir   = "C:\cobranza"
$ApiDir    = Join-Path $BaseDir "api"
$WebDir    = Join-Path $BaseDir "web"
$DotnetExe = (Get-Command dotnet).Source

function Require-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Ejecuta este script como Administrador."
    }
}

function Copiar-EnvSiFalta($ejemplo, $destino) {
    if (Test-Path $destino) {
        Write-Host "    - $destino ya existe; NO se sobrescribe."
    } else {
        Copy-Item $ejemplo $destino
        Write-Host "    - Creado $destino (EDÍTALO con tus valores reales)."
    }
}

# Lee un archivo .env (CLAVE=VALOR) y lo aplica como variables de entorno del servicio NSSM.
function Aplicar-EnvANssm($servicio, $envPath) {
    $pairs = @()
    foreach ($linea in Get-Content $envPath) {
        $t = $linea.Trim()
        if ($t -eq "" -or $t.StartsWith("#")) { continue }
        $idx = $t.IndexOf("=")
        if ($idx -lt 1) { continue }
        $k = $t.Substring(0, $idx).Trim()
        $v = $t.Substring($idx + 1).Trim()
        $pairs += "$k=$v"
    }
    # NSSM AppEnvironmentExtra admite múltiples pares separados por salto de línea.
    nssm set $servicio AppEnvironmentExtra ($pairs -join "`r`n") | Out-Null
}

Require-Admin

Write-Host "==> 1/4  Preparando archivos de entorno (.env)..."
if (-not (Test-Path $ApiDir)) { throw "No existe $ApiDir. Copia primero los binarios publicados." }
if (-not (Test-Path $WebDir)) { throw "No existe $WebDir. Copia primero los binarios publicados." }
Copiar-EnvSiFalta (Join-Path $RepoDir "deploy\cobranza-api.env.example") (Join-Path $ApiDir ".env")
Copiar-EnvSiFalta (Join-Path $RepoDir "deploy\cobranza-web.env.example") (Join-Path $WebDir ".env")

Write-Host "==> 2/4  Registrando servicio cobranza-api..."
nssm install cobranza-api $DotnetExe (Join-Path $ApiDir "SistemaCobranzaApp.Api.dll") | Out-Null
nssm set cobranza-api AppDirectory $ApiDir | Out-Null
nssm set cobranza-api DisplayName "Sistema Cobranza API" | Out-Null
nssm set cobranza-api Start SERVICE_AUTO_START | Out-Null
Aplicar-EnvANssm "cobranza-api" (Join-Path $ApiDir ".env")

Write-Host "==> 3/4  Registrando servicio cobranza-web..."
nssm install cobranza-web $DotnetExe (Join-Path $WebDir "SistemaCobranzaApp.Web.dll") | Out-Null
nssm set cobranza-web AppDirectory $WebDir | Out-Null
nssm set cobranza-web DisplayName "Sistema Cobranza Web" | Out-Null
nssm set cobranza-web Start SERVICE_AUTO_START | Out-Null
Aplicar-EnvANssm "cobranza-web" (Join-Path $WebDir ".env")

Write-Host "==> 4/4  Arrancando servicios..."
nssm start cobranza-api | Out-Null
nssm start cobranza-web | Out-Null

Write-Host ""
Write-Host "==> Listo. Estado:"
nssm status cobranza-api
nssm status cobranza-web
Write-Host ""
Write-Host "IMPORTANTE (primera vez): edita los secretos y reaplica el entorno:"
Write-Host "   notepad $ApiDir\.env      # contraseña BD + Jwt__Key"
Write-Host "   notepad $WebDir\.env      # ApiSettings__BaseUrl"
Write-Host "   powershell -ExecutionPolicy Bypass -File deploy\instalar-servidor.ps1   # re-aplica .env"
Write-Host "   (o reinicia: nssm restart cobranza-api ; nssm restart cobranza-web)"
Write-Host ""
Write-Host "Pruebas locales:"
Write-Host "   curl http://127.0.0.1:8081/api/empresa"
Write-Host "   curl -I http://127.0.0.1:8080"
