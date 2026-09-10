using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoRuta
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public RutaAdminDto? Ruta { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoRuta Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoRuta NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoRuta Ok(RutaAdminDto r) => new() { Exito = true, Ruta = r };
}

public class ServicioRutasAdmin
{
    private readonly CobranzaDbContext _db;

    public ServicioRutasAdmin(CobranzaDbContext db) => _db = db;

    public Task<List<RutaAdminDto>> ListarAsync(bool incluirInactivas, long? idZona, CancellationToken ct = default)
    {
        var q = from r in _db.Rutas.AsNoTracking()
                join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                select new { r, z };

        if (!incluirInactivas) q = q.Where(x => x.r.Estatus == Estatus.Activo);
        if (idZona is { } zona) q = q.Where(x => x.r.IdZona == zona);

        return q.OrderBy(x => x.r.Descripcion)
            .Select(x => new RutaAdminDto(x.r.IdRuta, x.r.Descripcion, x.r.IdZona,
                x.z != null ? x.z.Descripcion : null, x.r.Estatus == Estatus.Activo))
            .ToListAsync(ct);
    }

    public async Task<RutaAdminDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from r in _db.Rutas.AsNoTracking()
                          where r.IdRuta == id
                          join z in _db.Zonas.AsNoTracking() on r.IdZona equals z.IdZona into zj
                          from z in zj.DefaultIfEmpty()
                          select new { r, z }).FirstOrDefaultAsync(ct);
        return fila is null ? null
            : new RutaAdminDto(fila.r.IdRuta, fila.r.Descripcion, fila.r.IdZona,
                fila.z?.Descripcion, fila.r.Estatus == Estatus.Activo);
    }

    public async Task<ResultadoRuta> CrearAsync(string descripcion, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoRuta.Fallo(error);

        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoRuta.Fallo($"La zona #{z} no existe.");

        var ruta = new Ruta { Descripcion = descripcion.Trim(), IdZona = idZona, Estatus = Estatus.Activo };
        _db.Rutas.Add(ruta);
        await _db.SaveChangesAsync(ct);
        return ResultadoRuta.Ok((await ObtenerAsync(ruta.IdRuta, ct))!);
    }

    public async Task<ResultadoRuta> ActualizarAsync(long id, string descripcion, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoRuta.Fallo(error);

        var ruta = await _db.Rutas.FirstOrDefaultAsync(r => r.IdRuta == id, ct);
        if (ruta is null) return ResultadoRuta.NoExiste($"No se encontró la ruta #{id}.");

        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoRuta.Fallo($"La zona #{z} no existe.");

        ruta.Descripcion = descripcion.Trim();
        ruta.IdZona = idZona;
        await _db.SaveChangesAsync(ct);
        return ResultadoRuta.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<ResultadoRuta> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var ruta = await _db.Rutas.FirstOrDefaultAsync(r => r.IdRuta == id, ct);
        if (ruta is null) return ResultadoRuta.NoExiste($"No se encontró la ruta #{id}.");

        ruta.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoRuta.Ok((await ObtenerAsync(id, ct))!);
    }
}
