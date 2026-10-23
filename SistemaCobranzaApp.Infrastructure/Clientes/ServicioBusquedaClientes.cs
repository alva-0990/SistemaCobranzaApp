using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Clientes;

/// <summary>
/// Búsqueda y consulta de clientes (solo lectura). Separado de ServicioBusqueda
/// (que queda únicamente para ventas y pagos).
/// Incluye:
///   - Búsqueda por texto: nombre, apellidos, teléfono, email, dirección,
///     código postal, ruta y zona (todo por texto, LIKE case-insensitive).
///   - Consulta de clientes por ruta.
///   - Consulta de clientes por zona (agrupados por sus rutas).
/// </summary>
public class ServicioBusquedaClientes
{
    private readonly CobranzaDbContext _db;

    public ServicioBusquedaClientes(CobranzaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Busca clientes activos por nombre/apPaterno/apMaterno/teléfono/email/dirección/código postal
    /// y por ruta/zona (todos por su descripción en texto, LIKE case-insensitive).
    /// Devuelve lista vacía si no se envía ningún criterio.
    /// </summary>
    public async Task<List<ClienteDto>> BuscarAsync(ClienteBusquedaFiltro f, CancellationToken ct = default)
    {
        // El filtro es válido si incluye al menos un criterio de texto.
        if (string.IsNullOrWhiteSpace(f.Nombre)
            && string.IsNullOrWhiteSpace(f.ApPaterno)
            && string.IsNullOrWhiteSpace(f.ApMaterno)
            && string.IsNullOrWhiteSpace(f.Telefono)
            && string.IsNullOrWhiteSpace(f.Email)
            && string.IsNullOrWhiteSpace(f.Direccion)
            && string.IsNullOrWhiteSpace(f.CodigoPostal)
            && string.IsNullOrWhiteSpace(f.Ruta)
            && string.IsNullOrWhiteSpace(f.Zona))
            return new List<ClienteDto>();

        var q = BaseQuery();

        if (!string.IsNullOrWhiteSpace(f.Nombre))
            q = q.Where(x => EF.Functions.ILike(x.c.Nombre, BusquedaHelper.ParaLike(f.Nombre)));

        if (!string.IsNullOrWhiteSpace(f.ApPaterno))
            q = q.Where(x => EF.Functions.ILike(x.c.ApPaterno ?? "", BusquedaHelper.ParaLike(f.ApPaterno)));

        if (!string.IsNullOrWhiteSpace(f.ApMaterno))
            q = q.Where(x => EF.Functions.ILike(x.c.ApMaterno ?? "", BusquedaHelper.ParaLike(f.ApMaterno)));

        if (!string.IsNullOrWhiteSpace(f.Telefono))
            q = q.Where(x => EF.Functions.ILike(x.c.TelefonoPrincipal, BusquedaHelper.ParaLike(f.Telefono))
                          || EF.Functions.ILike(x.c.TelefonoSecundario ?? "", BusquedaHelper.ParaLike(f.Telefono)));

        if (!string.IsNullOrWhiteSpace(f.Email))
            q = q.Where(x => EF.Functions.ILike(x.c.Email ?? "", BusquedaHelper.ParaLike(f.Email)));

        if (!string.IsNullOrWhiteSpace(f.Direccion))
            q = q.Where(x => EF.Functions.ILike(x.c.Direccion, BusquedaHelper.ParaLike(f.Direccion)));

        if (!string.IsNullOrWhiteSpace(f.CodigoPostal))
            q = q.Where(x => EF.Functions.ILike(x.c.CodigoPostal ?? "", BusquedaHelper.ParaLike(f.CodigoPostal)));

        // Ruta y zona se buscan por su descripción (texto), no por Id.
        if (!string.IsNullOrWhiteSpace(f.Ruta))
            q = q.Where(x => EF.Functions.ILike(x.r != null ? x.r.Descripcion : "", BusquedaHelper.ParaLike(f.Ruta)));

        if (!string.IsNullOrWhiteSpace(f.Zona))
            q = q.Where(x => EF.Functions.ILike(x.z != null ? x.z.Descripcion : "", BusquedaHelper.ParaLike(f.Zona)));

        var filas = await q
            .OrderBy(x => x.c.Nombre).ThenBy(x => x.c.ApPaterno).ThenBy(x => x.c.ApMaterno).ThenBy(x => x.c.IdCliente)
            .ToListAsync(ct);

        return filas.Select(Mapear).ToList();
    }

    /// <summary>Lista los clientes activos de una ruta (por Id de ruta).</summary>
    public async Task<ClientesPorRutaDto?> PorRutaAsync(long idRuta, CancellationToken ct = default)
    {
        var ruta = await _db.Rutas.AsNoTracking().FirstOrDefaultAsync(r => r.IdRuta == idRuta, ct);
        if (ruta is null)
            return null;

        var zona = ruta.IdZona is { } idZona
            ? await _db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.IdZona == idZona, ct)
            : null;

        var filas = await BaseQuery()
            .Where(x => x.c.IdRuta == idRuta)
            .OrderBy(x => x.c.Nombre).ThenBy(x => x.c.ApPaterno).ThenBy(x => x.c.ApMaterno).ThenBy(x => x.c.IdCliente)
            .ToListAsync(ct);

        var clientes = filas.Select(Mapear).ToList();
        return new ClientesPorRutaDto(ruta.IdRuta, ruta.Descripcion, ruta.IdZona, zona?.Descripcion, clientes);
    }

    /// <summary>Lista los clientes activos de una zona, agrupados por sus rutas (por Id de zona).</summary>
    public async Task<ClientesPorZonaDto?> PorZonaAsync(long idZona, CancellationToken ct = default)
    {
        var zona = await _db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.IdZona == idZona, ct);
        if (zona is null)
            return null;

        var filas = await BaseQuery()
            .Where(x => x.r != null && x.r.IdZona == idZona)
            .OrderBy(x => x.r != null ? x.r.Descripcion : null)
            .ThenBy(x => x.c.Nombre).ThenBy(x => x.c.ApPaterno).ThenBy(x => x.c.ApMaterno).ThenBy(x => x.c.IdCliente)
            .ToListAsync(ct);

        var rutas = filas
            .GroupBy(x => new { x.c.IdRuta, Descripcion = x.r?.Descripcion })
            .Select(g => new ClientesPorRutaDto(
                g.Key.IdRuta,
                g.Key.Descripcion,
                idZona,
                zona.Descripcion,
                g.Select(Mapear).ToList()))
            .ToList();

        return new ClientesPorZonaDto(zona.IdZona, zona.Descripcion, rutas);
    }

    // ---------------- helpers ----------------

    // Proyección a un tipo con propiedades autoimplementadas (traducible por EF). Un record con
    // constructor posicional provoca que EF no pueda traducir filtros ILike aplicados después
    // de la proyección, así que la conversión a Fila se hace solo al materializar en memoria.
    private IQueryable<FilaProyectada> BaseQuery() =>
        from c in _db.Clientes.AsNoTracking()
        join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
        from r in rj.DefaultIfEmpty()
        join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
        from z in zj.DefaultIfEmpty()
        join v in _db.Vendedores.AsNoTracking() on c.IdVendedor equals v.IdVendedor into vj
        from v in vj.DefaultIfEmpty()
        join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
        from cl in clj.DefaultIfEmpty()
        where c.Estatus == Domain.Enums.Estatus.Activo
        select new FilaProyectada { c = c, r = r, z = z, v = v, cl = cl };

    // Tipo con propiedades autoimplementadas (en vez de un record posicional): EF lo trata como
    // una proyección estándar y puede traducir los Where/OrderBy que vienen después.
    private sealed class FilaProyectada
    {
        public Cliente c { get; set; } = default!;
        public Ruta? r { get; set; }
        public Zona? z { get; set; }
        public Vendedor? v { get; set; }
        public CatalogoClasificacion? cl { get; set; }
    }

    private static ClienteDto Mapear(FilaProyectada x) => new(
        x.c.IdCliente, x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno,
        BusquedaHelper.NombreCompleto(x.c.Nombre, x.c.ApPaterno, x.c.ApMaterno),
        x.c.TelefonoPrincipal, x.c.TelefonoSecundario, x.c.Email,
        x.c.Direccion, x.c.CodigoPostal, x.c.ReferenciaUbicacion,
        x.c.Latitud, x.c.Longitud,
        x.c.IdRuta, x.r?.Descripcion, x.r?.IdZona,
        x.c.IdVendedor, x.v?.Nombre,
        x.c.IdClasificacion, x.cl?.Descripcion,
        (int)x.c.Estatus, x.c.Observaciones, x.c.FechaAlta);
}
