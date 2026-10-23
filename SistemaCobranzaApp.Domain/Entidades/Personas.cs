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
    public long? IdUsuario { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;

    public Zona? Zona { get; set; }
}

public class Vendedor
{
    public long IdVendedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public long? IdZona { get; set; }
    public long? IdUsuario { get; set; }
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
    public Guid? ClienteUuid { get; set; }
    public long IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ApPaterno { get; set; }
    public string? ApMaterno { get; set; }

    // --- Nuevos Campos de Contacto y Localización ---
    public string TelefonoPrincipal { get; set; } = string.Empty;
    public string? TelefonoSecundario { get; set; }
    public string? Email { get; set; }
    public string Direccion { get; set; } = string.Empty;
    public string? CodigoPostal { get; set; }
    public string? ReferenciaUbicacion { get; set; }
    
    // Usamos decimal? para coincidir con el tipo NUMERIC de PostgreSQL y mantener precisión exacta
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }

    public long IdRuta { get; set; }
    public long? IdVendedor { get; set; }
    public long? IdClasificacion { get; set; }
    public DateTimeOffset FechaAlta { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;
    public string? Observaciones { get; set; }
    public long? IdUsuarioRegistro { get; set; }
    public DateTimeOffset FechaModificacion { get; set; }

    // --- Propiedades de Navegación de Entity Framework Core ---
    public Ruta? Ruta { get; set; }
    public Vendedor? Vendedor { get; set; }
    
    // Corregido: Se alinea con el tipo de clase de tu dominio para evitar errores de mapeo
    public CatalogoClasificacion? Clasificacion { get; set; }
}