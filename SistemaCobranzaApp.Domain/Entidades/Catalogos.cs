using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Domain.Entidades;

public class Rol
{
    public long IdRol { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoCondicion
{
    public long IdCondicion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoClasificacion
{
    public long IdClasificacion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoMovimientos
{
    public long IdMovimiento { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoEstatusVenta
{
    public long IdEstatusVenta { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class Zona
{
    public long IdZona { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoMes
{
    public long IdMes { get; set; }
    public int NumMeses { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

