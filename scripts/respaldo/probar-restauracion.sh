#!/usr/bin/env bash
# PRUEBA DE RESTAURACIÓN: restaura el respaldo más reciente de la base principal en una base
# TEMPORAL (nunca toca la base real), valida que tenga datos y la elimina.
# Un respaldo que nunca se ha restaurado no es un respaldo: ejecútalo al menos una vez al mes.
#
# Uso:  ./scripts/respaldo/probar-restauracion.sh [archivo.dump]
# Variables: CONTENEDOR, PGUSER, DIR_RESPALDOS, BASE (def: sistema_cobranza)  -- igual que respaldar.sh
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENEDOR="${CONTENEDOR-cobranza_postgres}"
PGUSER="${PGUSER:-cobranza}"
BASE="${BASE:-sistema_cobranza}"
DIR_RESPALDOS="${DIR_RESPALDOS:-$RAIZ/respaldos}"
TEMP="prueba_restauracion_$$"

log() { echo "[$(date '+%F %T')] $*"; }
pg() { if [[ -n "$CONTENEDOR" ]]; then docker exec -i "$CONTENEDOR" "$@"; else "$@"; fi; }
sql() { pg psql -U "$PGUSER" -d "$1" -tAc "$2"; }

ARCHIVO="${1:-$(ls -1t "$DIR_RESPALDOS/${BASE}_"*.dump 2>/dev/null | head -n1 || true)}"
[[ -n "$ARCHIVO" && -f "$ARCHIVO" ]] || { log "ERROR: no hay respaldo de '$BASE' en $DIR_RESPALDOS"; exit 1; }

# 1) Integridad del archivo contra su checksum.
if [[ -f "$ARCHIVO.sha256" ]]; then
  ( cd "$(dirname "$ARCHIVO")" && shasum -a 256 -c "$(basename "$ARCHIVO").sha256" ) \
    || { log "ERROR: el checksum no coincide (archivo dañado)."; exit 1; }
fi

limpiar() { sql postgres "DROP DATABASE IF EXISTS \"$TEMP\";" >/dev/null 2>&1 || true; }
trap limpiar EXIT

log "Restaurando $(basename "$ARCHIVO") en base temporal '$TEMP'..."
sql postgres "CREATE DATABASE \"$TEMP\";" > /dev/null
pg pg_restore -U "$PGUSER" -d "$TEMP" --no-owner --exit-on-error < "$ARCHIVO"

# 2) Validaciones: tablas presentes y datos.
tablas=$(sql "$TEMP" "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';")
log "Tablas restauradas: $tablas"
[[ "$tablas" -gt 0 ]] || { log "ERROR: la restauración no creó tablas."; exit 1; }

for t in clientes venta pagos usuarios; do
  existe=$(sql "$TEMP" "SELECT to_regclass('public.$t') IS NOT NULL;")
  if [[ "$existe" == "t" ]]; then
    n_restaurado=$(sql "$TEMP" "SELECT count(*) FROM $t;")
    n_actual=$(sql "$BASE" "SELECT count(*) FROM $t;" 2>/dev/null || echo "?")
    log "  $t: restaurado=$n_restaurado | base actual=$n_actual"
  else
    log "  $t: (no existe en este respaldo)"
  fi
done

log "PRUEBA DE RESTAURACIÓN EXITOSA: $(basename "$ARCHIVO")"
