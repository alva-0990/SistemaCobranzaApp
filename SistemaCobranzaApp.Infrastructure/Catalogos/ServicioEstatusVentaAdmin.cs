

using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoEstatusVenta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public EstatusVentaDto? EstatusVenta { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoEstatusVenta Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoEstatusVenta NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoEstatusVenta Ok(EstatusVentaDto e) => new() { Exito = true, EstatusVenta = e };
}

public class ServicioEstatusVentaAdmin
{
    private const string Proceso = "ServicioEstatusVentaAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioEstatusVentaAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activas)
    public async Task<List<EstatusVentaDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Set<CatalogoEstatusVenta>()
                .AsNoTracking()
                .Where(e => e.Estatus == Estatus.Activo)
                .OrderBy(e => e.Descripcion)
                .Select(e => new EstatusVentaDto(e.IdEstatusVenta, e.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener estatus de venta activos.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<EstatusVentaDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Set<CatalogoEstatusVenta>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .OrderBy(e => e.Descripcion)
                .Select(e => new EstatusVentaDto(e.IdEstatusVenta, e.Descripcion, e.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener todos los estatus de venta.");
            throw;
        }
    }

    // 3. Obtener por ID
    public async Task<EstatusVentaDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var e = await _db.Set<CatalogoEstatusVenta>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.IdEstatusVenta == id, ct);

            return e is null ? null : new EstatusVentaDto(e.IdEstatusVenta, e.Descripcion, e.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el estatus de venta #{id}.");
            throw;
        }
    }

    // 4. Crear (Alta)
    public async Task<ResultadoEstatusVenta> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoEstatusVenta.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            if (await _db.Set<CatalogoEstatusVenta>().IgnoreQueryFilters().AnyAsync(e => e.Descripcion == desc, ct))
                return ResultadoEstatusVenta.Fallo("Ya existe un estatus de venta con esa descripción.");

            var estatusVenta = new CatalogoEstatusVenta { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Set<CatalogoEstatusVenta>().Add(estatusVenta);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoEstatusVenta.Ok(new EstatusVentaDto(estatusVenta.IdEstatusVenta, estatusVenta.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoEstatusVenta.Fallo("Error en la base de datos al crear el estatus de venta.");
        }
    }

    // 5. Actualizar
    public async Task<ResultadoEstatusVenta> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoEstatusVenta.Fallo(error);

        try
        {
            var estatusVenta = await _db.Set<CatalogoEstatusVenta>().IgnoreQueryFilters().FirstOrDefaultAsync(e => e.IdEstatusVenta == id, ct);
            if (estatusVenta is null) return ResultadoEstatusVenta.NoExiste($"No se encontró el estatus de venta #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Set<CatalogoEstatusVenta>().IgnoreQueryFilters().AnyAsync(e => e.Descripcion == desc && e.IdEstatusVenta != id, ct))
                return ResultadoEstatusVenta.Fallo("Ya existe otro estatus de venta con esa descripción.");

            estatusVenta.Descripcion = desc;
            estatusVenta.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoEstatusVenta.Ok(new EstatusVentaDto(estatusVenta.IdEstatusVenta, estatusVenta.Descripcion, estatusVenta.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoEstatusVenta.Fallo("Error en la base de datos al actualizar el estatus de venta.");
        }
    }

    // 6. Dar de Baja (Inactivar)
    public async Task<ResultadoEstatusVenta> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var estatusVenta = await _db.Set<CatalogoEstatusVenta>().IgnoreQueryFilters().FirstOrDefaultAsync(e => e.IdEstatusVenta == id, ct);
            if (estatusVenta is null) return ResultadoEstatusVenta.NoExiste($"No se encontró el estatus de venta #{id}.");

            estatusVenta.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoEstatusVenta.Ok(new EstatusVentaDto(estatusVenta.IdEstatusVenta, estatusVenta.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoEstatusVenta.Fallo("Error en la base de datos al dar de baja el estatus de venta.");
        }
    }
}
