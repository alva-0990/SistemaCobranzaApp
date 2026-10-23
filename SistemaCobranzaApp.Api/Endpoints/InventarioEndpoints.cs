using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Inventario;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class InventarioEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // ===================== Grupo: Operaciones de Inventario =====================
        var inventario = app.MapGroup("/api/admin/inventario").RequireAuthorization();

        // Existencias central
        inventario.MapGet("/central", async (ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Consultar Existencias Central";
            log.Info(proceso, "Consultando stock actual en la bodega central.");
            var lista = await s.ExistenciasCentralAsync(ct);
            log.Info(proceso, $"Existencias de central obtenidas. Total artículos: {lista.Count}.");
            return Respuestas.Ok(lista, "Existencias de central obtenidas.");
        });

        // Existencias de una zona
        inventario.MapGet("/zona/{idZona:long}", async (long idZona, ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Consultar Existencias Zona";
            log.Info(proceso, $"Consultando stock actual de la zona #{idZona}.");
            var lista = await s.ExistenciasZonaAsync(idZona, ct);
            log.Info(proceso, $"Existencias de zona #{idZona} obtenidas. Total artículos: {lista.Count}.");
            return Respuestas.Ok(lista, $"Existencias de la zona #{idZona} obtenidas.");
        });

        // Entrada/ajuste central
        inventario.MapPost("/central/movimiento", async (MovimientoCentralRequest req, ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Movimiento Inventario Central";
            string tipoAccion = req.EsAjuste ? "Ajuste físico" : "Entrada/Salida";
            log.Info(proceso, $"Procesando {tipoAccion} en central para producto #{req.IdProducto}. Cantidad: {req.Cantidad}.");
            
            var r = await s.MovimientoCentralAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Movimiento en central exitoso. Folio: #{r.Movimiento!.IdMovimientoInv}.", datos: r.Movimiento);
                return Respuestas.Ok(r.Movimiento, "Movimiento de inventario central registrado correctamente.");
            }

            log.Warn(proceso, $"Error al registrar movimiento en central: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo registrar el movimiento central.", r.Error ?? "");
        });

        // Entrada/ajuste zona
        inventario.MapPost("/zona/movimiento", async (MovimientoZonaRequest req, ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Movimiento Inventario Zona";
            string tipoAccion = req.EsAjuste ? "Ajuste físico" : "Entrada/Salida";
            log.Info(proceso, $"Procesando {tipoAccion} en zona #{req.IdZona} para producto #{req.IdProducto}. Cantidad: {req.Cantidad}.");

            var r = await s.MovimientoZonaAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Movimiento en zona #{req.IdZona} exitoso. Folio: #{r.Movimiento!.IdMovimientoInv}.", datos: r.Movimiento);
                return Respuestas.Ok(r.Movimiento, "Movimiento de inventario de zona registrado correctamente.");
            }

            log.Warn(proceso, $"Error al registrar movimiento en zona #{req.IdZona}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo registrar el movimiento de la zona.", r.Error ?? "");
        });

        // Historial de movimientos de un producto (Tu consulta inmutable)
        inventario.MapGet("/producto/{idProducto:long}/movimientos", async (long idProducto, int? max, ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Historial de Movimientos";
            int limite = max ?? 100;
            log.Info(proceso, $"Consultando historial para el producto #{idProducto} (Límite: {limite}).");
            
            var lista = await s.HistorialAsync(idProducto, limite, ct);
            log.Info(proceso, $"Historial obtenido para producto #{idProducto}. Registros encontrados: {lista.Count}.");
            return Respuestas.Ok(lista, "Historial de movimientos obtenido correctamente.");
        });

        // Historial global de movimientos (vista con filtros: tipo, zona, fechas)
        inventario.MapGet("/movimientos", async (
            string? tipos, long? idZona, DateTimeOffset? desde, DateTimeOffset? hasta, int? max,
            ServicioInventario s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Historial Global de Inventario";
            var listaTipos = string.IsNullOrWhiteSpace(tipos)
                ? null
                : tipos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            log.Info(proceso, $"Consultando historial global. Tipos: '{tipos ?? "TODOS"}', Zona: {idZona?.ToString() ?? "TODAS"}, " +
                              $"Desde: {desde?.ToString() ?? "-"}, Hasta: {hasta?.ToString() ?? "-"}.");

            var lista = await s.HistorialGlobalAsync(listaTipos, idZona, desde, hasta, max ?? 500, ct);
            log.Info(proceso, $"Historial global obtenido. Registros: {lista.Count}.");
            return Respuestas.Ok(lista, "Historial de inventario obtenido correctamente.");
        });


        // ===================== Grupo: Propuestas de Envío =====================
        var propuestas = app.MapGroup("/api/admin/propuestas").RequireAuthorization();

        // Listar propuestas con filtros
        propuestas.MapGet("/", async (string? estatus, long? idZona, ServicioPropuestas s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Propuestas";
            log.Info(proceso, $"Consultando propuestas. Filtros -> Estatus: '{estatus ?? "TODOS"}', Zona: {idZona?.ToString() ?? "TODAS"}.");
            
            var lista = await s.ListarAsync(estatus, idZona, ct);
            log.Info(proceso, $"Propuestas obtenidas. Cantidad: {lista.Count}.");
            return Respuestas.Ok(lista, "Listado de propuestas obtenido.");
        });

        // Detalle de una propuesta por ID
        propuestas.MapGet("/{id:long}", async (long id, ServicioPropuestas s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Propuesta";
            log.Info(proceso, $"Buscando propuesta de envío #{id}.");
            
            var p = await s.ObtenerAsync(id, ct);
            if (p is null)
            {
                log.Warn(proceso, $"Propuesta #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la propuesta #{id}.");
            }

            log.Info(proceso, $"Propuesta #{id} obtenida correctamente.", datos: p);
            return Respuestas.Ok(p, "Propuesta obtenida correctamente.");
        });

        // Crear una propuesta (Estatus PENDIENTE)
        propuestas.MapPost("/", async (CrearPropuestaRequest req, ServicioPropuestas s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Crear Propuesta Envío";
            log.Info(proceso, $"Registrando nueva propuesta de envío hacia la zona #{req.IdZonaDestino}.");
            
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Propuesta creada #{r.Propuesta!.IdPropuesta} para zona #{req.IdZonaDestino}.", datos: r.Propuesta);
                return Respuestas.Creado($"/api/propuestas/{r.Propuesta.IdPropuesta}", r.Propuesta, "Propuesta de envío creada correctamente.");
            }

            log.Warn(proceso, $"No se pudo crear la propuesta de envío: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo crear la propuesta.", r.Error ?? "");
        });

        // Confirmar propuesta (Transaccional: afecta Central y Zona)
        propuestas.MapPost("/{id:long}/confirmar", async (long id, ResolverPropuestaRequest req, ServicioPropuestas s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Confirmar Propuesta Envío";
            log.Info(proceso, $"Procesando confirmación/cierre de la propuesta #{id} por el usuario #{req.IdUsuarioConfirma}.");
            
            var r = await s.ConfirmarAsync(id, req.IdUsuarioConfirma, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Propuesta #{id} confirmada y stock transferido correctamente.", datos: r.Propuesta);
                return Respuestas.Ok(r.Propuesta!, "Propuesta de envío procesada y aplicada al inventario con éxito.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Propuesta #{id} no encontrada para confirmar.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la propuesta #{id}.");
            }

            log.Warn(proceso, $"No se pudo confirmar la propuesta #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo completar la transferencia de la propuesta.", r.Error ?? "");
        });

        // Rechazar propuesta (marca RECHAZADA; no toca inventario)
        propuestas.MapPost("/{id:long}/rechazar", async (long id, ResolverPropuestaRequest req, ServicioPropuestas s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Rechazar Propuesta Envío";
            log.Info(proceso, $"Procesando rechazo de la propuesta #{id} por el usuario #{req.IdUsuarioConfirma}.");

            var r = await s.RechazarAsync(id, req.IdUsuarioConfirma, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Propuesta #{id} rechazada correctamente.", datos: r.Propuesta);
                return Respuestas.Ok(r.Propuesta!, "Propuesta de envío rechazada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Propuesta #{id} no encontrada para rechazar.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la propuesta #{id}.");
            }

            log.Warn(proceso, $"No se pudo rechazar la propuesta #{id}: {r.Error}");
            return Respuestas.SolicitudInvalida("No se pudo rechazar la propuesta.", r.Error ?? "");
        });
    }
}
