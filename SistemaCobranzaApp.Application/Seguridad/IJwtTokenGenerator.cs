using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Entidades;

namespace SistemaCobranzaApp.Application.Seguridad;


public record TokenGenerado(string Token, DateTimeOffset Expira);

public interface IJwtTokenGenerator
{
    TokenGenerado Generar(Usuario usuario);
}
