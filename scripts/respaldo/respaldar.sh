#!/usr/bin/env bash
# Respaldo de PostgreSQL (CobrAgil). Genera dumps comprimidos, los verifica y aplica retención.
#
# Uso:            ./scripts/respaldo/respaldar.sh
# Configuración:  variables de entorno (todas opcionales)
#   CONTENEDOR       contenedor de Postgres en Docker        (def: cobranza_postgres)
#                    Vacío => usa pg_dump local (necesita PGHOST/PGPASSWORD/pg_dump instalado)
#   PGUSER           usuario de Postgres                     (def: cobranza)
#   BASES            bases a respaldar, separadas por espacio (def: "sistema_cobranza evolution")
#   DIR_RESPALDOS    carpeta destino                         (def: <repo>/respaldos)
#   RETENCION_DIAS   días que se conservan                   (def: 14)
#   INCLUIR_INSTANCIAS  1 = respalda también ./evolution_instances (sesión WhatsApp) (def: 1)
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENEDOR="${CONTENEDOR-cobranza_postgres}"
PGUSER="${PGUSER:-cobranza}"
BASES="${BASES:-sistema_cobranza evolution}"
DIR_RESPALDOS="${DIR_RESPALDOS:-$RAIZ/respaldos}"
RETENCION_DIAS="${RETENCION_DIAS:-14}"
INCLUIR_INSTANCIAS="${INCLUIR_INSTANCIAS:-1}"
SELLO="$(date +%Y%m%d_%H%M%S)"

log() { echo "[$(date '+%F %T')] $*"; }

# Ejecuta pg_dump / pg_restore dentro del contenedor o localmente.
pg() {
  if [[ -n "$CONTENEDOR" ]]; then docker exec -i "$CONTENEDOR" "$@"; else "$@"; fi
}

mkdir -p "$DIR_RESPALDOS"
chmod 700 "$DIR_RESPALDOS"
errores=0

for base in $BASES; do
  destino="$DIR_RESPALDOS/${base}_${SELLO}.dump"
  log "Respaldando '$base' -> $(basename "$destino")"
  # Se escribe a .tmp y solo se renombra si el dump termina bien (nunca queda un respaldo a medias).
  if pg pg_dump -U "$PGUSER" -d "$base" -Fc --no-owner > "$destino.tmp"; then
    # Verificación: el archivo debe poder leerse (lista su contenido).
    if pg pg_restore --list < "$destino.tmp" > /dev/null; then
      mv "$destino.tmp" "$destino"
      ( cd "$DIR_RESPALDOS" && shasum -a 256 "$(basename "$destino")" > "$(basename "$destino").sha256" )
      log "OK '$base' ($(du -h "$destino" | cut -f1))"
    else
      log "ERROR: el respaldo de '$base' no es legible."; rm -f "$destino.tmp"; errores=$((errores+1))
    fi
  else
    log "ERROR: pg_dump falló para '$base'."; rm -f "$destino.tmp"; errores=$((errores+1))
  fi
done

if [[ "$INCLUIR_INSTANCIAS" == "1" && -d "$RAIZ/evolution_instances" ]]; then
  tar -czf "$DIR_RESPALDOS/evolution_instances_${SELLO}.tar.gz" -C "$RAIZ" evolution_instances \
    && log "OK evolution_instances" || { log "ERROR respaldando evolution_instances"; errores=$((errores+1)); }
fi

# Retención: borra respaldos más viejos que RETENCION_DIAS (solo si no hubo errores, para no quedarnos sin nada).
if [[ $errores -eq 0 ]]; then
  find "$DIR_RESPALDOS" -maxdepth 1 -type f \( -name '*.dump' -o -name '*.sha256' -o -name '*.tar.gz' \) \
    -mtime +"$RETENCION_DIAS" -print -delete | sed 's/^/  borrado: /' || true
fi

if [[ $errores -gt 0 ]]; then log "Terminó con $errores error(es)."; exit 1; fi
log "Respaldo completo en $DIR_RESPALDOS"
