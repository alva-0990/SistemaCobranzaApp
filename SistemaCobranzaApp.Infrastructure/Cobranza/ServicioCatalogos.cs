using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Consulta de catálogos de apoyo (para llenar combos en la UI).
/// Solo devuelve elementos activos.
/// </summary>
public class ServicioCatalogos
{
    private readonly CobranzaDbContext _db;

    public ServicioCatalogos(CobranzaDbContext db)
    {
        _db = db;
    }

    public Task<List<CatalogoItemDto>> ZonasAsync(CancellationToken ct = default) =>
        _db.Zonas.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.Descripcion)
            .Select(x => new CatalogoItemDto(x.IdZona, x.Descripcion)).ToListAsync(ct);

    public Task<List<CatalogoItemDto>> CondicionesAsync(CancellationToken ct = default) =>
        _db.Condiciones.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.Descripcion)
            .Select(x => new CatalogoItemDto(x.IdCondicion, x.Descripcion)).ToListAsync(ct);

    public Task<List<CatalogoItemDto>> ClasificacionesAsync(CancellationToken ct = default) =>
        _db.Clasificaciones.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.Descripcion)
            .Select(x => new CatalogoItemDto(x.IdClasificacion, x.Descripcion)).ToListAsync(ct);

    public Task<List<CatalogoItemDto>> MovimientosAsync(CancellationToken ct = default) =>
        _db.Movimientos.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.IdMovimiento)
            .Select(x => new CatalogoItemDto(x.IdMovimiento, x.Descripcion)).ToListAsync(ct);

    public Task<List<CatalogoItemDto>> EstatusVentaAsync(CancellationToken ct = default) =>
        _db.EstatusVenta.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.IdEstatusVenta)
            .Select(x => new CatalogoItemDto(x.IdEstatusVenta, x.Descripcion)).ToListAsync(ct);

    public Task<List<RutaDto>> RutasAsync(CancellationToken ct = default) =>
        _db.Rutas.AsNoTracking().Where(x => x.Estatus == Estatus.Activo)
            .OrderBy(x => x.Descripcion)
            .Select(x => new RutaDto(x.IdRuta, x.Descripcion, x.IdZona)).ToListAsync(ct);

    public Task<List<PersonaZonaDto>> VendedoresAsync(long? idZona, CancellationToken ct = default)
    {
        var q = _db.Vendedores.AsNoTracking().Where(x => x.Estatus == Estatus.Activo);
        if (idZona is { } z) q = q.Where(x => x.IdZona == z);
        return q.OrderBy(x => x.Nombre)
            .Select(x => new PersonaZonaDto(x.IdVendedor, x.Nombre, x.IdZona)).ToListAsync(ct);
    }

    /// <summary>
    /// Cobradores activos, opcionalmente filtrados por zona.
    /// Replica CargarCobradores de VistaPagoAgregar.
    /// </summary>
    public Task<List<PersonaZonaDto>> CobradoresAsync(long? idZona, CancellationToken ct = default)
    {
        var q = _db.Cobradores.AsNoTracking().Where(x => x.Estatus == Estatus.Activo);
        if (idZona is { } z) q = q.Where(x => x.IdZona == z);
        return q.OrderBy(x => x.Nombre)
            .Select(x => new PersonaZonaDto(x.IdCobrador, x.Nombre, x.IdZona)).ToListAsync(ct);
    }
}
