using System.Security.Claims;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Cobranza;
using SistemaCobranzaApp.Infrastructure.Ventas;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class VentaCreditoEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        // Obtiene el IdUsuario autenticado desde los claims del token JWT (NameIdentifier / sub).
        static long? ObtenerIdUsuario(ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            return long.TryParse(raw, out var id) ? id : null;
        }

        // ===========================================================================
        // GRUPO DE ENDPOINTS OPERATIVOS Y ADMINISTRATIVOS (Módulo de Crédito y Cobro)
        // ===========================================================================
        var api = app.MapGroup("/api/admin/ventas").RequireAuthorization(); // 🎯 Actualizado a tu grupo admin

        // 1. Crear venta a crédito con su respectiva tabla de amortización unificada
        api.MapPost("/", async (CrearVentaRequest req, ClaimsPrincipal user, ServicioVentasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Venta a Crédito (Admin)";
            var idUsuario = ObtenerIdUsuario(user);
            log.Info(proceso, $"Procesando venta para el cliente #{req.IdCliente} (usuario #{idUsuario?.ToString() ?? "desconocido"}).", datos: req);

            var r = await s.CrearAsync(req, idUsuario, ct);
            if (r.Exito)
            {
                return Respuestas.Creado($"/api/admin/ventas/{r.Venta!.IdVenta}", r.Venta, "Venta registrada administrativamente.");
            }
            return Respuestas.SolicitudInvalida("No se pudo registrar la venta.", r.Error ?? "");
        }).WithName("CrearVentaAdmin"); // 🎯 CORREGIDO: Nombre global único para Admin

        // 🚀 1b. [NUEVO] Renovar una cuenta absorbiendo el saldo de una venta vieja pendiente
        api.MapPost("/{idVentaVieja:long}/renovar", async (long idVentaVieja, CrearVentaRequest req, ClaimsPrincipal user, ServicioVentasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Renovación de Cuenta / Venta a Crédito (Admin)";
            var idUsuario = ObtenerIdUsuario(user);
            log.Info(proceso, $"Iniciando flujo de renovación. Absorbiendo venta vieja #{idVentaVieja} para dar de alta nueva cotización del cliente #{req.IdCliente} (usuario #{idUsuario?.ToString() ?? "desconocido"}).", datos: req);

            var r = await s.RenovarVentaAsync(idVentaVieja, req, idUsuario, ct);
            if (r.Exito)
            {
                return Respuestas.Creado($"/api/admin/ventas/{r.Venta!.IdVenta}", r.Venta, "Renovación completada. Cuenta vieja liquidada y saldo transferido a la nueva cuenta.");
            }
            return Respuestas.SolicitudInvalida("No se pudo procesar la renovación de la cuenta.", r.Error ?? "");
        }).WithName("RenovarVentaAdmin");

        // 1c. Editar una venta existente (solo ventas sin abonos). Reemplazo integral.
        api.MapPut("/{id:long}", async (long id, ActualizarVentaRequest req, ClaimsPrincipal user, ServicioVentasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Edición de Venta a Crédito (Admin)";
            var idUsuario = ObtenerIdUsuario(user);
            log.Info(proceso, $"Editando la venta #{id} (usuario #{idUsuario?.ToString() ?? "desconocido"}).", datos: req);

            var r = await s.ActualizarAsync(id, req, idUsuario, ct);
            if (r.Exito)
            {
                return Respuestas.Ok(r.Venta!, "Venta actualizada administrativamente.");
            }
            return Respuestas.SolicitudInvalida("No se pudo actualizar la venta.", r.Error ?? "");
        }).WithName("EditarVentaAdmin");

        // 2. Detalle completo e integral de una cuenta
        api.MapGet("/{id:long}", async (long id, ServicioVentasAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            var v = await s.ObtenerAsync(id, ct);
            return v is null ? Respuestas.NoEncontrado($"No se encontró la venta #{id}.") : Respuestas.Ok(v, "Venta obtuvo.");
        }).WithName("ObtenerVentaAdmin"); // 🎯 CORREGIDO: Nombre global único para Admin

        // 3. Cancelación lógica integral de una venta activa
        api.MapPost("/{id:long}/cancelar", async (long id, ClaimsPrincipal user, ServicioCancelaciones s, IServicioLog log, CancellationToken ct) =>
        {
            var idUsuario = ObtenerIdUsuario(user);
            var r = await s.CancelarVentaAsync(id, idUsuario, ct);

            if (r.Exito)
            {
                return Respuestas.Ok(r.Respuesta!, "Venta cancelada.");
            }

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? "")
                : Respuestas.SolicitudInvalida("Error", r.Error ?? "");
        }).WithName("CancelarVentaAdmin");
        
        // 4. Búsqueda inteligente de cuentas por folio
        api.MapGet("/buscar", async (string texto, ServicioBusqueda s, IServicioLog log, CancellationToken ct) =>
        {
            var v = await s.BuscarVentaAsync(texto, ct);
            return v is null ? Respuestas.NoEncontrado("No se encontró.") : Respuestas.Ok(v, "Localizada.");
        }).WithName("BuscarVentaCobranzaAdmin"); // 🎯 CORREGIDO: Nombre global único para Admin

        // 5. Consultar el estado de cuenta histórico consolidado de un cliente
        api.MapGet("/estado-cuenta/{idCliente:long}", async (long idCliente, ServicioEstadoCuenta s, IServicioLog log, CancellationToken ct) =>
        {
            var ec = await s.ObtenerAsync(idCliente, ct);
            return ec is null ? Respuestas.NoEncontrado("No se encontró.") : Respuestas.Ok(ec, "Obtenido.");
        }).WithName("ObtenerEstadoCuentaAdmin"); // 🎯 CORREGIDO: Nombre global único para Admin


        // ===========================================================================
        // MÓDULO ASOCIADO DE COBRANZA EN CALLE Y TRANSACCIONES DE EFECTIVO (Pagos)
        // ===========================================================================
        var pagos = app.MapGroup("/api/admin/pagos").RequireAuthorization(); // 🎯 Actualizado a tu grupo admin

        // 6. Registrar un abono en efectivo directo
        pagos.MapPost("/", async (RegistrarPagoRequest req, ServicioCobranza s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Registrar Abono (Admin)";
            var r = await s.RegistrarPagoAsync(req, ct);
            if (r.Exito) return Respuestas.Ok(r.Respuesta, "Abono procesado administrativamente.");
            return Respuestas.SolicitudInvalida("No se pudo registrar.", r.Error ?? "");
        }).WithName("RegistrarPagoAdmin"); // 🎯 CORREGIDO: Soluciona el error de crash duplicado 'RegistrarPago'

        // 7. El DELETE de /api/admin/pagos/{id} lo maneja PagosEndpoints (reversión en cascada
        //    vía fn_revertir_abono_cascada). Aquí NO se registra otro DELETE sobre la misma ruta
        //    para evitar AmbiguousMatchException ({id:long} vs {idPago:long}).

        // 8. Consultar el historial detallado de abonos aplicados a una cuenta
        pagos.MapGet("/historial/{idVenta:long}", async (long idVenta, ServicioBusqueda s, IServicioLog log, CancellationToken ct) =>
        {
            var lista = await s.HistorialPagosAsync(idVenta, ct);
            return Respuestas.Ok(lista, "Historial obtenido.");
        }).WithName("ObtenerHistorialPagosAdmin"); // 🎯 CORREGIDO: Nombre global único para Admin
    }
}