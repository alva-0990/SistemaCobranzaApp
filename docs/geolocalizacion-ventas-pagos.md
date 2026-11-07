# Geolocalización de Ventas y Pagos

Auditoría de ubicación para venta a domicilio y cobranza en campo: se guarda la
latitud/longitud del dispositivo al **registrar una venta** y al **registrar un pago**.

El trabajo se dividió en dos fases:

- **Fase A — Persistencia (COMPLETADA y verificada).** El sistema acepta y guarda
  `Latitud`/`Longitud` en venta y pago si se las envían. Tolerante a `null`.
- **Fase B — Captura del GPS en el dispositivo (IMPLEMENTADA; pendiente de prueba en Android).**
  Se construyó la captura del GPS y se conectó a los 4 diálogos existentes de venta/pago
  (online y offline). No verificable en este entorno (el proyecto MAUI requiere el workload
  `maui-android`); debe probarse en un dispositivo/emulador Android real.

---

## FASE A — Cambios realizados

Modelo de datos: dos columnas nullable por tabla, mismo molde que ya usa `Cliente`.
Tipos: `Latitud NUMERIC(10,8)`, `Longitud NUMERIC(11,8)`. Nombres de columna: `latitud`, `longitud`.

### 1. Entidades de dominio
- `SistemaCobranzaApp.Domain/Entidades/Ventas.cs` → clase `Venta`: se agregaron
  `public decimal? Latitud { get; set; }` y `public decimal? Longitud { get; set; }`.
- `SistemaCobranzaApp.Domain/Entidades/Pago.cs` → clase `Pago`: mismas dos propiedades.

### 2. Mapeo EF Core
- `SistemaCobranzaApp.Infrastructure/Persistencia/CobranzaDbContext.cs`
  - Bloque `b.Entity<Venta>` (tabla `venta`): mapeo de `Latitud`→`latitud` `numeric(10,8)` y
    `Longitud`→`longitud` `numeric(11,8)`.
  - Bloque `b.Entity<Pago>` (tabla `pagos`): idéntico mapeo.

### 3. DTOs de request
- `SistemaCobranzaApp.Shared/Dtos/VentasAdmin.cs` → `CrearVentaRequest`: se agregaron
  `Latitud`/`Longitud` como **init-props** (retrocompatible; no cambia parámetros posicionales).
- `SistemaCobranzaApp.Shared/Dtos/Cobranza.cs` → `RegistrarPagoRequest`: se agregaron
  `decimal? Latitud = null, decimal? Longitud = null` como **parámetros opcionales al final**.
  Esto NO rompe ningún call-site existente (Pagos.razor.cs, ReversionPagos.razor.cs,
  ServicioJornadaLocal.cs, tests).

### 4. Servicios (persistencia online)
- `SistemaCobranzaApp.Infrastructure/Ventas/ServicioVentasAdmin.cs` → al construir `new Venta { ... }`
  se asignan `Latitud = req.Latitud`, `Longitud = req.Longitud`.
- `SistemaCobranzaApp.Infrastructure/Cobranza/ServicioCobranza.cs`:
  - Pago principal (abono): `Latitud = req.Latitud`, `Longitud = req.Longitud`.
  - Pago de condonación por acuerdo (AJUSTE automático): **hereda** las coordenadas del abono
    que lo originó, para que ambos registros queden georreferenciados igual.

### 5. Base de datos
- `db/init/01_schema.sql`:
  - Tabla `Pagos`: `Latitud`/`Longitud` ya estaban (edición previa del usuario).
  - Tabla `Venta`: se agregaron `Latitud NUMERIC(10,8)` y `Longitud NUMERIC(11,8)`.
- **BD existente de desarrollo**: como `01_schema.sql` solo corre al crear una BD nueva, se
  aplicó `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` sobre `venta` y `pagos` para que la BD
  actual tenga las 4 columnas. **En producción / nuevas instalaciones el schema ya es correcto.**

```sql
-- Aplicado a la BD de desarrollo (idempotente):
ALTER TABLE venta ADD COLUMN IF NOT EXISTS latitud  NUMERIC(10, 8);
ALTER TABLE venta ADD COLUMN IF NOT EXISTS longitud NUMERIC(11, 8);
ALTER TABLE pagos ADD COLUMN IF NOT EXISTS latitud  NUMERIC(10, 8);
ALTER TABLE pagos ADD COLUMN IF NOT EXISTS longitud NUMERIC(11, 8);
```

### Índices
No se agregaron índices sobre `latitud`/`longitud`. El uso previsto es auditoría (ver el punto
de una venta/pago concreto o listarlo junto con el registro), que se resuelve con los índices
existentes por `IdVenta`, `IdPago`, `IdCobrador`, `FechaPago`. Un B-tree sobre lat/long solo
serviría para búsquedas por rango de coordenadas; para "cercanía" real se necesitaría PostGIS.
Recomendación: no agregar índice salvo que aparezca un caso de búsqueda geográfica concreto.

### Verificación realizada
- Compilación **0 errores** en: Domain, Shared, Infrastructure, Api, Web.
  (El proyecto MAUI `SistemaCobranzaApp` no compila en este entorno por falta del workload
  `maui-android`; no se modificó y no está relacionado con estos cambios.)
- BD: las 4 columnas existen con tipo exacto `numeric(10,8)` / `numeric(11,8)` y nombres
  `latitud` / `longitud`, coincidiendo con el mapeo EF (evita fallo en el `ReloadAsync` posterior
  al INSERT del pago).
- Prueba de escritura/lectura: `UPDATE` con coordenadas reales (18.46278900, -97.39283400)
  aceptado en ambas tablas; el `SELECT` que EF ejecuta funciona.

### No modificado (intencional) — se abordará en Fase B
- La **captura del GPS no existe aún**: los requests hoy mandan `Latitud=null`, `Longitud=null`.
  Por eso venta y pago guardarán `null` hasta completar la Fase B.
- UI de venta/pago (online y offline) y el flujo de sincronización offline: no requieren cambios
  de esquema (el almacenamiento offline serializa el request completo a JSON), pero sí deben
  **poblar** las coordenadas una vez exista la captura GPS.

---

## FASE B — Captura del GPS (IMPLEMENTADA; pendiente de prueba en Android)

Objetivo cumplido: se obtiene la ubicación del dispositivo al confirmar una venta o un pago y se
pasa en el request, tanto **online** como **offline**. Es best-effort: si no hay permiso/señal,
se registra con lat/long en `null` (soportado por la Fase A).

> IMPORTANTE: NO se crearon diálogos nuevos. Los diálogos de venta/pago (online y offline) ya
> existían; solo se les **agregó** la captura de GPS. Lo único nuevo son los 3 archivos del
> servicio de ubicación.

### B.1 Permisos Android — HECHO
- `SistemaCobranzaApp/Platforms/Android/AndroidManifest.xml`: se agregaron
  `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION` y `<uses-feature gps required="false">`.
- El permiso en runtime lo solicita el propio servicio de ubicación (ver B.2).

### B.2 Servicio de ubicación — HECHO (archivos nuevos)
- `SistemaCobranzaApp.Shared/Services/IServicioUbicacion.cs`: abstracción + `record struct Ubicacion(decimal Latitud, decimal Longitud)`;
  `Task<Ubicacion?> ObtenerActualAsync(CancellationToken)`.
- `SistemaCobranzaApp/Services/ServicioUbicacionMaui.cs` (MAUI): solicita `Permissions.LocationWhenInUse`
  y obtiene el fix con `Geolocation.GetLastKnownLocationAsync()` y, si no hay, `GetLocationAsync(Medium, 8s)`.
  Redondea a 8 decimales. `try/catch` → devuelve `null` ante permiso denegado, sin señal o timeout.
  Registrado en `MauiProgram.cs` (global, fuera del `#if ANDROID`).
- `SistemaCobranzaApp.Web/Services/ServicioUbicacionWeb.cs` (Web): no-op, devuelve `null`.
  Registrado en `SistemaCobranzaApp.Web/Program.cs`.

### B.3 Inyección en los 4 diálogos existentes — HECHO
- `DialogoNuevaVenta.razor` y `DialogoNuevaVentaOffline.razor`: `@inject IServicioUbicacion`;
  `ProcesarEstructuraVenta` pasó de `void` a `async Task`; captura la ubicación y la pasa como
  init-props `Latitud`/`Longitud` en el `CrearVentaRequest` que ya existía.
- `ResultadoDialogoPago.cs`: se agregaron init-props `Latitud`/`Longitud`.
- `DialogoRegistrarPago.razor` y `DialogoRegistrarPagoOffline.razor`: `@inject IServicioUbicacion`;
  en `Guardar()` capturan la ubicación y la ponen en el `ResultadoDialogoPago`.
- `Pagos.razor.cs` (online): las 3 construcciones de `RegistrarPagoRequest` (abono, liquidación por
  plan, liquidación por acuerdo) pasan `Latitud: i.Latitud, Longitud: i.Longitud`.
- `PagosOffline.razor.cs` + `ServicioJornadaLocal.cs`: `GuardarPagoAsync` /
  `GuardarLiquidacionPlanAsync` / `GuardarLiquidacionAcuerdoAsync` y `RegistrarCobroAsync` reciben
  `decimal? latitud, longitud` (opcionales) y los pasan al `RegistrarPagoRequest`.

### B.4 Offline — sin cambios de esquema
- El almacenamiento offline (SQLite, `AlmacenJornadaAndroid`) guarda el request como JSON; lat/long
  se serializan y sincronizan automáticamente al traerlas el request.
- Sincronización (`ServicioJornada.VenderAsync` / `CobrarAsync`): usan `with { ... }` que NO reescriben
  lat/long, así que se preservan. (Verificado en el análisis previo.)

### B.5 Pendiente de verificación
- **No se pudo compilar ni probar el proyecto MAUI** en este entorno (falta el workload
  `maui-android`). Los proyectos Shared, Infrastructure, Api y Web compilan con 0 errores.
- Debe probarse en un **dispositivo/emulador Android**: conceder permiso, registrar una venta y un
  pago, y confirmar que `venta.latitud/longitud` y `pagos.latitud/longitud` se guardan con las
  coordenadas reales (y quedan `null` si se niega el permiso).

---

## Riesgos identificados y su mitigación
Fase A:
1. `RegistrarPagoRequest` es record posicional → se usaron **parámetros opcionales al final**
   para no romper call-sites. (Mitigado.)
2. `ReloadAsync` tras insertar el pago lee las columnas mapeadas → se verificó que los nombres
   `latitud`/`longitud` y tipos coinciden con la BD. (Mitigado.)
3. `Venta` no tenía las columnas en la BD existente → se aplicó `ALTER TABLE`. (Mitigado.)
4. Precisión decimal → se fijó `HasColumnType("numeric(10,8)" / "(11,8)")` imitando a `Cliente`.
   (Mitigado.)

Fase B:
5. El servicio de ubicación MAUI no se pudo compilar aquí → se replicó exactamente el patrón de
   `IPermisosCamara`/`PermisosCamaraAndroid` y se usan API estándar de MAUI
   (`Microsoft.Maui.Devices.Sensors.Geolocation`, `Microsoft.Maui.ApplicationModel.Permissions`).
   Riesgo residual: requiere prueba en dispositivo. (Parcialmente mitigado.)
6. GPS best-effort: nunca bloquea el registro; ante fallo devuelve `null`. (Mitigado por diseño.)
