namespace SistemaCobranzaApp.Shared.Services;

public interface IAlmacenJornada
{
    bool Disponible { get; }
    bool ConInternet { get; }
    /// <summary>
    /// Se dispara ante un cambio de conectividad. Históricamente solo al RECUPERAR internet,
    /// hoy también al PERDERLO, para que la UI pueda repintar el estado de conexión en ambos
    /// casos. Los consumidores que ejecuten acciones de red deben verificar <see cref="ConInternet"/>.
    /// </summary>
    event Action? ConexionRecuperada;
    Task<string?> LeerAsync(long idUsuario);
    Task GuardarAsync(long idUsuario, string json);
}

/// <summary>La jornada offline se habilita solo en Android.</summary>
public sealed class AlmacenJornadaNoDisponible : IAlmacenJornada
{
    public bool Disponible => false;
    public bool ConInternet => true;
    public event Action? ConexionRecuperada { add { } remove { } }
    public Task<string?> LeerAsync(long idUsuario) => Task.FromResult<string?>(null);
    public Task GuardarAsync(long idUsuario, string json) => throw new NotSupportedException("La jornada offline requiere Android.");
}
