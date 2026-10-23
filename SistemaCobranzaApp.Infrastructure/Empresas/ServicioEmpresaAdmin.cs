using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Shared.Services;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Empresas;

public class ResultadoEmpresa
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public EmpresaDto? Empresa { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoEmpresa Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoEmpresa NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoEmpresa Ok(EmpresaDto e) => new() { Exito = true, Empresa = e };
}

public class ServicioEmpresaAdmin
{
    private const string Proceso = "ServicioEmpresaAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioEmpresaAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Obtener la empresa principal (Configuración activa única)
    public async Task<EmpresaDto?> ObtenerPrincipalAsync(CancellationToken ct = default)
    {
        try
        {
            var e = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            return e is null ? null : MapearADto(e);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener los datos de la empresa principal.");
            throw;
        }
    }

    // 2. Obtener por un ID específico
    public async Task<EmpresaDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var e = await _db.Empresas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return e is null ? null : MapearADto(e);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener la empresa #{id}.");
            throw;
        }
    }

    // 3. Crear el registro base de la empresa
    public async Task<ResultadoEmpresa> CrearAsync(CrearEmpresaRequest req, CancellationToken ct = default)
    {
        var errorLogo = ImagenEmpresa.Validar(req.LogoUrl) ?? ImagenEmpresa.Validar(req.LogoBlancoUrl);
        if (errorLogo is not null) return ResultadoEmpresa.Fallo(errorLogo);
        if (string.IsNullOrWhiteSpace(req.RazonSocial)) return ResultadoEmpresa.Fallo("La razón social es requerida.");
        if (string.IsNullOrWhiteSpace(req.NombreComercial)) return ResultadoEmpresa.Fallo("El nombre comercial es requerido.");
        if (string.IsNullOrWhiteSpace(req.Rfc_o_Nit)) return ResultadoEmpresa.Fallo("El identificador fiscal (RFC/NIT) es requerido.");

        var rfc = req.Rfc_o_Nit.Trim();

        try
        {
            if (await _db.Empresas.AnyAsync(x => x.Rfc_o_Nit == rfc, ct))
                return ResultadoEmpresa.Fallo("Ya existe una empresa registrada con ese RFC / NIT.");

            var empresa = new Empresa
            {
                RazonSocial = req.RazonSocial.Trim(),
                NombreComercial = req.NombreComercial.Trim(),
                NombreCorto = req.NombreCorto?.Trim(),
                Rfc_o_Nit = rfc,
                RegimenFiscal = req.RegimenFiscal?.Trim(),
                AnioFundacion = req.AnioFundacion,
                Lema = req.Lema?.Trim(),
                LogoUrl = req.LogoUrl?.Trim(),
                LogoBlancoUrl = req.LogoBlancoUrl?.Trim(),
                TelefonoPrincipal = req.TelefonoPrincipal?.Trim(),
                TelefonoSecundario = req.TelefonoSecundario?.Trim(),
                CorreoContacto = req.CorreoContacto?.Trim(),
                CorreoSoporte = req.CorreoSoporte?.Trim(),
                SitioWeb = req.SitioWeb?.Trim(),
                FacebookUrl = req.FacebookUrl?.Trim(),
                InstagramUrl = req.InstagramUrl?.Trim(),
                LinkedinUrl = req.LinkedinUrl?.Trim(),
                Calle = req.Calle?.Trim(),
                NumeroExterior = req.NumeroExterior?.Trim(),
                NumeroInterior = req.NumeroInterior?.Trim(),
                Colonia = req.Colonia?.Trim(),
                CodigoPostal = req.CodigoPostal?.Trim(),
                Ciudad = req.Ciudad?.Trim(),
                Estado_o_Provincia = req.Estado_o_Provincia?.Trim(),
                Pais = req.Pais?.Trim(),
                MonedaPorDefecto = req.MonedaPorDefecto?.Trim() ?? "MXN",
                SimboloMoneda = req.SimboloMoneda?.Trim() ?? "$",
                PorcentajeImpuesto = req.PorcentajeImpuesto,
                HorarioAtencion = req.HorarioAtencion?.Trim()
            };

            _db.Empresas.Add(empresa);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoEmpresa.Ok(MapearADto(empresa));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { req.RazonSocial });
            return ResultadoEmpresa.Fallo("Error en la base de datos al registrar la empresa.");
        }
    }

    // 4. Actualizar todos los datos comerciales y de operación
    public async Task<ResultadoEmpresa> ActualizarAsync(int id, ActualizarEmpresaRequest req, CancellationToken ct = default)
    {
        var errorLogo = ImagenEmpresa.Validar(req.LogoUrl) ?? ImagenEmpresa.Validar(req.LogoBlancoUrl);
        if (errorLogo is not null) return ResultadoEmpresa.Fallo(errorLogo);
        if (string.IsNullOrWhiteSpace(req.RazonSocial)) return ResultadoEmpresa.Fallo("La razón social es requerida.");
        if (string.IsNullOrWhiteSpace(req.NombreComercial)) return ResultadoEmpresa.Fallo("El nombre comercial es requerido.");
        if (string.IsNullOrWhiteSpace(req.Rfc_o_Nit)) return ResultadoEmpresa.Fallo("El identificador fiscal (RFC/NIT) es requerido.");

        try
        {
            var empresa = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (empresa is null) return ResultadoEmpresa.NoExiste($"No se encontró la empresa con ID #{id}.");

            var rfc = req.Rfc_o_Nit.Trim();
            if (await _db.Empresas.AnyAsync(x => x.Rfc_o_Nit == rfc && x.Id != id, ct))
                return ResultadoEmpresa.Fallo("El RFC / NIT ingresado ya está mapeado en otra configuración de empresa.");

            empresa.RazonSocial = req.RazonSocial.Trim();
            empresa.NombreComercial = req.NombreComercial.Trim();
            empresa.NombreCorto = req.NombreCorto?.Trim();
            empresa.Rfc_o_Nit = rfc;
            empresa.RegimenFiscal = req.RegimenFiscal?.Trim();
            empresa.AnioFundacion = req.AnioFundacion;
            empresa.Lema = req.Lema?.Trim();
            empresa.LogoUrl = req.LogoUrl?.Trim();
            empresa.LogoBlancoUrl = req.LogoBlancoUrl?.Trim();
            empresa.TelefonoPrincipal = req.TelefonoPrincipal?.Trim();
            empresa.TelefonoSecundario = req.TelefonoSecundario?.Trim();
            empresa.CorreoContacto = req.CorreoContacto?.Trim();
            empresa.CorreoSoporte = req.CorreoSoporte?.Trim();
            empresa.SitioWeb = req.SitioWeb?.Trim();
            empresa.FacebookUrl = req.FacebookUrl?.Trim();
            empresa.InstagramUrl = req.InstagramUrl?.Trim();
            empresa.LinkedinUrl = req.LinkedinUrl?.Trim();
            empresa.Calle = req.Calle?.Trim();
            empresa.NumeroExterior = req.NumeroExterior?.Trim();
            empresa.NumeroInterior = req.NumeroInterior?.Trim();
            empresa.Colonia = req.Colonia?.Trim();
            empresa.CodigoPostal = req.CodigoPostal?.Trim();
            empresa.Ciudad = req.Ciudad?.Trim();
            empresa.Estado_o_Provincia = req.Estado_o_Provincia?.Trim();
            empresa.Pais = req.Pais?.Trim();
            empresa.MonedaPorDefecto = req.MonedaPorDefecto?.Trim() ?? "MXN";
            empresa.SimboloMoneda = req.SimboloMoneda?.Trim() ?? "$";
            empresa.PorcentajeImpuesto = req.PorcentajeImpuesto;
            empresa.HorarioAtencion = req.HorarioAtencion?.Trim();

            await _db.SaveChangesAsync(ct);
            return ResultadoEmpresa.Ok(MapearADto(empresa));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoEmpresa.Fallo("Error en la base de datos al actualizar los parámetros de la empresa.");
        }
    }

    // Mapeador privado estático limpio
    private static EmpresaDto MapearADto(Empresa e) => new(
        e.Id, e.RazonSocial, e.NombreComercial, e.NombreCorto, e.Rfc_o_Nit, e.RegimenFiscal, e.AnioFundacion,
        e.Lema, e.LogoUrl, e.LogoBlancoUrl, e.TelefonoPrincipal, e.TelefonoSecundario, e.CorreoContacto, e.CorreoSoporte,
        e.SitioWeb, e.FacebookUrl, e.InstagramUrl, e.LinkedinUrl, e.Calle, e.NumeroExterior, e.NumeroInterior,
        e.Colonia, e.CodigoPostal, e.Ciudad, e.Estado_o_Provincia, e.Pais, e.MonedaPorDefecto, e.SimboloMoneda,
        e.PorcentajeImpuesto, e.HorarioAtencion
    );
}
