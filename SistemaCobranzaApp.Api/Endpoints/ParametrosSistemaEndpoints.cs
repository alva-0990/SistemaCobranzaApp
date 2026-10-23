using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints del módulo de parámetros del sistema (tabla ParametrosSistema).
///   - Operativos (/api/parametros): lectura de parámetros (p. ej. para resolver promociones en pantalla).
///   - Admin (/api/admin/parametros): CRUD de administración (ServicioParametrosAdmin).
/// La clave es la llave primaria (texto), por lo que las rutas por elemento usan {clave}.
/// </summary>
public class ParametrosSistemaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ===================== Operativos (consulta) =====================
        var operativas = app.MapGroup("/api/parametros").RequireAuthorization();

        // Listado completo de parámetros (lectura).
        operativas.MapGet("/", async (ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Parámetros (Operativo)";
            log.Info(proceso, "Consultando parámetros del sistema.");
            var lista = await s.ListarTodosAsync(ct);
            log.Info(proceso, $"Parámetros obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Parámetros obtenidos.");
        });

        // Obtener un parámetro por clave (lectura; útil para resolver promociones en pantalla).
        operativas.MapGet("/{clave}", async (string clave, ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Parámetro (Operativo)";
            log.Info(proceso, $"Consultando parámetro '{clave}'.", datos: new { clave });
            var p = await s.ObtenerAsync(clave, ct);
            return p is null
                ? Respuestas.NoEncontrado($"No se encontró el parámetro '{clave}'.")
                : Respuestas.Ok(p, "Parámetro obtenido.");
        });

        // ===================== Admin (CRUD) =====================
        var admin = app.MapGroup("/api/admin/parametros").RequireAuthorization();

        // Listado completo.
        admin.MapGet("/", async (ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Parámetros (Admin)";
            log.Info(proceso, "Consultando todos los parámetros del sistema.");
            var lista = await s.ListarTodosAsync(ct);
            log.Info(proceso, $"Parámetros obtenidos: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Parámetros obtenidos.");
        });

        // Obtener por clave.
        admin.MapGet("/{clave}", async (string clave, ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Parámetro (Admin)";
            log.Info(proceso, $"Buscando parámetro '{clave}'.", datos: new { clave });
            var p = await s.ObtenerAsync(clave, ct);
            if (p is null)
            {
                log.Warn(proceso, $"Parámetro '{clave}' no encontrado.");
                return Respuestas.NoEncontrado($"No se encontró el parámetro '{clave}'.");
            }
            log.Info(proceso, $"Parámetro '{clave}' obtenido.", datos: p);
            return Respuestas.Ok(p, "Parámetro obtenido.");
        });

        // Crear.
        admin.MapPost("/", async (CrearParametroSistemaRequest req, ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Parámetro";
            log.Info(proceso, $"Creando parámetro '{req.Clave}'.", datos: req);
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Parámetro '{r.Parametro!.Clave}' creado.", datos: r.Parametro);
                return Respuestas.Creado($"/api/admin/parametros/{r.Parametro.Clave}", r.Parametro, "Parámetro creado correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear el parámetro: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear el parámetro.", r.Error ?? "");
        });

        // Actualizar.
        admin.MapPut("/{clave}", async (string clave, ActualizarParametroSistemaRequest req, ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Parámetro";
            log.Info(proceso, $"Actualizando parámetro '{clave}'.", datos: new { clave, req });
            var r = await s.ActualizarAsync(clave, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Parámetro '{clave}' actualizado.", datos: r.Parametro);
                return Respuestas.Ok(r.Parametro!, "Parámetro actualizado correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Parámetro '{clave}' no encontrado.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el parámetro '{clave}'.");
            }
            log.Warn(proceso, $"No se pudo actualizar el parámetro '{clave}': {r.Error}", datos: new { clave });
            return Respuestas.SolicitudInvalida("No se pudo actualizar el parámetro.", r.Error ?? "");
        });

        // Eliminar (borrado físico).
        admin.MapDelete("/{clave}", async (string clave, ServicioParametrosAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Parámetro";
            log.Info(proceso, $"Eliminando parámetro '{clave}'.", datos: new { clave });
            var r = await s.EliminarAsync(clave, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Parámetro '{clave}' eliminado.", datos: r.Parametro);
                return Respuestas.Ok(r.Parametro!, "Parámetro eliminado correctamente.");
            }
            log.Warn(proceso, $"Parámetro '{clave}' no encontrado.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el parámetro '{clave}'.");
        });
    }
}
