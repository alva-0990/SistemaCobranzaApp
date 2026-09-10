using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Application.Seguridad;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

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

            var token = _tokens.Generar(usuario);

            return ResultadoLogin.Ok(new LoginResponse(
                token.Token,
                token.Expira,
                usuario.IdUsuario,
                usuario.Usuario_,
                usuario.NombreCompleto,
                usuario.IdRol,
                usuario.Rol?.Descripcion));
        }

        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
