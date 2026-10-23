using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Catalogos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class FrecuenciasPagoEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Ruta para selectores/combos generales de ventas a crédito (Requiere token JWT)
        var operativas = app.MapGroup("/api/frecuencias-pago").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Frecuencias Pago Operativas";
            log.Info(proceso, "Consultando frecuencias de pago activas.");
            var lista = await s.ListarActivasAsync(ct);
            log.Info(proceso, $"Frecuencias de pago activas obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Frecuencias de pago activas obtenidas.");
        });

        // Rutas para el panel de administración
        var admin = app.MapGroup("/api/admin/frecuencias-pago").RequireAuthorization();

        admin.MapGet("/", async (ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Frecuencias Pago (Admin)";
            log.Info(proceso, "Consultando todas las frecuencias de pago.");
            var lista = await s.ListarTodasAsync(ct);
            log.Info(proceso, $"Frecuencias de pago obtenidas: {lista.Count}.", datos: lista);
            return Respuestas.Ok(lista, "Frecuencias de pago obtenidas.");
        });

        admin.MapGet("/{id:long}", async (long id, ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Frecuencia Pago";
            log.Info(proceso, $"Buscando frecuencia de pago #{id}.", datos: new { id });
            var f = await s.ObtenerAsync(id, ct);
            if (f is null)
            {
                log.Warn(proceso, $"Frecuencia de pago #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la frecuencia de pago #{id}.");
            }
            log.Info(proceso, $"Frecuencia de pago #{id} obtenida.", datos: f);
            return Respuestas.Ok(f, "Frecuencia de pago obtenida.");
        });

        admin.MapPost("/", async (CrearCatalogoFrecuenciaPagoRequest req, ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Frecuencia Pago";
            log.Info(proceso, $"Creando frecuencia de pago: '{req.Descripcion}' con {req.DiasPeriodo} días.", datos: req);
            var r = await s.CrearAsync(req.Descripcion, req.DiasPeriodo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Frecuencia de pago creada #{r.FrecuenciaPago!.Id} ('{r.FrecuenciaPago.Descripcion}').", datos: r.FrecuenciaPago);
                return Respuestas.Creado($"/api/admin/frecuencias-pago/{r.FrecuenciaPago.Id}", r.FrecuenciaPago, "Frecuencia de pago creada correctamente.");
            }
            log.Warn(proceso, $"No se pudo crear la frecuencia de pago: {r.Error}", datos: req);
            return Respuestas.SolicitudInvalida("No se pudo crear la frecuencia de pago.", r.Error ?? "");
        });

        admin.MapPut("/{id:long}", async (long id, ActualizarCatalogoFrecuenciaPagoRequest req, ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Frecuencia Pago";
            log.Info(proceso, $"Actualizando frecuencia de pago #{id} a '{req.Descripcion}' con {req.DiasPeriodo} días.", datos: new { id, req.Descripcion, req.DiasPeriodo });
            var r = await s.ActualizarAsync(id, req.Descripcion, req.DiasPeriodo, req.Activo, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Frecuencia de pago #{id} actualizada.", datos: r.FrecuenciaPago);
                return Respuestas.Ok(r.FrecuenciaPago!, "Frecuencia de pago actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Frecuencia de pago #{id} no encontrada.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la frecuencia de pago #{id}.");
            }
            log.Warn(proceso, $"No se pudo actualizar la frecuencia de pago #{id}: {r.Error}", datos: new { id, req.Descripcion });
            return Respuestas.SolicitudInvalida("No se pudo actualizar la frecuencia de pago.", r.Error ?? "");
        });

        admin.MapDelete("/{id:long}", async (long id, ServicioFrecuenciasPagoAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Baja de Frecuencia Pago";
            log.Info(proceso, $"Dando de baja frecuencia de pago #{id}.", datos: new { id });
            var r = await s.DarDeBajaAsync(id, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Frecuencia de pago #{id} dada de baja.", datos: r.FrecuenciaPago);
                return Respuestas.Ok(r.FrecuenciaPago!, "Frecuencia de pago dada de baja correctamente.");
            }
            log.Warn(proceso, $"Frecuencia de pago #{id} no encontrada.");
            return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la frecuencia de pago #{id}.");
        });
    }
}
