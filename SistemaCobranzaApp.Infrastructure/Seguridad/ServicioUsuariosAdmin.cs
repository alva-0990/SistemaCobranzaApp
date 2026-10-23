using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Seguridad;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Seguridad;

public class ResultadoUsuario
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public UsuarioDto? Usuario { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoUsuario Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoUsuario NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoUsuario Ok(UsuarioDto u) => new() { Exito = true, Usuario = u };
}

/// <summary>
/// CRUD de usuarios. La contraseña se hashea con BCrypt (PasswordHelper).
/// Nunca se expone PasswordHash. Baja lógica (Estatus = Inactivo).
/// IMPORTANTE: los logs NUNCA registran la contraseña; solo campos no sensibles.
/// </summary>
public class ServicioUsuariosAdmin
{
    private const string Proceso = "ServicioUsuariosAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioUsuariosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<UsuarioDto>> ListarAsync(bool? incluirInactivos, CancellationToken ct = default)
    {
        try
        {
            var incluir = incluirInactivos ?? false;
            var q = from u in _db.Usuarios.AsNoTracking()
                    join r in _db.Roles.AsNoTracking() on u.IdRol equals r.IdRol into rj
                    from r in rj.DefaultIfEmpty()
                    select new { u, r };

            if (!incluir) q = q.Where(x => x.u.Estatus == Estatus.Activo);

            return await q.OrderBy(x => x.u.Usuario_)
                .Select(x => new UsuarioDto(
                    x.u.IdUsuario, x.u.Usuario_, x.u.NombreCompleto, x.u.IdRol,
                    x.r != null ? x.r.Descripcion : null,
                    x.u.Estatus == Estatus.Activo, x.u.FechaAlta))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { incluirInactivos });
            _log.Error(Proceso, "Error en la base de datos al obtener los usuarios.");
            throw;
        }
    }

    public async Task<UsuarioDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var fila = await (from u in _db.Usuarios.AsNoTracking()
                              where u.IdUsuario == id
                              join r in _db.Roles.AsNoTracking() on u.IdRol equals r.IdRol into rj
                              from r in rj.DefaultIfEmpty()
                              select new { u, r }).FirstOrDefaultAsync(ct);
            return fila is null ? null : Map(fila.u, fila.r);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el usuario #{id}.");
            throw;
        }
    }

    public async Task<ResultadoUsuario> CrearAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var error = ValidacionUsuario.Validar(req.Usuario, req.NombreCompleto, req.Password, requerirPassword: true);
        if (error is not null) return ResultadoUsuario.Fallo(error);

        try
        {
            var nombreUsuario = req.Usuario.Trim();
            if (await _db.Usuarios.AnyAsync(u => u.Usuario_ == nombreUsuario, ct))
                return ResultadoUsuario.Fallo("Ya existe un usuario con ese nombre.");

            if (req.IdRol is { } rol && !await _db.Roles.AnyAsync(r => r.IdRol == rol, ct))
                return ResultadoUsuario.Fallo($"El rol #{rol} no existe.");

            var usuario = new Usuario
            {
                Usuario_ = nombreUsuario,
                PasswordHash = PasswordHelper.CrearHash(req.Password),
                NombreCompleto = req.NombreCompleto.Trim(),
                IdRol = req.IdRol,
                Estatus = Estatus.Activo,
                FechaAlta = DateTimeOffset.UtcNow
            };
            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync(ct);

            return ResultadoUsuario.Ok((await ObtenerAsync(usuario.IdUsuario, ct))!);
        }
        catch (Exception ex)
        {
            // No se registra la contraseña: solo campos no sensibles.
            _log.Error(Proceso, ex, datos: new { usuario = req.Usuario, req.NombreCompleto, req.IdRol });
            return ResultadoUsuario.Fallo("Error en la base de datos al crear el usuario.");
        }
    }

    public async Task<ResultadoUsuario> ActualizarAsync(long id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        var error = ValidacionUsuario.Validar(req.Usuario, req.NombreCompleto, req.Password, requerirPassword: false);
        if (error is not null) return ResultadoUsuario.Fallo(error);

        try
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
            if (usuario is null) return ResultadoUsuario.NoExiste($"No se encontró el usuario #{id}.");

            var nombreUsuario = req.Usuario.Trim();
            if (await _db.Usuarios.AnyAsync(u => u.Usuario_ == nombreUsuario && u.IdUsuario != id, ct))
                return ResultadoUsuario.Fallo("Ya existe otro usuario con ese nombre.");

            if (req.IdRol is { } rol && !await _db.Roles.AnyAsync(r => r.IdRol == rol, ct))
                return ResultadoUsuario.Fallo($"El rol #{rol} no existe.");

            usuario.Usuario_ = nombreUsuario;
            usuario.NombreCompleto = req.NombreCompleto.Trim();
            usuario.IdRol = req.IdRol;
            usuario.Estatus = req.Activo ? Estatus.Activo : Estatus.Inactivo;

            // Cambiar contraseña solo si se envió una nueva.
            if (!string.IsNullOrWhiteSpace(req.Password))
                usuario.PasswordHash = PasswordHelper.CrearHash(req.Password);

            await _db.SaveChangesAsync(ct);
            return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            // No se registra la contraseña: solo campos no sensibles.
            _log.Error(Proceso, ex, datos: new { id, usuario = req.Usuario, req.NombreCompleto, req.IdRol });
            return ResultadoUsuario.Fallo("Error en la base de datos al actualizar el usuario.");
        }
    }

    public async Task<ResultadoUsuario> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
            if (usuario is null) return ResultadoUsuario.NoExiste($"No se encontró el usuario #{id}.");

            usuario.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoUsuario.Fallo("Error en la base de datos al dar de baja el usuario.");
        }
    }

    public async Task<ResultadoUsuario> ActualizarPerfilAsync(long id, ActualizarPerfilRequest req, CancellationToken ct)
    {
        var error = ValidacionUsuario.Validar(req.Usuario, req.NombreCompleto, null, false);
        if (error is not null) return ResultadoUsuario.Fallo(error);
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.Estatus == Estatus.Activo, ct);
        if (usuario is null) return ResultadoUsuario.NoExiste("El usuario no está disponible.");
        var nombre = req.Usuario.Trim();
        if (await _db.Usuarios.AnyAsync(u => u.Usuario_ == nombre && u.IdUsuario != id, ct))
            return ResultadoUsuario.Fallo("Ya existe otro usuario con ese nombre.");
        usuario.Usuario_ = nombre;
        usuario.NombreCompleto = req.NombreCompleto.Trim();
        await _db.SaveChangesAsync(ct);
        return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<ResultadoUsuario> ActualizarPasswordAsync(long id, ActualizarPasswordRequest req, CancellationToken ct)
    {
        if (req.NuevaPassword != req.ConfirmacionPassword)
            return ResultadoUsuario.Fallo("Las contraseñas no coinciden.");
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id && u.Estatus == Estatus.Activo, ct);
        if (usuario is null) return ResultadoUsuario.NoExiste("El usuario no está disponible.");
        var error = ValidacionUsuario.Validar(usuario.Usuario_, usuario.NombreCompleto, req.NuevaPassword, true);
        if (error is not null) return ResultadoUsuario.Fallo(error);
        usuario.PasswordHash = PasswordHelper.CrearHash(req.NuevaPassword);
        await _db.SaveChangesAsync(ct);
        return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
    }

    private static UsuarioDto Map(Usuario u, Rol? r) => new(
        u.IdUsuario, u.Usuario_, u.NombreCompleto, u.IdRol,
        r?.Descripcion, u.Estatus == Estatus.Activo, u.FechaAlta);
}
