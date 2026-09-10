using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoCobrador
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CobradorDto? Cobrador { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoCobrador Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoCobrador NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoCobrador Ok(CobradorDto c) => new() { Exito = true, Cobrador = c };
}

public class ServicioCobradoresAdmin
{
    private readonly CobranzaDbContext _db;
    public ServicioCobradoresAdmin(CobranzaDbContext db) => _db = db;

    public Task<List<CobradorDto>> ListarAsync(bool incluirInactivos, long? idZona, CancellationToken ct = default)
    {
        var q = from c in _db.Cobradores.AsNoTracking()
                join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                select new { c, z };
        if (!incluirInactivos) q = q.Where(x => x.c.Estatus == Estatus.Activo);
        if (idZona is { } zona) q = q.Where(x => x.c.IdZona == zona);
        return q.OrderBy(x => x.c.Nombre)
            .Select(x => new CobradorDto(x.c.IdCobrador, x.c.Nombre, x.c.IdZona,
                x.z != null ? x.z.Descripcion : null, x.c.Estatus == Estatus.Activo))
            .ToListAsync(ct);
    }

    public async Task<CobradorDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from c in _db.Cobradores.AsNoTracking()
                          where c.IdCobrador == id
                          join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                          from z in zj.DefaultIfEmpty()
                          select new { c, z }).FirstOrDefaultAsync(ct);
        return fila is null ? null
            : new CobradorDto(fila.c.IdCobrador, fila.c.Nombre, fila.c.IdZona,
                fila.z?.Descripcion, fila.c.Estatus == Estatus.Activo);
    }

    public async Task<ResultadoCobrador> CrearAsync(string nombre, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoCobrador.Fallo(error);
        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoCobrador.Fallo($"La zona #{z} no existe.");

        var cob = new Cobrador { Nombre = nombre.Trim(), IdZona = idZona, Estatus = Estatus.Activo };
        _db.Cobradores.Add(cob);
        await _db.SaveChangesAsync(ct);
        return ResultadoCobrador.Ok((await ObtenerAsync(cob.IdCobrador, ct))!);
    }

    public async Task<ResultadoCobrador> ActualizarAsync(long id, string nombre, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoCobrador.Fallo(error);

        var cob = await _db.Cobradores.FirstOrDefaultAsync(c => c.IdCobrador == id, ct);
        if (cob is null) return ResultadoCobrador.NoExiste($"No se encontró el cobrador #{id}.");
        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoCobrador.Fallo($"La zona #{z} no existe.");

        cob.Nombre = nombre.Trim();
        cob.IdZona = idZona;
        await _db.SaveChangesAsync(ct);
        return ResultadoCobrador.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<ResultadoCobrador> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var cob = await _db.Cobradores.FirstOrDefaultAsync(c => c.IdCobrador == id, ct);
        if (cob is null) return ResultadoCobrador.NoExiste($"No se encontró el cobrador #{id}.");
        cob.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoCobrador.Ok((await ObtenerAsync(id, ct))!);
    }
}

public class ResultadoVendedor
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public VendedorDto? Vendedor { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoVendedor Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoVendedor NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoVendedor Ok(VendedorDto v) => new() { Exito = true, Vendedor = v };
}

public class ServicioVendedoresAdmin
{
    private readonly CobranzaDbContext _db;
    public ServicioVendedoresAdmin(CobranzaDbContext db) => _db = db;

    public Task<List<VendedorDto>> ListarAsync(bool incluirInactivos, long? idZona, CancellationToken ct = default)
    {
        var q = from v in _db.Vendedores.AsNoTracking()
                join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                select new { v, z };
        if (!incluirInactivos) q = q.Where(x => x.v.Estatus == Estatus.Activo);
        if (idZona is { } zona) q = q.Where(x => x.v.IdZona == zona);
        return q.OrderBy(x => x.v.Nombre)
            .Select(x => new VendedorDto(x.v.IdVendedor, x.v.Nombre, x.v.IdZona,
                x.z != null ? x.z.Descripcion : null, x.v.Estatus == Estatus.Activo))
            .ToListAsync(ct);
    }

    public async Task<VendedorDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from v in _db.Vendedores.AsNoTracking()
                          where v.IdVendedor == id
                          join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                          from z in zj.DefaultIfEmpty()
                          select new { v, z }).FirstOrDefaultAsync(ct);
        return fila is null ? null
            : new VendedorDto(fila.v.IdVendedor, fila.v.Nombre, fila.v.IdZona,
                fila.z?.Descripcion, fila.v.Estatus == Estatus.Activo);
    }

    public async Task<ResultadoVendedor> CrearAsync(string nombre, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoVendedor.Fallo(error);
        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoVendedor.Fallo($"La zona #{z} no existe.");

        var vend = new Vendedor { Nombre = nombre.Trim(), IdZona = idZona, Estatus = Estatus.Activo };
        _db.Vendedores.Add(vend);
        await _db.SaveChangesAsync(ct);
        return ResultadoVendedor.Ok((await ObtenerAsync(vend.IdVendedor, ct))!);
    }

    public async Task<ResultadoVendedor> ActualizarAsync(long id, string nombre, long? idZona, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoVendedor.Fallo(error);

        var vend = await _db.Vendedores.FirstOrDefaultAsync(v => v.IdVendedor == id, ct);
        if (vend is null) return ResultadoVendedor.NoExiste($"No se encontró el vendedor #{id}.");
        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return ResultadoVendedor.Fallo($"La zona #{z} no existe.");

        vend.Nombre = nombre.Trim();
        vend.IdZona = idZona;
        await _db.SaveChangesAsync(ct);
        return ResultadoVendedor.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<ResultadoVendedor> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var vend = await _db.Vendedores.FirstOrDefaultAsync(v => v.IdVendedor == id, ct);
        if (vend is null) return ResultadoVendedor.NoExiste($"No se encontró el vendedor #{id}.");
        vend.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoVendedor.Ok((await ObtenerAsync(id, ct))!);
    }
}
