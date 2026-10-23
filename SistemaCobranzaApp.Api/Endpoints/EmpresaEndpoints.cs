using SistemaCobranzaApp.Api.Comun;
using SistemaCobranzaApp.Infrastructure.Empresas;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Api.Endpoints;

public class EmpresaEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/empresa/identidad", async (ServicioEmpresaAdmin s, CancellationToken ct) =>
        {
            var empresa = await s.ObtenerPrincipalAsync(ct);
            return empresa is null ? Respuestas.NoEncontrado("No hay empresa configurada.")
                : Respuestas.Ok(new IdentidadEmpresaDto(empresa.NombreComercial, empresa.NombreCorto,
                    empresa.LogoUrl, empresa.LogoBlancoUrl));
        }).AllowAnonymous();

        // Endpoints Operativos de la Empresa (Para consumo general en la app móvil o web)
        var operativas = app.MapGroup("/api/empresa").RequireAuthorization();
        
        operativas.MapGet("/", async (ServicioEmpresaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Empresa Operativa";
            log.Info(proceso, "Consultando datos de la empresa principal.");
            
            var empresa = await s.ObtenerPrincipalAsync(ct);
            if (empresa is null)
            {
                log.Warn(proceso, "No hay ninguna empresa configurada en el sistema.");
                return Respuestas.NoEncontrado("No se encontraron los datos de la empresa.");
            }

            log.Info(proceso, $"Datos comerciales obtenidos: {empresa.NombreComercial}.");
            return Respuestas.Ok(empresa, "Datos de la empresa obtenidos correctamente.");
        });

        // Endpoints Administrativos (Para paneles de control y configuración del sistema)
        var admin = app.MapGroup("/api/admin/empresa").RequireAuthorization();

        admin.MapGet("/{id:int}", async (int id, ServicioEmpresaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Obtener Empresa (Admin)";
            log.Info(proceso, $"Buscando configuración de empresa #{id}.", datos: new { id });
            
            var e = await s.ObtenerPorIdAsync(id, ct);
            if (e is null)
            {
                log.Warn(proceso, $"Empresa #{id} no encontrada.");
                return Respuestas.NoEncontrado($"No se encontró la empresa con ID #{id}.");
            }
            
            log.Info(proceso, $"Empresa #{id} obtenida.", datos: new { e.Id, e.NombreComercial });
            return Respuestas.Ok(e, "Configuración de empresa obtenida.");
        });

        admin.MapPost("/", async (CrearEmpresaRequest req, ServicioEmpresaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Alta de Configuración de Empresa";
            log.Info(proceso, $"Registrando nueva empresa: '{req.RazonSocial}'.", datos: new { req.NombreComercial });
            
            var r = await s.CrearAsync(req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Empresa creada #{r.Empresa!.Id} ('{r.Empresa.NombreComercial}').", datos: new { r.Empresa!.Id, r.Empresa.NombreComercial });
                return Respuestas.Creado($"/api/admin/empresa/{r.Empresa.Id}", r.Empresa, "Empresa registrada correctamente.");
            }
            
            log.Warn(proceso, $"No se pudo registrar la empresa: {r.Error}", datos: new { req.NombreComercial });
            return Respuestas.SolicitudInvalida("No se pudo registrar la empresa.", r.Error ?? "");
        });

        admin.MapPut("/{id:int}", async (int id, ActualizarEmpresaRequest req, ServicioEmpresaAdmin s, IServicioLog log, CancellationToken ct) =>
        {
            const string proceso = "Actualización de Parámetros de Empresa";
            log.Info(proceso, $"Actualizando empresa #{id} ('{req.NombreComercial}').", datos: new { id, req.NombreComercial });
            
            var r = await s.ActualizarAsync(id, req, ct);
            if (r.Exito)
            {
                log.Info(proceso, $"Empresa #{id} modificada con éxito.", datos: new { r.Empresa!.Id, r.Empresa.NombreComercial });
                return Respuestas.Ok(r.Empresa!, "Configuración de empresa actualizada correctamente.");
            }
            if (r.NoEncontrado)
            {
                log.Warn(proceso, $"Empresa #{id} no encontrada para actualización.");
                return Respuestas.NoEncontrado(r.Error ?? $"No se encontró la empresa #{id}.");
            }
            
            log.Warn(proceso, $"No se pudo actualizar la empresa #{id}: {r.Error}", datos: new { id });
            return Respuestas.SolicitudInvalida("No se pudieron guardar los cambios de la empresa.", r.Error ?? "");
        });
    }
}
