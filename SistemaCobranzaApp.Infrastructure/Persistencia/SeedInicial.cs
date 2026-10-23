using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Seguridad;

namespace SistemaCobranzaApp.Infrastructure.Persistencia;

/// <summary>
/// Siembra inicial idempotente. Crea el usuario administrador por defecto
/// si todavía no existe ningún usuario 'admin'. El hash se genera con la
/// misma librería que valida el login, evitando hashes desalineados.
/// </summary>
public static class SeedInicial
{
    public const string UsuarioAdminPorDefecto = "admin";
    public const string PasswordAdminPorDefecto = "Admin123!";
    public const string RolAdminPorDefecto = "Administrador";

    public static async Task SembrarAdminAsync(CobranzaDbContext db, CancellationToken ct = default)
    {
        var existe = await db.Usuarios.AnyAsync(u => u.Usuario_ == UsuarioAdminPorDefecto, ct);
        if (existe)
            return;

        // Garantizamos que exista un rol de administrador antes de crear el usuario.
        // Si la tabla de roles está vacía (BD recién creada sin el seed SQL de catálogos),
        // insertar el usuario con un IdRol fijo violaría la FK usuarios_idrol_fkey.
        var idRolAdmin = await ObtenerOCrearRolAdminAsync(db, ct);

        db.Usuarios.Add(new Usuario
        {
            IdRol = idRolAdmin,
            Usuario_ = UsuarioAdminPorDefecto,
            PasswordHash = PasswordHelper.CrearHash(PasswordAdminPorDefecto),
            NombreCompleto = "Administrador del Sistema",
            Estatus = Estatus.Activo,
            FechaAlta = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Devuelve el IdRol del rol de administrador. Prioriza el rol con IdRol = 1 (convención del
    /// catálogo); si no existe, busca uno por descripción "Administrador"; si tampoco existe,
    /// crea uno nuevo y deja que la base de datos asigne el Id. Idempotente.
    /// </summary>
    private static async Task<long> ObtenerOCrearRolAdminAsync(CobranzaDbContext db, CancellationToken ct)
    {
        // 1. Rol con el Id convencional del catálogo.
        var rolPorId = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.IdRol == 1, ct);
        if (rolPorId is not null)
            return rolPorId.IdRol;

        // 2. Rol por descripción (por si el Id difiere pero el rol ya existe).
        var rolPorNombre = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Descripcion == RolAdminPorDefecto, ct);
        if (rolPorNombre is not null)
            return rolPorNombre.IdRol;

        // 3. No hay ningún rol de administrador: lo creamos. La BD asigna el Id.
        var nuevoRol = new Rol
        {
            Descripcion = RolAdminPorDefecto,
            Estatus = Estatus.Activo
        };
        db.Roles.Add(nuevoRol);
        await db.SaveChangesAsync(ct);
        return nuevoRol.IdRol;
    }
}
