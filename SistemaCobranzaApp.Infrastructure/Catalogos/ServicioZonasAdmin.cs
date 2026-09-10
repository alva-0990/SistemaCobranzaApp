using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoZona
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ZonaDto? Zona { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoZona Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoZona NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoZona Ok(ZonaDto z) => new() { Exito = true, Zona = z };
}

public class ServicioZonasAdmin
{
    private readonly CobranzaDbContext _db;

    public ServicioZonasAdmin(CobranzaDbContext db) => _db = db;

    public Task<List<ZonaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct = default)
    {
        var q = _db.Zonas.AsNoTracking().AsQueryable();
        if (!incluirInactivas) q = q.Where(z => z.Estatus == Estatus.Activo);
        return q.OrderBy(z => z.Descripcion)
            .Select(z => new ZonaDto(z.IdZona, z.Descripcion, z.Estatus == Estatus.Activo))
            .ToListAsync(ct);
    }

    public async Task<ZonaDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var z = await _db.Zonas.AsNoTracking().FirstOrDefaultAsync(x => x.IdZona == id, ct);
        return z is null ? null : new ZonaDto(z.IdZona, z.Descripcion, z.Estatus == Estatus.Activo);
    }

    public async Task<ResultadoZona> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoZona.Fallo(error);

        var desc = descripcion.Trim();
        if (await _db.Zonas.AnyAsync(z => z.Descripcion == desc, ct))
            return ResultadoZona.Fallo("Ya existe una zona con esa descripción.");

        var zona = new Zona { Descripcion = desc, Estatus = Estatus.Activo };
        _db.Zonas.Add(zona);
        await _db.SaveChangesAsync(ct);
        return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, true));
    }

    public async Task<ResultadoZona> ActualizarAsync(long id, string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoZona.Fallo(error);

        var zona = await _db.Zonas.FirstOrDefaultAsync(z => z.IdZona == id, ct);
        if (zona is null) return ResultadoZona.NoExiste($"No se encontró la zona #{id}.");

        var desc = descripcion.Trim();
        if (await _db.Zonas.AnyAsync(z => z.Descripcion == desc && z.IdZona != id, ct))
            return ResultadoZona.Fallo("Ya existe otra zona con esa descripción.");

        zona.Descripcion = desc;
        await _db.SaveChangesAsync(ct);
        return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, zona.Estatus == Estatus.Activo));
    }

    public async Task<ResultadoZona> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var zona = await _db.Zonas.FirstOrDefaultAsync(z => z.IdZona == id, ct);
        if (zona is null) return ResultadoZona.NoExiste($"No se encontró la zona #{id}.");

        zona.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoZona.Ok(new ZonaDto(zona.IdZona, zona.Descripcion, false));
    }
}
