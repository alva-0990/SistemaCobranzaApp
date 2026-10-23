using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Domain.Entidades;

// =====================================================================
// 1. ENTIDADES DE CONTROL (CATÁLOGOS ESTÁNDAR)
// =====================================================================

public class CatalogoCategoria
{
    public long IdCategoria { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoMarca
{
    public long IdMarca { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoColor
{
    public long IdColor { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

public class CatalogoTalla
{
    public long IdTalla { get; set; }
    public string Descripcion { get; set; } = string.Empty; // Ej: "M", "26", "CH"
    public string Tipo { get; set; } = string.Empty;        // Ej: "ROPA", "CALZADO", "GENERAL"
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

// =====================================================================
// 2. ENTIDAD PRINCIPAL (PRODUCTOS)
// =====================================================================

public class Producto
{
    public long IdProducto { get; set; }
    public string CodigoClave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Costo { get; set; }
    public decimal PrecioSugerido { get; set; }
    public decimal PrecioMinimo { get; set; }
    public int StockMini { get; set; }
    
    // Identificadores de Llaves Foráneas
    public long IdCategoria { get; set; }
    public long IdMarca { get; set; }
    public long? IdColor { get; set; } // Nullable para productos sin color estricto
    public long? IdTalla { get; set; } // Nullable para productos sin talla (como electrodomésticos)
    
    public Estatus Estatus { get; set; } = Estatus.Activo;

    // Propiedades de Navegación Virtuales (para Entity Framework)
    public virtual CatalogoCategoria Categoria { get; set; } = null!;
    public virtual CatalogoMarca Marca { get; set; } = null!;
    public virtual CatalogoColor? Color { get; set; }
    public virtual CatalogoTalla? Talla { get; set; }
}

public class CatalogoFrecuenciaPago
{
    public long IdFrecuenciaPago { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int DiasPeriodo { get; set; }
    public Estatus Estatus { get; set; } = Estatus.Activo;
}

