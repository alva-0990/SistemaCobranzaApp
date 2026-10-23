# Plan: Conectar el dashboard de Home.razor a datos reales

> **Estado:** ✅ Implementado. Este documento queda como referencia de la arquitectura y
> de las decisiones tomadas. Los builds de `SistemaCobranzaApp.Api` y
> `SistemaCobranzaApp.Web` compilan con 0 errores.

## Contexto

El archivo `SistemaCobranzaApp.Shared/Pages/Home.razor` (ruta `/`) tenía un **mockup** del
dashboard administrativo con datos ficticios. Se reemplazaron esos datos por datos reales del
backend, siguiendo la arquitectura existente del proyecto (endpoints en `ReportesEndpoints.cs`,
servicio `ServicioReportes`, DTOs en `Shared/Dtos/Reportes.cs`, cliente `ApiCliente`, y el patrón
de `Reportes.razor`).

**No se cambió el diseño visual aprobado** (KPIs, filtros colapsables, 3 gráficas, tabla
resumen); solo se alimentó con datos reales.

## Arquitectura seguida (igual que los demás reportes)

- Endpoint en `SistemaCobranzaApp.Api/Endpoints/ReportesEndpoints.cs` con `.RequireAuthorization()`.
- Lógica en `SistemaCobranzaApp.Infrastructure/Reportes/ServicioReportes.cs` (EF Core, `AsNoTracking`,
  zona horaria con `CalculoReportes.NormalizarRango` / `FechaProyecto`).
- DTOs en `SistemaCobranzaApp.Shared/Dtos/Reportes.cs`.
- Método en `SistemaCobranzaApp.Shared/Services/ApiCliente.cs` usando `GetReporteAsync<T>`.
- Consumo en `Home.razor` con el mismo estilo que `Reportes.razor`.

## 1. DTOs (en `Shared/Dtos/Reportes.cs`)

```csharp
public record DashboardFiltro(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    long? IdZona = null);

public record DashboardKpis(
    decimal VentasTotales,
    decimal TotalCobrado,
    decimal SaldoPorCobrar,
    decimal CobradoHoy,
    int CuentasConSaldo,
    int PagosHoy);

public record DashboardSerieZona(string Zona, decimal Monto);
public record DashboardSerieDia(DateTimeOffset Dia, decimal Monto);

public record DashboardResumenFila(
    string Zona,
    string? Vendedor,
    string? Cobrador,
    decimal TotalVendido,
    decimal TotalCobrado,
    decimal SaldoPendiente,
    decimal PorcentajeRecuperacion); // 0–100

public record DashboardResponse(
    DashboardKpis Kpis,
    IReadOnlyList<DashboardSerieZona> VentasPorZona,
    IReadOnlyList<DashboardSerieZona> CobranzaPorZona,
    IReadOnlyList<DashboardSerieDia> CobranzaPorDia,
    IReadOnlyList<DashboardResumenFila> Resumen);
```

## 2. Servicio (`ServicioReportes.DashboardAsync`)

`Task<DashboardResponse> DashboardAsync(DashboardFiltro f, CancellationToken ct = default)`:

- Normaliza el rango con `CalculoReportes.NormalizarRango(f.Desde, f.Hasta)`.
- **VentasTotales / TotalVendido**: suma de `Ventas.TotalFinal` en el rango (por `FechaCompra`),
  filtrando por zona vía `Cliente → Ruta → IdZona`, excluyendo canceladas
  (`IdEstatusVenta != EstatusVentaIds.Cancelada`).
- **TotalCobrado / CobranzaPorZona / CobranzaPorDia**: suma de `Pagos.MontoAbona` en el rango
  (por `FechaPago`), agrupada por zona (`Pago → Cliente → Ruta → Zona`) y por día
  (`FechaPago.EnZonaProyecto().Date`).
- **SaldoPorCobrar / CuentasConSaldo**: cartera vigente (ventas no canceladas con saldo > 0),
  reutilizando `CalculoCobranza.CalcularSaldoAnterior(TotalFinal, Enganche, TotalAbonado)`.
  Es independiente del rango de fechas.
- **CobradoHoy / PagosHoy**: pagos del día usando `FechaProyecto.InicioDiaUtc` / `FinDiaUtc`.
- **Resumen**: agrupa por zona (con vendedor/cobrador representativo) con total vendido, total
  cobrado, saldo y `% recuperación = TotalCobrado / TotalVendido * 100`
  (vía `CalculoReportes.PorcentajeRecuperacion`, protegido contra división por cero).
- Respeta el filtro `IdZona` cuando viene.
- Todo envuelto en try/catch; ante error devuelve una respuesta vacía coherente para no romper
  el panel.

> Nota: fue necesario agregar `using SistemaCobranzaApp.Domain.Comun;` en `ServicioReportes.cs`
> para `FechaProyecto`.

## 3. Endpoint (`ReportesEndpoints.cs`)

```csharp
app.MapGet("/api/reportes/dashboard",
        async (DateTimeOffset? desde, DateTimeOffset? hasta, long? idZona,
            ServicioReportes s, CancellationToken ct) =>
            Results.Ok(await s.DashboardAsync(new DashboardFiltro(desde, hasta, idZona), ct)))
    .RequireAuthorization();
```

## 4. Cliente (`ApiCliente.cs`, junto a `ReporteClientesAsync`)

```csharp
public Task<(DashboardResponse? datos, string? error)> DashboardAsync(
    DateTimeOffset? desde, DateTimeOffset? hasta, long? idZona = null, CancellationToken ct = default)
{
    var url = $"/api/reportes/dashboard?desde={Fecha(desde)}&hasta={Fecha(hasta)}";
    if (idZona is { } z) url += $"&idZona={z}";
    return GetReporteAsync<DashboardResponse>(url, ct);
}
```

## 5. Home.razor

- Guardas de sesión y redirección a `/login` en `OnInitializedAsync`
  (`CargarSesionDesdeAlmacenamientoAsync` + `Autenticado`), como en `Reportes.razor`.
- Carga de zonas reales con `ListarZonasOperativasAsync()` para el combo de filtro (reemplaza la
  lista `_zonas` ficticia). El `MudSelect` de Zona pasó a `T="long?"` con `ZonaDto`.
- `GenerarAsync()` (botón **Aplicar**): llama `ApiClienteService.DashboardAsync(_desde, _hasta, _idZona)`
  y mapea:
  - KPIs → textos con `.ToString("C2")` (más `% de recuperación` global, cuentas con saldo, pagos hoy).
  - `VentasPorZona` / `CobranzaPorZona` → `ChartSeries<double>` + `XAxisLabels` con nombres de zona
    (arreglos separados por si difiere el orden entre ventas y cobranza).
  - `CobranzaPorDia` → `ChartSeries<double>` + labels `dd/MM`.
  - `Resumen` → la `MudTable` con barra de progreso de `% recuperación`.
- Spinner (`MudProgressCircular`) en el encabezado mientras carga; errores vía `Snackbar`.
- Se mantuvo el diseño/estilos actuales (tarjetas KPI, `MudCollapse` de filtros, `boton-aplicar`,
  gráficas y tabla). Se eliminó la etiqueta "Datos de ejemplo (mockup)" y el `@code` ficticio.

## 6. Notas técnicas

- MudBlazor 9.10: `ChartSeries<double>` y `<MudChart T="double" ...>`. `ChartOptions` solo admite
  `ChartPalette`, `ShowLegend`, `ShowToolTips`, `TooltipTitleFormat`, `TooltipSubtitleFormat`
  (no hay `YAxisFormat` ni `LineStrokeWidth`). El formato de moneda en ejes se maneja en el tooltip
  o se omite.
- Zona horaria: siempre `FechaProyecto` / `EnZonaProyecto()` para agrupar por día.
- El analizador de MudBlazor emite advertencias cosméticas por `XAxisLabels` en `MudChart`
  ("Illegal Attribute ... LowerCase"), igual que en otras páginas del proyecto (`PanelClass` en
  `MudTabs`, etc.). No son errores y no afectan el build.
- Rutas intactas: el dashboard es `/` (Home), el reporte admin sigue en `/reportes`.

## Criterios de aceptación

- Al abrir `/`, el dashboard muestra KPIs, gráficas y tabla con datos reales del rango por defecto
  (últimos 30 días).
- El filtro por fechas y zona recalcula todo al presionar **Aplicar**.
- El diseño visual se mantiene idéntico al mockup aprobado.
- Builds de Api y Web con 0 errores.

## Archivos modificados

- `SistemaCobranzaApp.Shared/Dtos/Reportes.cs`
- `SistemaCobranzaApp.Infrastructure/Reportes/ServicioReportes.cs`
- `SistemaCobranzaApp.Api/Endpoints/ReportesEndpoints.cs`
- `SistemaCobranzaApp.Shared/Services/ApiCliente.cs`
- `SistemaCobranzaApp.Shared/Pages/Home.razor`

## Pendiente de verificación manual

La correctitud se verificó por compilación (Api 0/0, Web 0/0, solución `buildIsSuccess=true`) y
análisis estático. Falta una prueba end-to-end con la API corriendo y datos reales en la base de
datos para confirmar valores.
