namespace SistemaCobranzaApp.Shared.Dtos;

public record LoginRequest(string Usuario, string Password);

public record LoginResponse(
    string Token,
    DateTimeOffset Expira,
    long IdUsuario,
    string Usuario,
    string NombreCompleto,
    long? IdRol,
    string? Rol);
