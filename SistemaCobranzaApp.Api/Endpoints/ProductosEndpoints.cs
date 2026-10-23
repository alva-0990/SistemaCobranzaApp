using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class ProductosEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var productos = app.MapGroup("/api/productos").RequireAuthorization();

        // Listado paginado con filtros (texto, incluirInactivos, pagina, tamanoPagina).
        productos.MapGet("/", async (string? texto, bool? incluirInactivos, int? pagina, int? tamanoPagina,
            ServicioProductosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Productos";
            log.Info(proceso, "Consultando productos.");
            var pag = await s.ListarAsync(texto, incluirInactivos, pagina, tamanoPagina, ct);
            log.Info(proceso, $"Productos obtenidos: {pag.Items.Count} (total {pag.Total}, página {pag.Pagina}).", datos: pag);
            return Respuestas.Ok(pag, "Productos obtenidos.");
        });

        productos.MapGet("/{id:long}", async (long id, ServicioProductosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Producto";
            log.Info(proceso, $"Buscando producto #{id}.");
            var p = await s.ObtenerAsync(id, ct);
            if (p is null)
            {
                log.Warn(proceso, $"Producto #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el producto #{id}.");
            }
            log.Info(proceso, $"Producto #{id} obtenido.", datos: p);
            return Respuestas.Ok(p, "Producto obtenido.");
        });

        productos.MapPost("/", async (CrearProductoRequest req, ServicioProductosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Producto";
            log.Info(proceso, $"Creando producto: '{req.CodigoClave}' - '{req.Descripcion}'.");
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Producto creado #{r.Producto!.Id} ('{r.Producto.CodigoClave}').", datos: r.Producto);
                return Respuestas.Creado($"/api/productos/{r.Producto.Id}", r.Producto, "Producto creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el producto: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear el producto.", r.Error ?? "");
        });

        productos.MapPut("/{id:long}", async (long id, ActualizarProductoRequest req, ServicioProductosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Producto";
            log.Info(proceso, $"Actualizando producto #{id} ('{req.CodigoClave}').");
            var r = await s.ActualizarAsync(id, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Producto #{id} actualizado.", datos: r.Producto);
                return Respuestas.Ok(r.Producto!, "Producto actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Producto #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el producto #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el producto #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar el producto.", r.Error ?? "");
        });

        productos.MapDelete("/{id:long}", async (long id, ServicioProductosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Producto";
            log.Info(proceso, $"Dando de baja producto #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Producto #{id} dado de baja.", datos: r.Producto);
                return Respuestas.Ok(r.Producto!, "Producto dado de baja correctamente.");
            }
            log.Warn(proceso, $"Producto #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el producto #{id}.");
        });
    }
}
