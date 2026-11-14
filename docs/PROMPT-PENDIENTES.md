# Prompt de desarrollo: pendientes del sistema CobrAgil

Copia el bloque de abajo como prompt para continuar el desarrollo. Contexto: ver `docs/RESUMEN-FUNCIONAMIENTO.md`.

---

```text
Eres un desarrollador senior en el repo SistemaCobranzaApp_v1 (Blazor Shared + MAUI Android + Blazor Server Web,
API minimal REST con JWT, EF Core + PostgreSQL, modo offline con SQLite en Android).

Reglas de trabajo:
- Lee el código existente antes de escribir y respeta el estilo (español, MudBlazor, DTOs en Shared/Dtos, servicios en Infrastructure).
- Toda pantalla nueva debe funcionar en Web, Android en línea y Android offline cuando aplique (hay páginas *Offline.razor).
- Cambios de BD: crea un script idempotente en db/migraciones/AAAA-MM-DD_nombre.sql Y actualiza db/init/01_schema.sql.
- Mapas: OpenStreetMap (gratis, sin API key); componentes existentes: BotonMapa, DialogoMapaCliente, INavegadorMapa.
- Ubicación del dispositivo: IServicioUbicacion (best-effort, nunca bloquea el flujo).
- Al terminar cada tarea: compila, corre lint/pruebas y resume qué se verificó y qué no.

Ya implementado: mapa/“Llévame ahí” en cliente, ventas y pagos; GPS automático al alta de cliente; respaldos de Postgres
(scripts/respaldo, deploy/GUIA-RESPALDOS.md).

Desarrolla en este orden (una tarea a la vez, pidiendo confirmación antes de pasar a la siguiente):

1. ACTUALIZAR UBICACIÓN DEL CLIENTE
   Botón “Actualizar ubicación aquí” en DetalleCliente y DetalleClienteOffline: toma el GPS actual, confirma con el usuario
   y guarda lat/lng del cliente (endpoint de actualización + operación offline que se sincronice). Sirve para corregir pines
   y llenar clientes existentes sin coordenadas.

2. MAPA DE RUTA DEL DÍA (cobrador/vendedor)
   Pantalla con mapa de OSM (Leaflet vía JS interop o similar) con los clientes con saldo de su ruta/zona, pines por color
   según atraso/clasificación, lista ordenada por cercanía y botón “Llévame ahí” por cliente. Debe funcionar offline con la
   jornada descargada (los tiles requieren internet; mostrar lista si no hay).

3. VALIDACIÓN PAGO vs DOMICILIO
   Calcular distancia (Haversine) entre la ubicación del pago y la del domicilio del cliente; marcar pagos a más de N metros
   (parámetro en ParametrosSistema, p. ej. PAGO_DISTANCIA_MAX_METROS) y mostrar la alerta en historial de pagos y reportes.

4. PROMESAS DE PAGO
   Registrar promesa (cliente/venta, fecha, monto, nota), lista de promesas del día y vencidas, y marcar cumplida/incumplida
   automáticamente al registrarse un pago.

5. RECORDATORIOS AUTOMÁTICOS POR WHATSAPP
   Usar la cola ColaMensajesWhatsapp (tipo_origen RECORDATORIO) y las plantillas por etapa de atraso ya sembradas en
   02_seed.sql. Worker que encole según días de atraso, sin duplicar envíos, respetando baja (BAJA) y parámetros de activación.

6. CORTE DE CAJA DEL COBRADOR
   Cierre de jornada: total cobrado por movimiento, monto entregado, diferencia, firma/observaciones, historial de cortes y
   reporte para el administrador.

7. BITÁCORA DE AUDITORÍA
   Registrar quién/cuándo/desde dónde (IP, dispositivo, coordenadas) en reversiones, condonaciones, ajustes, ediciones y bajas
   de clientes, cambios de parámetros y roles. Pantalla de consulta con filtros (solo Administrador).

8. CONFIABILIDAD
   a) Migraciones versionadas: tabla de control y aplicación automática de db/migraciones al desplegar.
   b) Pruebas automáticas (carpeta tests/) de saldos, cuotas, comisiones, racha de pagos y sincronización offline.
   c) Endpoint /health (API + BD) y alertas al administrador si falla respaldo, worker de WhatsApp o sincronización.

9. EXTRAS (si hay tiempo)
   Foto de evidencia en pagos/entregas; notificaciones push; modo oscuro/letra grande; mapa de calor de ventas y cobranza;
   proyección de flujo de efectivo; scoring de riesgo por historial de puntualidad.

Para cada tarea entrega: diseño breve (tablas/DTOs/endpoints/pantallas), implementación, script de migración si aplica,
y lista de verificación manual (Web, Android en línea, Android offline).
```
