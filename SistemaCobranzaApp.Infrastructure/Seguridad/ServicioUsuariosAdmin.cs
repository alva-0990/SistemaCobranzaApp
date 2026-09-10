using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Seguridad;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

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
/// </summary>
public class ServicioUsuariosAdmin
{
    private readonly CobranzaDbContext _db;

    public ServicioUsuariosAdmin(CobranzaDbContext db) => _db = db;

    public Task<List<UsuarioDto>> ListarAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        var q = from u in _db.Usuarios.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on u.IdRol equals r.IdRol into rj
                from r in rj.DefaultIfEmpty()
                select new { u, r };

        if (!incluirInactivos) q = q.Where(x => x.u.Estatus == Estatus.Activo);

        return q.OrderBy(x => x.u.Usuario_)
            .Select(x => new UsuarioDto(
                x.u.IdUsuario, x.u.Usuario_, x.u.NombreCompleto, x.u.IdRol,
                x.r != null ? x.r.Descripcion : null,
                x.u.Estatus == Estatus.Activo, x.u.FechaAlta))
            .ToListAsync(ct);
    }

    public async Task<UsuarioDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from u in _db.Usuarios.AsNoTracking()
                          where u.IdUsuario == id
                          join r in _db.Roles.AsNoTracking() on u.IdRol equals r.IdRol into rj
                          from r in rj.DefaultIfEmpty()
                          select new { u, r }).FirstOrDefaultAsync(ct);
        return fila is null ? null : Map(fila.u, fila.r);
    }

    public async Task<ResultadoUsuario> CrearAsync(CrearUsuarioRequest req, CancellationToken ct = default)
    {
        var error = ValidacionUsuario.Validar(req.Usuario, req.NombreCompleto, req.Password, requerirPassword: true);
        if (error is not null) return ResultadoUsuario.Fallo(error);

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

    public async Task<ResultadoUsuario> ActualizarAsync(long id, ActualizarUsuarioRequest req, CancellationToken ct = default)
    {
        var error = ValidacionUsuario.Validar(req.Usuario, req.NombreCompleto, req.Password, requerirPassword: false);
        if (error is not null) return ResultadoUsuario.Fallo(error);

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

        // Cambiar contraseña solo si se envió una nueva.
        if (!string.IsNullOrWhiteSpace(req.Password))
            usuario.PasswordHash = PasswordHelper.CrearHash(req.Password);

        await _db.SaveChangesAsync(ct);
        return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<ResultadoUsuario> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == id, ct);
        if (usuario is null) return ResultadoUsuario.NoExiste($"No se encontró el usuario #{id}.");

        usuario.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoUsuario.Ok((await ObtenerAsync(id, ct))!);
    }

    private static UsuarioDto Map(Usuario u, Rol? r) => new(
        u.IdUsuario, u.Usuario_, u.NombreCompleto, u.IdRol,
        r?.Descripcion, u.Estatus == Estatus.Activo, u.FechaAlta);
}
