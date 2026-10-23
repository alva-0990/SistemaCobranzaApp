using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SistemaCobranzaApp.Application.Seguridad;
using SistemaCobranzaApp.Domain.Entidades;

namespace SistemaCobranzaApp.Api.Seguridad;

public class JwtOptions
{
    public string Issuer { get; set; } = "SistemaCobranza";
    public string Audience { get; set; } = "SistemaCobranza";
    public string Key { get; set; } = string.Empty;
    public int MinutosVigencia { get; set; } = 900; // 15 horas (jornada de campo + renovación al recuperar señal)
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opciones;

    public JwtTokenGenerator(JwtOptions opciones)
    {
        _opciones = opciones;
    }

    public TokenGenerado Generar(Usuario usuario)
    {
        var expira = DateTimeOffset.UtcNow.AddMinutes(_opciones.MinutosVigencia);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.Usuario_),
            new("nombre", usuario.NombreCompleto)
        };

        if (usuario.Rol is not null)
            claims.Add(new Claim(ClaimTypes.Role, usuario.Rol.Descripcion));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Issuer,
            audience: _opciones.Audience,
            claims: claims,
            expires: expira.UtcDateTime,
            signingCredentials: creds);

        var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenGenerado(tokenStr, expira);
    }
}
