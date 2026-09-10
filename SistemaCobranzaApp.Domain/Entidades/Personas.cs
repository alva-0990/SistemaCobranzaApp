using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Domain.Entidades;

public class Usuario
{
    public long IdUsuario { get; set; }
    public long? IdRol { get; set; }
    public string Usuario_ { get; set; } = string.Empty; // columna "Usuario"
    public string PasswordHash { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
    public DateTimeOffset FechaAlta { get; set; }

    public Rol? Rol { get; set; }
}

public class Cobrador
{
    public long IdCobrador { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public long? IdZona { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;

    public Zona? Zona { get; set; }
}

public class Vendedor
{
    public long IdVendedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public long? IdZona { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;

    public Zona? Zona { get; set; }
}

public class Ruta
{
    public long IdRuta { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public long? IdZona { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;

    public Zona? Zona { get; set; }
}

public class Cliente
{
    public long IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ApPaterno { get; set; }
    public string? ApMaterno { get; set; }
    public long IdRuta { get; set; }
    public long? IdVendedor { get; set; }
    public long? IdClasificacion { get; set; }
    public DateTimeOffset FechaAlta { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    public Ruta? Ruta { get; set; }
    public Vendedor? Vendedor { get; set; }
    public CatalogoClasificacion? Clasificacion { get; set; }
}
