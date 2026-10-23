using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class CatalogosProductosEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ---- Operativas (Rutas rápidas para combos en el frontend) ----
        var operativas = app.MapGroup("/api/catalogos-productos").RequireAuthorization();

        operativas.MapGet("/categorias", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarCategoriasActivasAsync(ct)));

        operativas.MapGet("/marcas", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarMarcasActivasAsync(ct)));

        operativas.MapGet("/colores", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarColoresActivosAsync(ct)));

        operativas.MapGet("/tallas", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarTallasActivasAsync(ct)));


        // ---- Panel de Administración Completo (Mismo estándar de todos tus catálogos) ----
        var admin = app.MapGroup("/api/admin/catalogos-productos").RequireAuthorization();
        
                // ==========================================
        // 1. CATEGORÍAS
        // ==========================================
        admin.MapGet("/categorias", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarCategoriasTodasAsync(ct), "Categorías obtenidas."));

        admin.MapGet("/categorias/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            var res = await s.ObtenerCategoriaAsync(id, ct);
            return res is null ? Respuestas.NoEncontrado($"No se encontró la categoría #{id}.") : Respuestas.Ok(res, "Categoría obtenida.");
        });

        admin.MapPost("/categorias", async (CrearCategoriaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Alta de Categoría";
            log.Info(proceso, $"Creando categoría: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearCategoriaAsync(req.Descripcion, ct);
            return r.Exito ? Respuestas.Creado($"/api/admin/catalogos-productos/categorias/{r.Categoria!.Id}", r.Categoria, "Categoría creada correctamente.") : Respuestas.SolicitudInvalida("No se pudo crear.", r.Error ?? "");
        });

        admin.MapPut("/categorias/{id:long}", async (long id, ActualizarCategoriaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Actualización de Categoría";
            log.Info(proceso, $"Actualizando categoría #{id}.", datos: req);
            var r = await s.ActualizarCategoriaAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito) return Respuestas.Ok(r.Categoria!, "Categoría actualizada correctamente.");
            return r.NoEncontrado ? Respuestas.NoEncontrado(r.Error ?? "") : Respuestas.SolicitudInvalida("No se pudo actualizar.", r.Error ?? "");
        });

        admin.MapDelete("/categorias/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Baja de Categoría"; log.Info(proceso, $"Dando de baja categoría #{id}.");
            var r = await s.DarDeBajaCategoriaAsync(id, ct);
            return r.Exito ? Respuestas.Ok(r.Categoria!, "Categoría dada de baja correctamente.") : Respuestas.NoEncontrado(r.Error ?? "");
        });

        // ==========================================
        // 2. MARCAS
        // ==========================================
        admin.MapGet("/marcas", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarMarcasTodasAsync(ct), "Marcas obtenidas."));

        admin.MapGet("/marcas/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            var res = await s.ObtenerMarcaAsync(id, ct);
            return res is null ? Respuestas.NoEncontrado($"No se encontró la marca #{id}.") : Respuestas.Ok(res, "Marca obtenida.");
        });

        admin.MapPost("/marcas", async (CrearMarcaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Alta de Marca"; log.Info(proceso, $"Creando marca: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearMarcaAsync(req.Descripcion, ct);
            return r.Exito ? Respuestas.Creado($"/api/admin/catalogos-productos/marcas/{r.Marca!.Id}", r.Marca, "Marca creada correctamente.") : Respuestas.SolicitudInvalida("No se pudo crear.", r.Error ?? "");
        });

        admin.MapPut("/marcas/{id:long}", async (long id, ActualizarMarcaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Actualización de Marca"; log.Info(proceso, $"Actualizando marca #{id}.", datos: req);
            var r = await s.ActualizarMarcaAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito) return Respuestas.Ok(r.Marca!, "Marca actualizada correctamente.");
            return r.NoEncontrado ? Respuestas.NoEncontrado(r.Error ?? "") : Respuestas.SolicitudInvalida("No se pudo actualizar.", r.Error ?? "");
        });

        admin.MapDelete("/marcas/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Baja de Marca"; log.Info(proceso, $"Dando de baja marca #{id}.");
            var r = await s.DarDeBajaMarcaAsync(id, ct);
            return r.Exito ? Respuestas.Ok(r.Marca!, "Marca dada de baja correctamente.") : Respuestas.NoEncontrado(r.Error ?? "");
        });
    
            // ==========================================
        // 3. COLORES
        // ==========================================
        admin.MapGet("/colores", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarColoresTodosAsync(ct), "Colores obtenidos."));

        admin.MapGet("/colores/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            var res = await s.ObtenerColorAsync(id, ct);
            return res is null ? Respuestas.NoEncontrado($"No se encontró el color #{id}.") : Respuestas.Ok(res, "Color obtenido.");
        });

        admin.MapPost("/colores", async (CrearColorRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Alta de Color"; log.Info(proceso, $"Creando color: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearColorAsync(req.Descripcion, ct);
            return r.Exito ? Respuestas.Creado($"/api/admin/catalogos-productos/colores/{r.Color!.Id}", r.Color, "Color creado correctamente.") : Respuestas.SolicitudInvalida("No se pudo crear.", r.Error ?? "");
        });

        admin.MapPut("/colores/{id:long}", async (long id, ActualizarColorRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Actualización de Color"; log.Info(proceso, $"Actualizando color #{id}.", datos: req);
            var r = await s.ActualizarColorAsync(id, req.Descripcion, req.Activo, ct);
            if (r.Exito) return Respuestas.Ok(r.Color!, "Color actualizado correctamente.");
            return r.NoEncontrado ? Respuestas.NoEncontrado(r.Error ?? "") : Respuestas.SolicitudInvalida("No se pudo actualizar.", r.Error ?? "");
        });

        admin.MapDelete("/colores/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Baja de Color"; log.Info(proceso, $"Dando de baja color #{id}.");
            var r = await s.DarDeBajaColorAsync(id, ct);
            return r.Exito ? Respuestas.Ok(r.Color!, "Color dado de baja correctamente.") : Respuestas.NoEncontrado(r.Error ?? "");
        });

        // ==========================================
        // 4. TALLAS
        // ==========================================
        admin.MapGet("/tallas", async (ServicioCatalogosProductosAdmin s, CancellationToken ct) => 
            Respuestas.Ok(await s.ListarTallasTodasAsync(ct), "Tallas obtenidas."));

        admin.MapGet("/tallas/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            var res = await s.ObtenerTallaAsync(id, ct);
            return res is null ? Respuestas.NoEncontrado($"No se encontró la talla #{id}.") : Respuestas.Ok(res, "Talla obtuvo correctamente.");
        });

        admin.MapPost("/tallas", async (CrearTallaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Alta de Talla"; log.Info(proceso, $"Creando talla: '{req.Descripcion}'.", datos: req);
            var r = await s.CrearTallaAsync(req, ct);
            return r.Exito ? Respuestas.Creado($"/api/admin/catalogos-productos/tallas/{r.Talla!.Id}", r.Talla, "Talla creada correctamente.") : Respuestas.SolicitudInvalida("No se pudo crear.", r.Error ?? "");
        });

        admin.MapPut("/tallas/{id:long}", async (long id, ActualizarTallaRequest req, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Actualización de Talla"; log.Info(proceso, $"Actualizando talla #{id}.", datos: req);
            var r = await s.ActualizarTallaAsync(id, req, ct);
            if (r.Exito) return Respuestas.Ok(r.Talla!, "Talla actualizada correctamente.");
            return r.NoEncontrado ? Respuestas.NoEncontrado(r.Error ?? "") : Respuestas.SolicitudInvalida("No se pudo actualizar.", r.Error ?? "");
        });

        admin.MapDelete("/tallas/{id:long}", async (long id, ServicioCatalogosProductosAdmin s, IServicioLog log, CancellationToken ct) => {
            const string proceso = "Baja de Talla"; log.Info(proceso, $"Dando de baja talla #{id}.");
            var r = await s.DarDeBajaTallaAsync(id, ct);
            return r.Exito ? Respuestas.Ok(r.Talla!, "Talla dada de baja correctamente.") : Respuestas.NoEncontrado(r.Error ?? "");
        });
    }
}

