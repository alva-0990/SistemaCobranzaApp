using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

/// <summary>
/// Endpoints administrativos para gestionar cuentas de gastos.
/// Todas las operaciones requieren autenticación.
/// </summary>
public class CuentasGastoEndpoints : IEndpointModule
{
    private static CuentaGastoDto MapearDto(CuentaGasto c, decimal saldoActual) =>
        new(
            c.IdCuentaGasto,
            c.Nombre,
            c.TipoCuenta,
            c.Moneda,
            c.SaldoInicial,
            saldoActual,
            c.Observaciones,
            c.Estatus == 1);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var admin = app
            .MapGroup("/api/admin/cuentas-gasto")
            .RequireAuthorization();

        // ============================================================
        // LISTAR CUENTAS DE GASTOS
        // GET /api/admin/cuentas-gasto?incluirInactivas=false
        // ============================================================
        admin.MapGet("/", async (
            bool incluirInactivas,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Listar Cuentas de Gasto";

            log.Info(proceso, "Consultando cuentas de gastos.",
                datos: new { incluirInactivas });

            var lista = await s.ListarAsync(incluirInactivas, ct);

            var dto = new List<CuentaGastoDto>(lista.Count);
            foreach (var cuenta in lista)
            {
                var saldo = await s.ObtenerSaldoAsync(cuenta.IdCuentaGasto, ct) ?? cuenta.SaldoInicial;
                dto.Add(MapearDto(cuenta, saldo));
            }

            log.Info(proceso, $"Cuentas obtenidas: {dto.Count}.",
                datos: new { incluirInactivas, cantidad = dto.Count });

            return Respuestas.Ok(dto, "Cuentas de gastos obtenidas correctamente.");
        })
        .WithName("ListarCuentasGasto");

        // ============================================================
        // OBTENER CUENTA POR ID
        // GET /api/admin/cuentas-gasto/{id}
        // ============================================================
        admin.MapGet("/{id:long}", async (
            long id,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Obtener Cuenta de Gasto";

            log.Info(proceso, $"Consultando cuenta de gasto #{id}.", datos: new { id });

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo consultar la cuenta.",
                    "El identificador debe ser mayor que cero.");

            var cuenta = await s.ObtenerAsync(id, ct);

            if (cuenta is null)
            {
                log.Warn(proceso, $"No se encontró la cuenta de gasto #{id}.");
                return Respuestas.NoEncontrado($"No se encontró la cuenta de gasto #{id}.");
            }

            var saldo = await s.ObtenerSaldoAsync(id, ct) ?? cuenta.SaldoInicial;

            return Respuestas.Ok(MapearDto(cuenta, saldo),
                "Cuenta de gasto obtenida correctamente.");
        })
        .WithName("ObtenerCuentaGasto");

        // ============================================================
        // CONSULTAR SALDO DE UNA CUENTA
        // GET /api/admin/cuentas-gasto/{id}/saldo
        // ============================================================
        admin.MapGet("/{id:long}/saldo", async (
            long id,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Consultar Saldo de Cuenta de Gasto";

            log.Info(proceso, $"Consultando saldo de la cuenta #{id}.", datos: new { id });

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo consultar el saldo.",
                    "El identificador debe ser mayor que cero.");

            var saldo = await s.ObtenerSaldoAsync(id, ct);

            if (saldo is null)
            {
                log.Warn(proceso, $"No se encontró la cuenta de gasto #{id}.");
                return Respuestas.NoEncontrado($"No se encontró la cuenta de gasto #{id}.");
            }

            return Respuestas.Ok(
                new { IdCuentaGasto = id, Saldo = saldo.Value },
                "Saldo de la cuenta obtenido correctamente.");
        })
        .WithName("ObtenerSaldoCuentaGasto");

        // ============================================================
        // CREAR CUENTA DE GASTOS
        // POST /api/admin/cuentas-gasto
        // ============================================================
        admin.MapPost("/", async (
            CrearCuentaGastoRequest req,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Crear Cuenta de Gasto";

            log.Info(proceso, "Iniciando creación de cuenta de gasto.",
                datos: new { req.Nombre, req.TipoCuenta, req.Moneda, req.SaldoInicial });

            var r = await s.CrearAsync(
                req.Nombre,
                req.TipoCuenta,
                req.Moneda,
                req.SaldoInicial,
                req.Observaciones,
                ct);

            if (r.Exito)
            {
                var dto = MapearDto(r.Datos!, r.Datos!.SaldoInicial);
                return Respuestas.Creado(
                    $"/api/admin/cuentas-gasto/{r.Datos.IdCuentaGasto}",
                    dto,
                    "Cuenta de gasto creada correctamente.");
            }

            log.Warn(proceso, $"No se pudo crear la cuenta de gasto: {r.Error}");

            return Respuestas.SolicitudInvalida(
                "No se pudo crear la cuenta de gasto.",
                r.Error ?? "Ocurrió un error al crear la cuenta.");
        })
        .WithName("CrearCuentaGasto");

        // ============================================================
        // ACTUALIZAR CUENTA DE GASTOS
        // PUT /api/admin/cuentas-gasto/{id}
        // ============================================================
        admin.MapPut("/{id:long}", async (
            long id,
            ActualizarCuentaGastoRequest req,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Actualizar Cuenta de Gasto";

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo actualizar la cuenta.",
                    "El identificador debe ser mayor que cero.");

            log.Info(proceso, $"Actualizando cuenta de gasto #{id}.", datos: req);

            var r = await s.ActualizarAsync(
                id,
                req.Nombre,
                req.TipoCuenta,
                req.Moneda,
                req.Observaciones,
                req.Activo,
                ct);

            if (r.Exito)
            {
                var saldo = await s.ObtenerSaldoAsync(id, ct) ?? r.Datos!.SaldoInicial;
                return Respuestas.Ok(MapearDto(r.Datos!, saldo),
                    "Cuenta de gasto actualizada correctamente.");
            }

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró la cuenta de gasto #{id}.")
                : Respuestas.SolicitudInvalida("No se pudo actualizar la cuenta de gasto.", r.Error ?? "");
        })
        .WithName("ActualizarCuentaGasto");

        // ============================================================
        // CAMBIAR ESTATUS DE CUENTA
        // PATCH /api/admin/cuentas-gasto/{id}/estatus
        // ============================================================
        admin.MapPatch("/{id:long}/estatus", async (
            long id,
            CambiarEstatusCuentaGastoRequest req,
            ServicioCuentasGastoAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Cambiar Estatus de Cuenta de Gasto";

            log.Info(proceso, $"Cambiando estatus de la cuenta #{id}.",
                datos: new { id, req.Activo });

            if (id <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo actualizar la cuenta.",
                    "El identificador debe ser mayor que cero.");

            var r = await s.CambiarEstatusAsync(id, req.Activo, ct);

            if (r.Exito)
            {
                var saldo = await s.ObtenerSaldoAsync(id, ct) ?? r.Datos!.SaldoInicial;
                return Respuestas.Ok(MapearDto(r.Datos!, saldo),
                    "Estatus de la cuenta actualizado correctamente.");
            }

            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"No se encontró la cuenta de gasto #{id}.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la cuenta de gasto #{id}.");
            }

            log.Warn(proceso, $"No se pudo actualizar el estatus de la cuenta #{id}: {r.Error}");

            return Respuestas.SolicitudInvalida(
                "No se pudo actualizar el estatus de la cuenta.",
                r.Error ?? "Ocurrió un error al actualizar el estatus.");
        })
        .WithName("CambiarEstatusCuentaGasto");
    }
}
