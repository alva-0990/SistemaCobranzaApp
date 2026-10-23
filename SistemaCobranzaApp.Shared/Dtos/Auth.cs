namespace SistemaCobranzaApp.Shared.Dtos;

public record LoginRequest(string Usuario, string Password);

public record LoginResponse(
    string Token,
    DateTimeOffset Expira,
    long IdUsuario,
    string Usuario,
    string NombreCompleto,
    long? IdRol,
    string? Rol,
    long? IdZona,
    string? Zona
    );

public record ActualizarPerfilRequest(string Usuario, string NombreCompleto);
public record ActualizarPasswordRequest(string NuevaPassword, string ConfirmacionPassword);
