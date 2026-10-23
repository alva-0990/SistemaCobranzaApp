using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class VendedoresEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var operativas = app.MapGroup("/api/vendedores").RequireAuthorization();

        // Solo vendedores activos (para selectores/combos comunes).
        operativas.MapGet("/", async (long? idZona, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Vendedores Operativos";
            log.Info(proceso, "Consultando vendedores activos.");
            var lista = await s.ListarActivasAsync(idZona, ct);
            log.Info(proceso, $"Vendedores activos obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Vendedores activos obtenidos.");
        });

        var admin = app.MapGroup("/api/admin/vendedores").RequireAuthorization();

        admin.MapGet("/", async (bool? incluirInactivos, long? idZona, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Vendedores (Admin)";
            log.Info(proceso, "Consultando vendedores.");
            var lista = await s.ListarAsync(incluirInactivos, idZona, ct);
            log.Info(proceso, $"Vendedores obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Vendedores obtenidos.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Vendedor";
            log.Info(proceso, $"Buscando vendedor #{id}.");
            var x = await s.ObtenerAsync(id, ct);
            if (x is null)
            {
                log.Warn(proceso, $"Vendedor #{id} no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el vendedor #{id}.");
            }
            log.Info(proceso, $"Vendedor #{id} obtenido.", datos: x);
            return Respuestas.Ok(x, "Vendedor obtenido.");
        });

        admin.MapPost("/", async (CrearVendedorRequest req, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Vendedor";
            log.Info(proceso, $"Creando vendedor: '{req.Nombre}'.");
            var r = await s.CrearAsync(req.Nombre, req.IdZona, ct, req.IdUsuario);
            if (r.Exito)
            {
                log.Info(proceso, $"Vendedor creado #{r.Vendedor!.IdVendedor} ('{r.Vendedor.Nombre}').", datos: r.Vendedor);
                return Respuestas.Creado($"/api/admin/vendedores/{r.Vendedor.IdVendedor}", r.Vendedor, "Vendedor creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el vendedor: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear el vendedor.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarVendedorRequest req, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Vendedor";
            log.Info(proceso, $"Actualizando vendedor #{id} a '{req.Nombre}'.");
            var r = await s.ActualizarAsync(id, req.Nombre, req.IdZona, req.Activo, ct, req.IdUsuario);
            if (r.Exito)
            {
                log.Info(proceso, $"Vendedor #{id} actualizado.", datos: r.Vendedor);
                return Respuestas.Ok(r.Vendedor!, "Vendedor actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Vendedor #{id} no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el vendedor #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar el vendedor #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo actualizar el vendedor.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioVendedoresAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Vendedor";
            log.Info(proceso, $"Dando de baja vendedor #{id}.");
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Vendedor #{id} dado de baja.", datos: r.Vendedor);
                return Respuestas.Ok(r.Vendedor!, "Vendedor dado de baja correctamente.");
            }
            log.Warn(proceso, $"Vendedor #{id} no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el vendedor #{id}.");
        });
    }
}
