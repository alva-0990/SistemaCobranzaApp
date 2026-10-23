using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Seguridad;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Seguridad;

public class ResultadoRol
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public RolDto? Rol { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoRol Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoRol NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoRol Ok(RolDto r) => new() { Exito = true, Rol = r };
}

public class ServicioRolesAdmin
{
    private const string Proceso = "ServicioRolesAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioRolesAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<RolDto>> ListarAsync(bool? incluirInactivos, CancellationToken ct = default)
    {
        try
        {
            var incluir = incluirInactivos ?? false;
            var q = _db.Roles.AsNoTracking().AsQueryable();
            if (!incluir) q = q.Where(r => r.Estatus == Estatus.Activo);
            return await q.OrderBy(r => r.Descripcion)
                .Select(r => new RolDto(r.IdRol, r.Descripcion, r.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { incluirInactivos });
            _log.Error(Proceso, "Error en la base de datos al obtener los roles.");
            throw;
        }
    }

    public async Task<RolDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var r = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(x => x.IdRol == id, ct);
            return r is null ? null : new RolDto(r.IdRol, r.Descripcion, r.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el rol #{id}.");
            throw;
        }
    }

    public async Task<ResultadoRol> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionRol.Validar(descripcion);
        if (error is not null) return ResultadoRol.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            if (await _db.Roles.AnyAsync(r => r.Descripcion == desc, ct))
                return ResultadoRol.Fallo("Ya existe un rol con esa descripción.");

            var rol = new Rol { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Roles.Add(rol);
            await _db.SaveChangesAsync(ct);
            return ResultadoRol.Ok(new RolDto(rol.IdRol, rol.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoRol.Fallo("Error en la base de datos al crear el rol.");
        }
    }

    public async Task<ResultadoRol> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionRol.Validar(descripcion);
        if (error is not null) return ResultadoRol.Fallo(error);

        try
        {
            var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == id, ct);
            if (rol is null) return ResultadoRol.NoExiste($"No se encontró el rol #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Roles.AnyAsync(r => r.Descripcion == desc && r.IdRol != id, ct))
                return ResultadoRol.Fallo("Ya existe otro rol con esa descripción.");

            rol.Descripcion = desc;
            rol.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoRol.Ok(new RolDto(rol.IdRol, rol.Descripcion, rol.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion, activo });
            return ResultadoRol.Fallo("Error en la base de datos al actualizar el rol.");
        }
    }

    public async Task<ResultadoRol> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var rol = await _db.Roles.FirstOrDefaultAsync(r => r.IdRol == id, ct);
            if (rol is null) return ResultadoRol.NoExiste($"No se encontró el rol #{id}.");

            rol.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoRol.Ok(new RolDto(rol.IdRol, rol.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoRol.Fallo("Error en la base de datos al dar de baja el rol.");
        }
    }
}
