using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Inventario;

public class ResultadoCategoria
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CategoriaDto? Categoria { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoCategoria Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoCategoria NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoCategoria Ok(CategoriaDto c) => new() { Exito = true, Categoria = c };
}

public class ResultadoMarca
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public MarcaDto? Marca { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoMarca Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoMarca NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoMarca Ok(MarcaDto m) => new() { Exito = true, Marca = m };
}

public class ResultadoColor
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ColorDto? Color { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoColor Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoColor NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoColor Ok(ColorDto c) => new() { Exito = true, Color = c };
}

public class ResultadoTalla
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public TallaDto? Talla { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoTalla Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoTalla NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoTalla Ok(TallaDto t) => new() { Exito = true, Talla = t };
}

public class ServicioCatalogosProductosAdmin
{
    private const string Proceso = "ServicioCatalogosProductosAdmin (BD)";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCatalogosProductosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // =====================================================================
    // 1. CATEGORÍAS
    // =====================================================================
    public async Task<List<CategoriaDto>> ListarCategoriasActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoCategorias.AsNoTracking()
                .Where(x => x.Estatus == Estatus.Activo)
                .OrderBy(x => x.Descripcion)
                .Select(x => new CategoriaDto(x.IdCategoria, x.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener categorías activas.");
            throw;
        }
    }

    public async Task<List<CategoriaDto>> ListarCategoriasTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoCategorias.AsNoTracking().IgnoreQueryFilters()
                .OrderBy(x => x.Descripcion)
                .Select(x => new CategoriaDto(x.IdCategoria, x.Descripcion, x.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener todas las categorías.");
            throw;
        }
    }

    public async Task<CategoriaDto?> ObtenerCategoriaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var x = await _db.CatalogoCategorias.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.IdCategoria == id, ct);
            return x is null ? null : new CategoriaDto(x.IdCategoria, x.Descripcion, x.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<ResultadoCategoria> CrearCategoriaAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCategoria.Fallo(error);
        var desc = descripcion.Trim();
        try
        {
            if (await _db.CatalogoCategorias.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc, ct))
                return ResultadoCategoria.Fallo("Ya existe una categoría con esa descripción.");

            var c = new CatalogoCategoria { Descripcion = desc, Estatus = Estatus.Activo };
            _db.CatalogoCategorias.Add(c);
            await _db.SaveChangesAsync(ct);
            return ResultadoCategoria.Ok(new CategoriaDto(c.IdCategoria, c.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoCategoria.Fallo("Error en la base de datos al crear la categoría.");
        }
    }

    public async Task<ResultadoCategoria> ActualizarCategoriaAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCategoria.Fallo(error);
        try
        {
            var c = await _db.CatalogoCategorias.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdCategoria == id, ct);
            if (c is null) return ResultadoCategoria.NoExiste($"No se encontró la categoría #{id}.");

            var desc = descripcion.Trim();
            if (await _db.CatalogoCategorias.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.IdCategoria != id, ct))
                return ResultadoCategoria.Fallo("Ya existe otra categoría con esa descripción.");

            c.Descripcion = desc;
            c.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoCategoria.Ok(new CategoriaDto(c.IdCategoria, c.Descripcion, c.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoCategoria.Fallo("Error en la base de datos al actualizar la categoría.");
        }
    }

    public async Task<ResultadoCategoria> DarDeBajaCategoriaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var c = await _db.CatalogoCategorias.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdCategoria == id, ct);
            if (c is null) return ResultadoCategoria.NoExiste($"No se encontró la categoría #{id}.");

            c.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoCategoria.Ok(new CategoriaDto(c.IdCategoria, c.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoCategoria.Fallo("Error en la base de datos al dar de baja la categoría.");
        }
    }
    
        // =====================================================================
    // 2. MARCAS
    // =====================================================================
    public async Task<List<MarcaDto>> ListarMarcasActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoMarcas.AsNoTracking()
                .Where(x => x.Estatus == Estatus.Activo)
                .OrderBy(x => x.Descripcion)
                .Select(x => new MarcaDto(x.IdMarca, x.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener marcas activas.");
            throw;
        }
    }

    public async Task<List<MarcaDto>> ListarMarcasTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoMarcas.AsNoTracking().IgnoreQueryFilters()
                .OrderBy(x => x.Descripcion)
                .Select(x => new MarcaDto(x.IdMarca, x.Descripcion, x.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener todas las marcas.");
            throw;
        }
    }

    public async Task<MarcaDto?> ObtenerMarcaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var x = await _db.CatalogoMarcas.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.IdMarca == id, ct);
            return x is null ? null : new MarcaDto(x.IdMarca, x.Descripcion, x.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<ResultadoMarca> CrearMarcaAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMarca.Fallo(error);
        var desc = descripcion.Trim();
        try
        {
            if (await _db.CatalogoMarcas.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc, ct))
                return ResultadoMarca.Fallo("Ya existe una marca con esa descripción.");

            var m = new CatalogoMarca { Descripcion = desc, Estatus = Estatus.Activo };
            _db.CatalogoMarcas.Add(m);
            await _db.SaveChangesAsync(ct);
            return ResultadoMarca.Ok(new MarcaDto(m.IdMarca, m.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoMarca.Fallo("Error en la base de datos al crear la marca.");
        }
    }

    public async Task<ResultadoMarca> ActualizarMarcaAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMarca.Fallo(error);
        try
        {
            var m = await _db.CatalogoMarcas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdMarca == id, ct);
            if (m is null) return ResultadoMarca.NoExiste($"No se encontró la marca #{id}.");

            var desc = descripcion.Trim();
            if (await _db.CatalogoMarcas.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.IdMarca != id, ct))
                return ResultadoMarca.Fallo("Ya existe otra marca con esa descripción.");

            m.Descripcion = desc;
            m.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoMarca.Ok(new MarcaDto(m.IdMarca, m.Descripcion, m.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoMarca.Fallo("Error en la base de datos al actualizar la marca.");
        }
    }

    public async Task<ResultadoMarca> DarDeBajaMarcaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var m = await _db.CatalogoMarcas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdMarca == id, ct);
            if (m is null) return ResultadoMarca.NoExiste($"No se encontró la marca #{id}.");

            m.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoMarca.Ok(new MarcaDto(m.IdMarca, m.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoMarca.Fallo("Error en la base de datos al dar de baja la marca.");
        }
    }

    // =====================================================================
    // 3. COLORES
    // =====================================================================
    public async Task<List<ColorDto>> ListarColoresActivosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoColores.AsNoTracking()
                .Where(x => x.Estatus == Estatus.Activo)
                .OrderBy(x => x.Descripcion)
                .Select(x => new ColorDto(x.IdColor, x.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener colores activos.");
            throw;
        }
    }

    public async Task<List<ColorDto>> ListarColoresTodosAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoColores.AsNoTracking().IgnoreQueryFilters()
                .OrderBy(x => x.Descripcion)
                .Select(x => new ColorDto(x.IdColor, x.Descripcion, x.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener todos los colores.");
            throw;
        }
    }

    public async Task<ColorDto?> ObtenerColorAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var x = await _db.CatalogoColores.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.IdColor == id, ct);
            return x is null ? null : new ColorDto(x.IdColor, x.Descripcion, x.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<ResultadoColor> CrearColorAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoColor.Fallo(error);
        var desc = descripcion.Trim();
        try
        {
            if (await _db.CatalogoColores.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc, ct))
                return ResultadoColor.Fallo("Ya existe un color con esa descripción.");

            var c = new CatalogoColor { Descripcion = desc, Estatus = Estatus.Activo };
            _db.CatalogoColores.Add(c);
            await _db.SaveChangesAsync(ct);
            return ResultadoColor.Ok(new ColorDto(c.IdColor, c.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoColor.Fallo("Error en la base de datos al crear el color.");
        }
    }

    public async Task<ResultadoColor> ActualizarColorAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoColor.Fallo(error);
        try
        {
            var c = await _db.CatalogoColores.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdColor == id, ct);
            if (c is null) return ResultadoColor.NoExiste($"No se encontró el color #{id}.");

            var desc = descripcion.Trim();
            if (await _db.CatalogoColores.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.IdColor != id, ct))
                return ResultadoColor.Fallo("Ya existe otro color con esa descripción.");

            c.Descripcion = desc;
            c.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoColor.Ok(new ColorDto(c.IdColor, c.Descripcion, c.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoColor.Fallo("Error en la base de datos al actualizar el color.");
        }
    }

    public async Task<ResultadoColor> DarDeBajaColorAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var c = await _db.CatalogoColores.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdColor == id, ct);
            if (c is null) return ResultadoColor.NoExiste($"No se encontró el color #{id}.");

            c.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoColor.Ok(new ColorDto(c.IdColor, c.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoColor.Fallo("Error en la base de datos al dar de baja el color.");
        }
    }

    // =====================================================================
    // 4. TALLAS
    // =====================================================================
    public async Task<List<TallaDto>> ListarTallasActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoTallas.AsNoTracking()
                .Where(x => x.Estatus == Estatus.Activo)
                .OrderBy(x => x.Tipo).ThenBy(x => x.Descripcion)
                .Select(x => new TallaDto(x.IdTalla, x.Descripcion, x.Tipo, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener tallas activas.");
            throw;
        }
    }

    public async Task<List<TallaDto>> ListarTallasTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoTallas.AsNoTracking().IgnoreQueryFilters()
                .OrderBy(x => x.Tipo).ThenBy(x => x.Descripcion)
                .Select(x => new TallaDto(x.IdTalla, x.Descripcion, x.Tipo, x.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, "Error al obtener todas las tallas.");
            throw;
        }
    }

    public async Task<TallaDto?> ObtenerTallaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var x = await _db.CatalogoTallas.AsNoTracking().IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.IdTalla == id, ct);
            return x is null ? null : new TallaDto(x.IdTalla, x.Descripcion, x.Tipo, x.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<ResultadoTalla> CrearTallaAsync(CrearTallaRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(req.Descripcion, "La descripción");
        if (error is not null) return ResultadoTalla.Fallo(error);
        var desc = req.Descripcion.Trim();
        var tipo = req.Tipo.Trim();
        try
        {
            if (await _db.CatalogoTallas.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.Tipo == tipo, ct))
                return ResultadoTalla.Fallo("Ya existe una talla con esa descripción y tipo.");

            var t = new CatalogoTalla { Descripcion = desc, Tipo = tipo, Estatus = Estatus.Activo };
            _db.CatalogoTallas.Add(t);
            await _db.SaveChangesAsync(ct);
            return ResultadoTalla.Ok(new TallaDto(t.IdTalla, t.Descripcion, t.Tipo, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: req);
            return ResultadoTalla.Fallo("Error en la base de datos al crear la talla.");
        }
    }

    public async Task<ResultadoTalla> ActualizarTallaAsync(long id, ActualizarTallaRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(req.Descripcion, "La descripción");
        if (error is not null) return ResultadoTalla.Fallo(error);
        try
        {
            var t = await _db.CatalogoTallas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdTalla == id, ct);
            if (t is null) return ResultadoTalla.NoExiste($"No se encontró la talla #{id}.");

            var desc = req.Descripcion.Trim();
            var tipo = req.Tipo.Trim();
            if (await _db.CatalogoTallas.IgnoreQueryFilters().AnyAsync(x => x.Descripcion == desc && x.Tipo == tipo && x.IdTalla != id, ct))
                return ResultadoTalla.Fallo("Ya existe otra talla con esa misma descripción y tipo.");

            t.Descripcion = desc;
            t.Tipo = tipo;
            t.Estatus = req.Activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoTalla.Ok(new TallaDto(t.IdTalla, t.Descripcion, t.Tipo, t.Estatus == Estatus.Activo));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, req });
            return ResultadoTalla.Fallo("Error en la base de datos al actualizar la talla.");
        }
    }

    public async Task<ResultadoTalla> DarDeBajaTallaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var t = await _db.CatalogoTallas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.IdTalla == id, ct);
            if (t is null) return ResultadoTalla.NoExiste($"No se encontró la talla #{id}.");

            t.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoTalla.Ok(new TallaDto(t.IdTalla, t.Descripcion, t.Tipo, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoTalla.Fallo("Error en la base de datos al dar de baja la talla.");
        }
    }
}
