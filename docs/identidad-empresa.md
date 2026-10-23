# Identidad de empresa

El login, el encabezado y el pie muestran el nombre comercial de la API; el encabezado móvil utiliza el nombre corto. Sin empresa se muestra «Nombre comercial» y el icono fijo de cartera configurado en `AppTheme`. La identidad se conserva localmente para iniciar sin conexión. El login consulta `/api/empresa/identidad`, que expone solo nombres y logos; `/api/empresa` conserva su autorización para los demás datos.

En **Empresa → Editar empresa**, seleccionar un logo normal y uno para fondos oscuros. PNG, JPEG o WebP, hasta 256 KB por imagen. Se guardan como data URI Base64 en `logo_url` y `logo_blanco_url`, sin carpeta de archivos adicional. También se pueden conservar URLs existentes. Los cambios se guardan al pulsar Guardar.

En bases existentes, ejecutar primero `db/init/09_empresa_logos.sql`. Los scripts de inicialización de Docker solo se ejecutan al crear un volumen nuevo; no es necesario recrear ni borrar la base actual.

## Nombre e icono de la APK

La APK utiliza el nombre fijo «Sistema de Cobranza» y el icono fijo `Resources/AppIcon/appicon.svg`. El manifiesto Android referencia `@mipmap/appicon` y `@mipmap/appicon_round`, generados por MAUI a partir de ese archivo. No consulta la empresa durante la compilación.

Dentro de la aplicación se conservan el nombre y los logos de la empresa. Cuando no hay logo, el login utiliza el icono `AppTheme.IconoLogin`.
