using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoMes
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public MesDto? Mes { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoMes Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoMes NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoMes Ok(MesDto m) => new() { Exito = true, Mes = m };
}

public class ServicioMesesAdmin
{
    private const string Proceso = "ServicioMesesAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioMesesAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activos)
    public async Task<List<MesDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoMeses
                .AsNoTracking()
                .Where(m => m.Estatus == Estatus.Activo)
                .OrderBy(m => m.NumMeses)
                .Select(m => new MesDto(m.IdMes, m.NumMeses, m.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener los meses activos.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<MesDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoMeses
                .AsNoTracking()
                .IgnoreQueryFilters()
                .OrderBy(m => m.NumMeses)
                .Select(m => new MesDto(m.IdMes, m.NumMeses, m.Descripcion, m.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener todos los meses.");
            throw;
        }
    }

    // 3. Obtener por ID
    public async Task<MesDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var m = await _db.CatalogoMeses
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.IdMes == id, ct);

            return m is null ? null : new MesDto(m.IdMes, m.NumMeses, m.Descripcion, m.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el mes #{id}.");
            throw;
        }
    }

    // 4. Crear (Alta)
    public async Task<ResultadoMes> CrearAsync(int numMeses, string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMes.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            if (await _db.CatalogoMeses.IgnoreQueryFilters().AnyAsync(m => m.NumMeses == numMeses || m.Descripcion == desc, ct))
                return ResultadoMes.Fallo("Ya existe un registro con el mismo número de meses o descripción.");

            var nuevoMes = new CatalogoMes { NumMeses = numMeses, Descripcion = desc, Estatus = Estatus.Activo };
            _db.CatalogoMeses.Add(nuevoMes);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMes.Ok(new MesDto(nuevoMes.IdMes, nuevoMes.NumMeses, nuevoMes.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { numMeses, descripcion = desc });
            return ResultadoMes.Fallo("Error en la base de datos al crear el registro de meses.");
        }
    }

    // 5. Actualizar
    public async Task<ResultadoMes> ActualizarAsync(long id, int numMeses, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMes.Fallo(error);

        try
        {
            var mes = await _db.CatalogoMeses.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.IdMes == id, ct);
            if (mes is null) return ResultadoMes.NoExiste($"No se encontró el registro de mes #{id}.");

            var desc = descripcion.Trim();
            if (await _db.CatalogoMeses.IgnoreQueryFilters().AnyAsync(m => (m.NumMeses == numMeses || m.Descripcion == desc) && m.IdMes != id, ct))
                return ResultadoMes.Fallo("Ya existe otro registro con ese número de meses o descripción.");

            mes.NumMeses = numMeses;
            mes.Descripcion = desc;
            mes.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMes.Ok(new MesDto(mes.IdMes, mes.NumMeses, mes.Descripcion, mes.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, numMeses, descripcion });
            return ResultadoMes.Fallo("Error en la base de datos al actualizar el registro de mes.");
        }
    }

    // 6. Dar de Baja (Inactivar)
    public async Task<ResultadoMes> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var mes = await _db.CatalogoMeses.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.IdMes == id, ct);
            if (mes is null) return ResultadoMes.NoExiste($"No se encontró el registro de mes #{id}.");

            mes.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMes.Ok(new MesDto(mes.IdMes, mes.NumMeses, mes.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoMes.Fallo("Error en la base de datos al dar de baja el registro de mes.");
        }
    }
}
