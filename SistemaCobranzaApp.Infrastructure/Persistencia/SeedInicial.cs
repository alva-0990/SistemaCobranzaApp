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

    public static async Task SembrarAdminAsync(CobranzaDbContext db, CancellationToken ct = default)
    {
        var existe = await db.Usuarios.AnyAsync(u => u.Usuario_ == UsuarioAdminPorDefecto, ct);
        if (existe)
            return;

        db.Usuarios.Add(new Usuario
        {
            IdRol = 1, // Administrador
            Usuario_ = UsuarioAdminPorDefecto,
            PasswordHash = PasswordHelper.CrearHash(PasswordAdminPorDefecto),
            NombreCompleto = "Administrador del Sistema",
            Estatus = Estatus.Activo,
            FechaAlta = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }
}
