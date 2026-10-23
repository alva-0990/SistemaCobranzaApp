
using System.Security.Claims;
using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Gastos;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class GastosComprobantesEndpoints : IEndpointModule
{
    private static GastoComprobanteDto MapearDto(GastoComprobante c) =>
        new(
            c.IdComprobante,
            c.IdGasto,
            c.TipoComprobante,
            c.NombreArchivo,
            c.MimeType,
            c.ArchivoUrl,
            c.Observaciones,
            c.IdUsuarioRegistro,
            c.FechaRegistro);

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        static long? ObtenerIdUsuario(ClaimsPrincipal user)
        {
            var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? user.FindFirstValue("sub");

            return long.TryParse(raw, out var id) ? id : null;
        }

        var admin = app
            .MapGroup("/api/admin/gastos/{idGasto:long}/comprobantes")
            .RequireAuthorization();

        // ============================================================
        // 1. LISTAR COMPROBANTES DE UN GASTO
        // ============================================================
        admin.MapGet("/", async (
            long idGasto,
            ServicioGastosComprobantesAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Listar Comprobantes de Gasto";

            log.Info(proceso, $"Consultando comprobantes del gasto #{idGasto}.", datos: new { idGasto });

            if (idGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudieron consultar los comprobantes.",
                    "El identificador del gasto debe ser mayor que cero.");

            var lista = await s.ListarPorGastoAsync(idGasto, ct);
            var dto = lista.Select(MapearDto).ToList();

            return Respuestas.Ok(dto, "Comprobantes obtenidos correctamente.");
        }).WithName("ListarComprobantesGastoAdmin");

        // ============================================================
        // 2. OBTENER UN COMPROBANTE (metadatos)
        // ============================================================
        admin.MapGet("/{idComprobante:long}", async (
            long idGasto,
            long idComprobante,
            ServicioGastosComprobantesAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            if (idGasto <= 0 || idComprobante <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo consultar el comprobante.",
                    "Los identificadores deben ser mayores que cero.");

            var r = await s.ObtenerAsync(idComprobante, ct);

            if (!r.Exito)
                return r.NoEncontrado
                    ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró el comprobante #{idComprobante}.")
                    : Respuestas.SolicitudInvalida("No se pudo consultar el comprobante.", r.Error ?? "");

            if (r.Datos is null || r.Datos.IdGasto != idGasto)
                return Respuestas.NoEncontrado(
                    $"No se encontró el comprobante #{idComprobante} del gasto #{idGasto}.");

            return Respuestas.Ok(MapearDto(r.Datos), "Comprobante obtenido correctamente.");
        }).WithName("ObtenerComprobanteGastoAdmin");

        // ============================================================
        // 3. DESCARGAR EL ARCHIVO DE UN COMPROBANTE (bytes almacenados)
        // GET /api/admin/gastos/{idGasto}/comprobantes/{idComprobante}/archivo
        // ============================================================
        admin.MapGet("/{idComprobante:long}/archivo", async (
            long idGasto,
            long idComprobante,
            ServicioGastosComprobantesAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            if (idGasto <= 0 || idComprobante <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo descargar el comprobante.",
                    "Los identificadores deben ser mayores que cero.");

            var r = await s.ObtenerConArchivoAsync(idComprobante, ct);

            if (!r.Exito)
                return r.NoEncontrado
                    ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró el comprobante #{idComprobante}.")
                    : Respuestas.SolicitudInvalida("No se pudo descargar el comprobante.", r.Error ?? "");

            var comprobante = r.Datos!;

            if (comprobante.IdGasto != idGasto)
                return Respuestas.NoEncontrado(
                    $"No se encontró el comprobante #{idComprobante} del gasto #{idGasto}.");

            if (comprobante.ArchivoBytes is not { Length: > 0 })
            {
                // Sin bytes: puede ser un comprobante referenciado por URL.
                return string.IsNullOrWhiteSpace(comprobante.ArchivoUrl)
                    ? Respuestas.NoEncontrado("El comprobante no tiene un archivo almacenado.")
                    : Respuestas.Ok(new { comprobante.ArchivoUrl },
                        "El comprobante está almacenado en una URL externa.");
            }

            var mime = string.IsNullOrWhiteSpace(comprobante.MimeType)
                ? "application/octet-stream"
                : comprobante.MimeType!;
            var nombre = string.IsNullOrWhiteSpace(comprobante.NombreArchivo)
                ? $"comprobante-{idComprobante}"
                : comprobante.NombreArchivo!;

            return Results.File(comprobante.ArchivoBytes!, mime, nombre);
        }).WithName("DescargarComprobanteGastoAdmin");

        // ============================================================
        // 4. CREAR UN COMPROBANTE
        // ============================================================
        admin.MapPost("/", async (
            long idGasto,
            CrearGastoComprobanteRequest req,
            ClaimsPrincipal user,
            ServicioGastosComprobantesAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Registrar Comprobante de Gasto";

            log.Info(proceso, $"Registrando comprobante para el gasto #{idGasto}.",
                datos: new
                {
                    idGasto,
                    req.TipoComprobante,
                    req.NombreArchivo,
                    req.MimeType,
                    TieneArchivo = req.ArchivoBytes is { Length: > 0 },
                    TieneUrl = !string.IsNullOrWhiteSpace(req.ArchivoUrl)
                });

            if (idGasto <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el comprobante.",
                    "El identificador del gasto debe ser mayor que cero.");

            var idUsuario = ObtenerIdUsuario(user);

            if (!idUsuario.HasValue || idUsuario.Value <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo registrar el comprobante.",
                    "El token no contiene un identificador de usuario válido.");

            var r = await s.CrearAsync(
                idGasto,
                req.TipoComprobante,
                idUsuario.Value,
                req.NombreArchivo,
                req.MimeType,
                req.ArchivoBytes,
                req.ArchivoUrl,
                req.Observaciones,
                ct);

            if (r.Exito)
                return Respuestas.Creado(
                    $"/api/admin/gastos/{idGasto}/comprobantes/{r.Datos!.IdComprobante}",
                    MapearDto(r.Datos),
                    "Comprobante registrado correctamente.");

            if (r.NoEncontrado)
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró el gasto #{idGasto}.");

            return Respuestas.SolicitudInvalida(
                "No se pudo registrar el comprobante.", r.Error ?? "");
        }).WithName("CrearComprobanteGastoAdmin");

        // ============================================================
        // 5. ELIMINAR UN COMPROBANTE
        // ============================================================
        admin.MapDelete("/{idComprobante:long}", async (
            long idGasto,
            long idComprobante,
            ServicioGastosComprobantesAdmin s,
            IServicioLog log,
            CancellationToken ct) =>
        {
            const string proceso = "Eliminar Comprobante de Gasto";

            log.Info(proceso, $"Eliminando comprobante #{idComprobante} del gasto #{idGasto}.",
                datos: new { idGasto, idComprobante });

            if (idGasto <= 0 || idComprobante <= 0)
                return Respuestas.SolicitudInvalida(
                    "No se pudo eliminar el comprobante.",
                    "Los identificadores deben ser mayores que cero.");

            var existente = await s.ObtenerAsync(idComprobante, ct);

            if (!existente.Exito)
                return existente.NoEncontrado
                    ? Respuestas.NoEncontrado(existente.Error ?? $"No se encontró el comprobante #{idComprobante}.")
                    : Respuestas.SolicitudInvalida("No se pudo consultar el comprobante.", existente.Error ?? "");

            if (existente.Datos is null || existente.Datos.IdGasto != idGasto)
                return Respuestas.NoEncontrado(
                    $"No se encontró el comprobante #{idComprobante} del gasto #{idGasto}.");

            var r = await s.EliminarAsync(idComprobante, ct);

            if (r.Exito)
                return Respuestas.Ok(r.Datos, "Comprobante eliminado correctamente.");

            return r.NoEncontrado
                ? Respuestas.NoEncontrado(r.Error ?? $"No se encontró el comprobante #{idComprobante}.")
                : Respuestas.SolicitudInvalida("No se pudo eliminar el comprobante.", r.Error ?? "");
        }).WithName("EliminarComprobanteGastoAdmin");
    }
}
