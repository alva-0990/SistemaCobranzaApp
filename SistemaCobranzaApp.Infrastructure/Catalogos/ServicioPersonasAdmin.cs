using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

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
    private const string Proceso = "ServicioCobradoresAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCobradoresAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // Listado operativo (solo activos) para selectores/combos comunes.
    public async Task<List<CobradorDto>> ListarActivasAsync(long? idZona, CancellationToken ct = default)
    {
        try
        {
            var q = from c in _db.Cobradores.AsNoTracking()
                where c.Estatus == Estatus.Activo
                    
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                    
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Cobrador
                join u in _db.Usuarios.AsNoTracking() on c.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                    
                select new { c, z, u };

            if (idZona is { } zona) q = q.Where(x => x.c.IdZona == zona);

            return await q.OrderBy(x => x.c.Nombre)
                .Select(x => new CobradorDto(
                    x.c.IdCobrador, 
                    x.c.Nombre, 
                    x.c.IdZona,
                    x.z != null ? x.z.Descripcion : null, 
                    x.c.IdUsuario,                         // <-- Asigna IdUsuario
                    x.u != null ? x.u.Usuario_ : null,     // <-- Asigna el username de texto
                    true                                   // <-- Estatus Activo
                ))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener los cobradores activos.");
            throw;
        }
    }


    public async Task<List<CobradorDto>> ListarAsync(bool? incluirInactivos, long? idZona, CancellationToken ct = default)
    {
        try
        {
            var incluir = incluirInactivos ?? false;
            var q = from c in _db.Cobradores.AsNoTracking()
                    
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                    
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Cobrador
                join u in _db.Usuarios.AsNoTracking() on c.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                    
                select new { c, z, u };

            if (!incluir) q = q.Where(x => x.c.Estatus == Estatus.Activo);
            if (idZona is { } zona) q = q.Where(x => x.c.IdZona == zona);

            return await q.OrderBy(x => x.c.Nombre)
                .Select(x => new CobradorDto(
                    x.c.IdCobrador, 
                    x.c.Nombre, 
                    x.c.IdZona,
                    x.z != null ? x.z.Descripcion : null, 
                    x.c.IdUsuario,                         // <-- Asigna IdUsuario
                    x.u != null ? x.u.Usuario_ : null,     // <-- Asigna el username de texto
                    x.c.Estatus == Estatus.Activo          // <-- Evalúa si está activo
                ))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { incluirInactivos, idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener los cobradores.");
            throw;
        }
    }


    public async Task<CobradorDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var fila = await (from c in _db.Cobradores.AsNoTracking()
                where c.IdCobrador == id
                              
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on c.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                              
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Cobrador
                join u in _db.Usuarios.AsNoTracking() on c.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                              
                select new { c, z, u }).FirstOrDefaultAsync(ct);

            return fila is null ? null
                : new CobradorDto(
                    fila.c.IdCobrador, 
                    fila.c.Nombre, 
                    fila.c.IdZona,
                    fila.z?.Descripcion, 
                    fila.c.IdUsuario,                      // <-- Asigna IdUsuario
                    fila.u?.Usuario_,                      // <-- Asigna el username de texto
                    fila.c.Estatus == Estatus.Activo       // <-- Evalúa si está activo
                );
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el cobrador #{id}.");
            throw;
        }
    }


    public async Task<ResultadoCobrador> CrearAsync(string nombre, long? idZona, CancellationToken ct = default,
        long? idUsuario = null)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoCobrador.Fallo(error);

        try
        {
            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoCobrador.Fallo($"La zona #{z} no existe.");

            if (idUsuario is { } u && !await _db.Usuarios.AnyAsync(x => x.IdUsuario == u, ct))
                return ResultadoCobrador.Fallo($"El usuario #{u} no existe.");

            var cob = new Cobrador { Nombre = nombre.Trim(), IdZona = idZona, IdUsuario = idUsuario, Estatus = Estatus.Activo };
            _db.Cobradores.Add(cob);
            await _db.SaveChangesAsync(ct);
            return ResultadoCobrador.Ok((await ObtenerAsync(cob.IdCobrador, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { nombre, idZona });
            return ResultadoCobrador.Fallo("Error en la base de datos al crear el cobrador.");
        }
    }

    public async Task<ResultadoCobrador> ActualizarAsync(long id, string nombre, long? idZona, bool activo,
        CancellationToken ct = default, long? idUsuario = null)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoCobrador.Fallo(error);

        try
        {
            var cob = await _db.Cobradores.FirstOrDefaultAsync(c => c.IdCobrador == id, ct);
            if (cob is null) return ResultadoCobrador.NoExiste($"No se encontró el cobrador #{id}.");
            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoCobrador.Fallo($"La zona #{z} no existe.");

            if (idUsuario is { } u && !await _db.Usuarios.AnyAsync(x => x.IdUsuario == u, ct))
                return ResultadoCobrador.Fallo($"El usuario #{u} no existe.");

            cob.Nombre = nombre.Trim();
            cob.IdZona = idZona;
            cob.IdUsuario = idUsuario;
            cob.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoCobrador.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, nombre, idZona, activo });
            return ResultadoCobrador.Fallo("Error en la base de datos al actualizar el cobrador.");
        }
    }

    public async Task<ResultadoCobrador> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var cob = await _db.Cobradores.FirstOrDefaultAsync(c => c.IdCobrador == id, ct);
            if (cob is null) return ResultadoCobrador.NoExiste($"No se encontró el cobrador #{id}.");
            cob.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoCobrador.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoCobrador.Fallo("Error en la base de datos al dar de baja el cobrador.");
        }
    }

    public async Task<ResultadoCobrador> CambiarEstatusAsync(
        long id,
        bool activo,
        CancellationToken ct = default)
    {
        var cob = await _db.Cobradores
            .FirstOrDefaultAsync(c => c.IdCobrador == id, ct);

        if (cob is null)
            return ResultadoCobrador.NoExiste(
                $"No se encontró el cobrador #{id}.");

        cob.Estatus = activo
            ? Estatus.Activo
            : Estatus.Inactivo;

        await _db.SaveChangesAsync(ct);

        return ResultadoCobrador.Ok(
            (await ObtenerAsync(id, ct))!);
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
    private const string Proceso = "ServicioVendedoresAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioVendedoresAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // Listado operativo (solo activos) para selectores/combos comunes.
    public async Task<List<VendedorDto>> ListarActivasAsync(long? idZona, CancellationToken ct = default)
    {
        try
        {
            var q = from v in _db.Vendedores.AsNoTracking()
                where v.Estatus == Estatus.Activo
                    
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                    
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Vendedor
                join u in _db.Usuarios.AsNoTracking() on v.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                    
                select new { v, z, u };

            if (idZona is { } zona) q = q.Where(x => x.v.IdZona == zona);

            return await q.OrderBy(x => x.v.Nombre)
                .Select(x => new VendedorDto(
                    x.v.IdVendedor, 
                    x.v.Nombre, 
                    x.v.IdZona,
                    x.z != null ? x.z.Descripcion : null, 
                    x.v.IdUsuario,                         // <-- Asigna IdUsuario
                    x.u != null ? x.u.Usuario_ : null,     // <-- Asigna el identificador/username de texto
                    true                                   // <-- Estatus Activo
                ))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener los vendedores activos.");
            throw;
        }
    }


    public async Task<List<VendedorDto>> ListarAsync(bool? incluirInactivos, long? idZona, CancellationToken ct = default)
    {
        try
        {
            var incluir = incluirInactivos ?? false;
            var q = from v in _db.Vendedores.AsNoTracking()
                    
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                    
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Vendedor
                join u in _db.Usuarios.AsNoTracking() on v.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                    
                select new { v, z, u };

            if (!incluir) q = q.Where(x => x.v.Estatus == Estatus.Activo);
            if (idZona is { } zona) q = q.Where(x => x.v.IdZona == zona);

            return await q.OrderBy(x => x.v.Nombre)
                .Select(x => new VendedorDto(
                    x.v.IdVendedor, 
                    x.v.Nombre, 
                    x.v.IdZona,
                    x.z != null ? x.z.Descripcion : null, 
                    x.v.IdUsuario,                         // <-- Asigna IdUsuario
                    x.u != null ? x.u.Usuario_ : null,     // <-- Asigna el identificador/username de texto
                    x.v.Estatus == Estatus.Activo          // <-- Evalúa si está activo dinámicamente
                ))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { incluirInactivos, idZona });
            _log.Error(Proceso, "Error en la base de datos al obtener los vendedores.");
            throw;
        }
    }


    public async Task<VendedorDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var fila = await (from v in _db.Vendedores.AsNoTracking()
                where v.IdVendedor == id
                              
                // Cruce para obtener la descripción de la Zona
                join z in _db.Zonas.AsNoTracking() on v.IdZona equals z.IdZona into zj
                from z in zj.DefaultIfEmpty()
                              
                // NUEVO: Cruce para obtener la información de la cuenta de Login del Vendedor
                join u in _db.Usuarios.AsNoTracking() on v.IdUsuario equals u.IdUsuario into uj
                from u in uj.DefaultIfEmpty()
                              
                select new { v, z, u }).FirstOrDefaultAsync(ct);

            return fila is null ? null
                : new VendedorDto(
                    fila.v.IdVendedor, 
                    fila.v.Nombre, 
                    fila.v.IdZona,
                    fila.z?.Descripcion, 
                    fila.v.IdUsuario,                      // <-- Asigna IdUsuario
                    fila.u?.Usuario_,                      // <-- Asigna el username de texto
                    fila.v.Estatus == Estatus.Activo       // <-- Evalúa si está activo
                );
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el vendedor #{id}.");
            throw;
        }
    }


    public async Task<ResultadoVendedor> CrearAsync(string nombre, long? idZona, CancellationToken ct = default,
        long? idUsuario = null)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoVendedor.Fallo(error);

        try
        {
            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoVendedor.Fallo($"La zona #{z} no existe.");

            if (idUsuario is { } u && !await _db.Usuarios.AnyAsync(x => x.IdUsuario == u, ct))
                return ResultadoVendedor.Fallo($"El usuario #{u} no existe.");

            var vend = new Vendedor { Nombre = nombre.Trim(), IdZona = idZona, IdUsuario = idUsuario, Estatus = Estatus.Activo };
            _db.Vendedores.Add(vend);
            await _db.SaveChangesAsync(ct);
            return ResultadoVendedor.Ok((await ObtenerAsync(vend.IdVendedor, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { nombre, idZona });
            return ResultadoVendedor.Fallo("Error en la base de datos al crear el vendedor.");
        }
    }

    public async Task<ResultadoVendedor> ActualizarAsync(long id, string nombre, long? idZona, bool activo,
        CancellationToken ct = default, long? idUsuario = null)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(nombre, "El nombre")
                    ?? ValidacionCatalogo.ValidarZonaOpcional(idZona);
        if (error is not null) return ResultadoVendedor.Fallo(error);

        try
        {
            var vend = await _db.Vendedores.FirstOrDefaultAsync(v => v.IdVendedor == id, ct);
            if (vend is null) return ResultadoVendedor.NoExiste($"No se encontró el vendedor #{id}.");
            if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
                return ResultadoVendedor.Fallo($"La zona #{z} no existe.");

            if (idUsuario is { } u && !await _db.Usuarios.AnyAsync(x => x.IdUsuario == u, ct))
                return ResultadoVendedor.Fallo($"El usuario #{u} no existe.");

            vend.Nombre = nombre.Trim();
            vend.IdZona = idZona;
            vend.IdUsuario = idUsuario;
            vend.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoVendedor.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, nombre, idZona });
            return ResultadoVendedor.Fallo("Error en la base de datos al actualizar el vendedor.");
        }
    }

    public async Task<ResultadoVendedor> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var vend = await _db.Vendedores.FirstOrDefaultAsync(v => v.IdVendedor == id, ct);
            if (vend is null) return ResultadoVendedor.NoExiste($"No se encontró el vendedor #{id}.");
            vend.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoVendedor.Ok((await ObtenerAsync(id, ct))!);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoVendedor.Fallo("Error en la base de datos al dar de baja el vendedor.");
        }
    }

    public async Task<ResultadoVendedor> CambiarEstatusAsync(
        long id,
        bool activo,
        CancellationToken ct = default)
    {
        var vend = await _db.Vendedores
            .FirstOrDefaultAsync(v => v.IdVendedor == id, ct);

        if (vend is null)
            return ResultadoVendedor.NoExiste(
                $"No se encontró el vendedor #{id}.");

        vend.Estatus = activo
            ? Estatus.Activo
            : Estatus.Inactivo;

        await _db.SaveChangesAsync(ct);

        return ResultadoVendedor.Ok(
            (await ObtenerAsync(id, ct))!);
    }
    }

    

