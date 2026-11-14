namespace SistemaCobranzaApp.Shared.Seguridad;

/// <summary>IDs fijos de la tabla Rol (ver db/init/02_seed.sql).</summary>
public static class RolesSistema
{
    public const long Administrador = 1;
    public const long Supervisor = 2;
    public const long VendedorSinConexion = 3;
    public const long CobradorSinConexion = 4;
    public const long Consulta = 5;
    public const long VendedorEnLinea = 6;
    public const long CobradorEnLinea = 7;
    public const long Superadmin = 8;
    public const long AuxiliarAdministrativo = 9;
}

/// <summary>
/// Fuente única de verdad de qué ruta puede abrir cada rol. La usan el menú web (NavMenu),
/// la barra inferior móvil y el guardia de rutas (MainLayout). El filtro del menú solo oculta;
/// el guardia es lo que impide entrar escribiendo la URL.
/// </summary>
public static class PermisosRol
{
    private const long Adm = RolesSistema.Administrador;
    private const long Sup = RolesSistema.Supervisor;
    private const long VOff = RolesSistema.VendedorSinConexion;
    private const long COff = RolesSistema.CobradorSinConexion;
    private const long Con = RolesSistema.Consulta;
    private const long VOn = RolesSistema.VendedorEnLinea;
    private const long COn = RolesSistema.CobradorEnLinea;
    private const long Sa = RolesSistema.Superadmin;
    private const long Aux = RolesSistema.AuxiliarAdministrativo;

    // Clave = primer segmento de la ruta ("" = Home). Valor = roles permitidos.
    // Superadmin se agrega siempre en PuedeAcceder, no hace falta repetirlo aquí.
    private static readonly Dictionary<string, long[]> RolesPorRuta = new(StringComparer.OrdinalIgnoreCase)
    {
        [""] = [Adm, Sup, Con, VOn, COn, Aux],
        ["clientes"] = [Adm, Sup, Con, VOn, COn, Aux],
        ["pagos"] = [Adm, Sup, Con, COn, Aux],
        ["reversion-pagos"] = [Adm, Sup],
        ["gastos"] = [Adm, Sup, Aux],
        ["pagos-comisiones"] = [Adm, Sup],
        ["vendedores-cobradores"] = [Adm, Sup, Aux],
        ["zonas-rutas"] = [Adm, Sup, Aux],
        ["productos"] = [Adm, Sup, Con, VOn, Aux],
        ["inventario"] = [Adm, Sup, Con, VOn, Aux],
        ["cartera-clientes"] = [Adm],
        ["operaciones-ventas"] = [Adm],
        ["parametros"] = [Adm],
        ["recordatorios-whatsapp"] = [Adm, Sup, COn, Aux],
        ["mensajes-whatsapp"] = [Adm, Sup, Aux],
        ["reportes"] = [Adm, Sup, Con, VOn, COn, Aux],
        ["estadisticas"] = [Adm, Sup, Con, Aux],
        ["roles-usuarios"] = [Adm],
        ["empresa"] = [Adm],
        ["jornada"] = [VOff, COff],
    };

    // Rutas que no dependen del rol (login, errores).
    private static readonly HashSet<string> RutasPublicas = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "not-found", "error"
    };

    /// <summary>Primer segmento de una ruta relativa: "clientes/5?x=1" -> "clientes".</summary>
    public static string Segmento(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa)) return "";
        var r = rutaRelativa.Trim();
        var corte = r.IndexOfAny(['?', '#']);
        if (corte >= 0) r = r[..corte];
        r = r.Trim('/');
        var barra = r.IndexOf('/');
        return barra >= 0 ? r[..barra] : r;
    }

    /// <summary>¿Puede el rol abrir esta ruta (relativa a la base, p. ej. "clientes/5")?</summary>
    public static bool PuedeAcceder(long? idRol, string? rutaRelativa)
    {
        var segmento = Segmento(rutaRelativa);
        if (RutasPublicas.Contains(segmento)) return true;
        if (idRol is null) return segmento.Length == 0;   // sin rol: solo Home
        if (idRol == Sa) return true;
        return RolesPorRuta.TryGetValue(segmento, out var roles) && roles.Contains(idRol.Value);
    }

    /// <summary>Ruta a la que se envía al usuario cuando intenta abrir algo que no le corresponde.</summary>
    public static string RutaInicio(long? idRol) =>
        idRol is RolesSistema.VendedorSinConexion or RolesSistema.CobradorSinConexion ? "jornada" : "";
}
