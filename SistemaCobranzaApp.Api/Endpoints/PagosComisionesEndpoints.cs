using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.CatalogoComisiones;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class PagosComisionesEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/pagos-comisiones").RequireAuthorization();

        // GET: Caja (Disponibles)
        grupo.MapGet("", async (long? idVendedor, long? idZona, DateTime? fechaDesde, DateTime? fechaHasta, 
            ServicioPagosComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Comisiones Disponibles";
            var filtros = new FiltroComisionesRequest(idVendedor, idZona, fechaDesde, fechaHasta);
            log.Info(proceso, "Consultando comisiones disponibles.", datos: filtros);
            
            var lista = await s.ListarDisponiblesAsync(filtros, ct); 
            return Respuestas.Ok(lista, "Comisiones obtenidas.");
        });

        // GET: Liberar Créditos (Pendientes)
        grupo.MapGet("pendientes-liberar", async (long? idVendedor, long? idZona, DateTime? fechaDesde, DateTime? fechaHasta, 
            ServicioPagosComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Comisiones Pendientes";
            var filtros = new FiltroComisionesRequest(idVendedor, idZona, fechaDesde, fechaHasta);
            log.Info(proceso, "Consultando comisiones pendientes.", datos: filtros);
            
            var lista = await s.ListarPendientesLiberarAsync(filtros, ct); 
            return Respuestas.Ok(lista, "Comisiones pendientes obtenidas.");
        });

        // GET: Historial (Pagadas)
        grupo.MapGet("historial", async (long? idVendedor, long? idZona, DateTime? fechaDesde, DateTime? fechaHasta, 
            ServicioPagosComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Listar Historial Pagadas";
            var filtros = new FiltroComisionesRequest(idVendedor, idZona, fechaDesde, fechaHasta);
            log.Info(proceso, "Consultando historial de pagos.", datos: filtros);
            
            var lista = await s.ListarHistorialAsync(filtros, ct); 
            return Respuestas.Ok(lista, "Historial obtenido.");
        });

        // POST: Procesar la liberación manual
        grupo.MapPost("liberar", async (ProcesarPagoComisionesRequest req, ServicioPagosComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Liberar Comisiones Manual";
            int totalReg = req.IdsComisiones is null ? 0 : req.IdsComisiones.Count;
            log.Info(proceso, $"Procesando solicitud para {totalReg} registros.", datos: req);

            var r = await s.LiberarComisionesManualAsync(req, ct); 
            if (r.Exito) return Respuestas.Ok(r.RegistrosProcesados, "Comisiones liberadas correctamente.");
            return Respuestas.SolicitudInvalida("No se pudieron liberar las comisiones.", r.Error ?? "");
        });

        // POST: Procesar el pago definitivo de caja
        grupo.MapPost("procesar", async (ProcesarPagoComisionesRequest req, ServicioPagosComisionesAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Procesar Pago Manual de Comisiones";
            int totalReg = req.IdsComisiones is null ? 0 : req.IdsComisiones.Count;
            log.Info(proceso, $"Iniciando pago manual para {totalReg} registros.", datos: req);
            
            var r = await s.PagarComisionesAsync(req, idUsuarioPago: 1, ct: ct); 
            if (r.Exito) return Respuestas.Ok(r.RegistrosProcesados, "Comisiones pagadas correctamente.");
            return Respuestas.SolicitudInvalida("No se pudo procesar el pago.", r.Error ?? "");
        });
    }
}