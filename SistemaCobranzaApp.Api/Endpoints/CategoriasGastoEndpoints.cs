using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints administrativos para el catálogo de categorías de gasto.
/// Todas las operaciones requieren autenticación.
/// </summary>
public class CategoriasGastoEndpoints : IEndpointModule
{
    private static CatalogoCategoriaGastoDto MapearDto(CatalogoCategoriaGasto c) =>
        new(
            c.IdCategoriaGasto,
            c.Nombre,
            c.Descripcion,
            c.RequiereComprobante,
            c.Estatus == 1);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app
            .MapGroup("/api/admin/categorias-gasto")
            .RequireAuthorization();

        // ============================================================
        // LISTAR CATEGORÍAS
        // GET /api/admin/categorias-gasto?incluirInactivas=false
        // ============================================================
        admin.MapGet("/", async (
            bool incluirInactivas,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Listar Categorías de Gasto";

            try
            {
                log.Info(proceso, "Consultando categorías de gasto.",
                    datos: new { incluirInactivas });

                var lista = await s.ListarAsync(incluirInactivas, ct);
                var dto = lista.Select(MapearDto).ToList();

                return Respuestas.Ok(dto, "Categorías de gasto obtenidas correctamente.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex);
                return Respuestas.ErrorInterno(
                    "No fue posible consultar las categorías de gasto.");
            }
        }).WithName("ListarCategoriasGasto");

        // ============================================================
        // OBTENER CATEGORÍA POR ID
        // GET /api/admin/categorias-gasto/{id}
        // ============================================================
        admin.MapGet("/{id:long}", async (
            long id,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Obtener Categoría de Gasto";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo consultar la categoría.",
                    "El identificador debe ser mayor que cero.");

            try
            {
                var categoria = await s.ObtenerAsync(id, ct);

                return categoria is null
                    ? Respuestas.NoEncontrado($"No se encontró la categoría #{id}.")
                    : Respuestas.Ok(MapearDto(categoria), "Categoría obtenida correctamente.");
            }
            catch (Exception ex)
            {
                log.Error(proceso, ex, datos: new { id });
                return Respuestas.ErrorInterno("No fue posible consultar la categoría.");
            }
        }).WithName("ObtenerCategoriaGasto");

        // ============================================================
        // CREAR CATEGORÍA
        // POST /api/admin/categorias-gasto
        // ============================================================
        admin.MapPost("/", async (
            CrearCatalogoCategoriaGastoRequest req,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Crear Categoría de Gasto";

            log.Info(proceso, "Creando categoría de gasto.", datos: req);

            var r = await s.CrearAsync(
                req.Nombre,
                req.Descripcion,
                req.RequiereComprobante,
                ct);

            if (r.Exito)
                return Respuestas.Creado(
                    $"/api/admin/categorias-gasto/{r.Datos!.IdCategoriaGasto}",
                    MapearDto(r.Datos),
                    "Categoría de gasto creada correctamente.");

            return Respuestas.SolicitudInvalida(
                "No se pudo crear la categoría de gasto.",
                r.Error ?? "");
        }).WithName("CrearCategoriaGasto");

        // ============================================================
        // ACTUALIZAR CATEGORÍA
        // PUT /api/admin/categorias-gasto/{id}
        // ============================================================
        admin.MapPut("/{id:long}", async (
            long id,
            ActualizarCatalogoCategoriaGastoRequest req,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Actualizar Categoría de Gasto";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo actualizar la categoría.",
                    "El identificador debe ser mayor que cero.");

            log.Info(proceso, $"Actualizando categoría #{id}.", datos: req);

            var r = await s.ActualizarAsync(
                id,
                req.Nombre,
                req.Descripcion,
                req.RequiereComprobante,
                req.Activo,
                ct);

            if (r.Exito)
                return Respuestas.Ok(MapearDto(r.Datos!), "Categoría actualizada correctamente.");

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró la categoría #{id}.")
                : Respuestas.SolicitudInvalida("No se pudo actualizar la categoría.", r.Error ?? "");
        }).WithName("ActualizarCategoriaGasto");

        // ============================================================
        // CAMBIAR ESTATUS (ACTIVAR / DESACTIVAR)
        // PATCH /api/admin/categorias-gasto/{id}/estatus
        // ============================================================
        admin.MapPatch("/{id:long}/estatus", async (
            long id,
            CambiarEstatusCategoriaGastoRequest req,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Cambiar Estatus de Categoría de Gasto";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo actualizar la categoría.",
                    "El identificador debe ser mayor que cero.");

            log.Info(proceso, $"Cambiando estatus de la categoría #{id}.",
                datos: new { id, req.Activo });

            var r = await s.CambiarEstatusAsync(id, req.Activo, ct);

            if (r.Exito)
                return Respuestas.Ok(MapearDto(r.Datos!),
                    req.Activo
                        ? "Categoría activada correctamente."
                        : "Categoría desactivada correctamente.");

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró la categoría #{id}.")
                : Respuestas.SolicitudInvalida(
                    "No se pudo actualizar el estatus de la categoría.", r.Error ?? "");
        }).WithName("CambiarEstatusCategoriaGasto");

        // ============================================================
        // DAR DE BAJA (baja lógica)
        // DELETE /api/admin/categorias-gasto/{id}
        // ============================================================
        admin.MapDelete("/{id:long}", async (
            long id,
            ServicioCategoriasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Dar de Baja Categoría de Gasto";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo dar de baja la categoría.",
                    "El identificador debe ser mayor que cero.");

            log.Info(proceso, $"Dando de baja la categoría #{id}.", datos: new { id });

            var r = await s.DarDeBajaAsync(id, ct);

            if (r.Exito)
                return Respuestas.Ok(MapearDto(r.Datos!), "Categoría dada de baja correctamente.");

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró la categoría #{id}.")
                : Respuestas.SolicitudInvalida("No se pudo dar de baja la categoría.", r.Error ?? "");
        }).WithName("DarDeBajaCategoriaGasto");
    }
}
