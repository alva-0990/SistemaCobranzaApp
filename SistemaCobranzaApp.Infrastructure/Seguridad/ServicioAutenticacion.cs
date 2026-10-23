using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Application.Seguridad;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Seguridad;

public class ResultadoLogin
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public LoginResponse? Respuesta { get; init; }

    public static ResultadoLogin Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoLogin Ok(LoginResponse r) => new() { Exito = true, Respuesta = r };
}

public class ServicioAutenticacion
{
    private readonly CobranzaDbContext _db;
    private readonly IJwtTokenGenerator _tokens;
    private readonly IServicioLog _log;

    public ServicioAutenticacion(CobranzaDbContext db, IJwtTokenGenerator tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<ResultadoLogin> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.Usuario) || string.IsNullOrWhiteSpace(req.Password))
                return ResultadoLogin.Fallo("Usuario y contraseña son obligatorios.");

            var usuario = await _db.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Usuario_ == req.Usuario, ct);

            // Mensaje genérico para no revelar si el usuario existe.
            if (usuario is null)
                return ResultadoLogin.Fallo("Usuario o contraseña incorrectos.");

            if (usuario.Estatus != Estatus.Activo)
                return ResultadoLogin.Fallo("El usuario está inactivo.");

            if (!PasswordHelper.VerificarPassword(req.Password, usuario.PasswordHash))
                return ResultadoLogin.Fallo("Usuario o contraseña incorrectos.");

            return ResultadoLogin.Ok(await ConstruirRespuestaAsync(usuario, ct));
        }

        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// Renueva la sesión de un usuario ya autenticado (token vigente): reemite un
    /// <see cref="LoginResponse"/> con un token nuevo y su fecha de expiración recalculada.
    /// No valida contraseña: lo invoca el endpoint protegido /api/auth/refresh, que ya
    /// exige un JWT válido. Permite extender la jornada offline al recuperar señal.
    /// </summary>
    public async Task<ResultadoLogin> RefreshAsync(long idUsuario, CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario, ct);

        if (usuario is null)
            return ResultadoLogin.Fallo("El usuario ya no está disponible.");
        if (usuario.Estatus != Estatus.Activo)
            return ResultadoLogin.Fallo("El usuario está inactivo.");

        return ResultadoLogin.Ok(await ConstruirRespuestaAsync(usuario, ct));
    }

    /// <summary>
    /// Arma el <see cref="LoginResponse"/> (token + datos del usuario + perfil operativo/zona)
    /// a partir de un usuario ya validado. Compartido por el login y la renovación de sesión.
    /// </summary>
    private async Task<LoginResponse> ConstruirRespuestaAsync(Domain.Entidades.Usuario usuario, CancellationToken ct)
    {
        // Perfil operativo y zona (id y texto) para los usuarios de campo.
        long? idZona = null;
        string? zonaDescripcion = null;

        // IdRol = 4 es Cobrador según los datos semilla.
        if (usuario.IdRol == 4)
        {
            var cobrador = await (from c in _db.Cobradores.AsNoTracking()
                                  where c.IdUsuario == usuario.IdUsuario
                                  join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                                  from z in zj.DefaultIfEmpty()
                                  select new
                                  {
                                      c.IdCobrador,
                                      c.IdZona,
                                      ZonaTexto = z != null ? z.Descripcion : null
                                  }).FirstOrDefaultAsync(ct);

            if (cobrador is not null)
            {
                idZona = cobrador.IdZona;
                zonaDescripcion = cobrador.ZonaTexto;
            }
        }
        // IdRol = 3 es Vendedor según los datos semilla.
        else if (usuario.IdRol == 3)
        {
            var vendedor = await (from v in _db.Vendedores.AsNoTracking()
                                  where v.IdUsuario == usuario.IdUsuario
                                  join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                                  from z in zj.DefaultIfEmpty()
                                  select new
                                  {
                                      v.IdVendedor,
                                      v.IdZona,
                                      ZonaTexto = z != null ? z.Descripcion : null
                                  }).FirstOrDefaultAsync(ct);

            if (vendedor is not null)
            {
                idZona = vendedor.IdZona;
                zonaDescripcion = vendedor.ZonaTexto;
            }
        }

        var token = _tokens.Generar(usuario);

        return new LoginResponse(
            token.Token,
            token.Expira,
            usuario.IdUsuario,
            usuario.Usuario_,
            usuario.NombreCompleto,
            usuario.IdRol,
            usuario.Rol?.Descripcion,
            idZona,
            zonaDescripcion);
    }
}
