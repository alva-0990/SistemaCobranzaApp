# Respaldos de PostgreSQL

Scripts en `scripts/respaldo/`:

| Script | Qué hace |
|---|---|
| `respaldar.sh` | `pg_dump` (formato custom, comprimido) de `sistema_cobranza` y `evolution`, verifica que el archivo se pueda leer, genera `.sha256`, respalda `evolution_instances/` (sesión de WhatsApp) y borra respaldos con más de 14 días. |
| `probar-restauracion.sh` | Restaura el último respaldo en una base **temporal** (nunca toca la real), valida checksum, tablas y conteos (`clientes`, `venta`, `pagos`, `usuarios`) y la elimina. |

Probados contra el contenedor `cobranza_postgres`: respaldo y restauración exitosos (42 tablas; conteos idénticos a la base viva).

## Uso manual

```bash
./scripts/respaldo/respaldar.sh
./scripts/respaldo/probar-restauracion.sh          # usa el respaldo más reciente
```

Variables opcionales: `CONTENEDOR` (def. `cobranza_postgres`; vacío = Postgres local, requiere `PGPASSWORD`/`PGHOST`),
`PGUSER`, `BASES`, `DIR_RESPALDOS` (def. `./respaldos`), `RETENCION_DIAS` (def. 14), `INCLUIR_INSTANCIAS` (def. 1).

## Automatizar (Linux, cron del servidor)

```cron
# Respaldo diario 02:00 y prueba de restauración el día 1 de cada mes 03:00
0 2 * * *  cd /ruta/SistemaCobranzaApp && DIR_RESPALDOS=/var/backups/cobranza ./scripts/respaldo/respaldar.sh >> /var/log/cobranza-respaldo.log 2>&1
0 3 1 * *  cd /ruta/SistemaCobranzaApp && DIR_RESPALDOS=/var/backups/cobranza ./scripts/respaldo/probar-restauracion.sh >> /var/log/cobranza-respaldo.log 2>&1
```

## IMPORTANTE: copia fuera del servidor

Un respaldo en el mismo disco no protege contra falla del disco, robo o ransomware. Copia `DIR_RESPALDOS` a otro lugar,
por ejemplo con `rclone` (Google Drive, S3, Backblaze B2…) o `rsync` a otra máquina:

```bash
rclone copy /var/backups/cobranza remoto:cobranza-respaldos --max-age 24h
```

## Restaurar en una emergencia

```bash
# 1) Detener la API para que no escriba durante la restauración
docker compose stop api web
# 2) Recrear la base y restaurar
docker exec -i cobranza_postgres psql -U cobranza -d postgres -c "DROP DATABASE sistema_cobranza;" -c "CREATE DATABASE sistema_cobranza;"
docker exec -i cobranza_postgres pg_restore -U cobranza -d sistema_cobranza --no-owner < respaldos/sistema_cobranza_AAAAMMDD_HHMMSS.dump
# 3) Levantar de nuevo
docker compose start api web
```

El respaldo de `evolution_instances_*.tar.gz` se descomprime en la raíz del proyecto para recuperar la sesión de WhatsApp sin re-escanear el QR.
Los respaldos contienen datos reales de clientes: protege la carpeta (permisos 700) y no la subas a git (`respaldos/` ya está en `.gitignore`).
