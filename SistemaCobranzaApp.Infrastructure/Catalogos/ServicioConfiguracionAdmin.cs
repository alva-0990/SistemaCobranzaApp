using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Constantes;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

/// <summary>
/// Resultado uniforme para las operaciones de los catálogos de configuración
/// (clasificaciones, condiciones, estatus de venta y movimientos).
/// </summary>
public class ResultadoCatalogoConfig
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public CatalogoConfigDto? Item { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoCatalogoConfig Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoCatalogoConfig NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoCatalogoConfig Ok(CatalogoConfigDto item) => new() { Exito = true, Item = item };
}

/// <summary>
/// Lógica común de CRUD (baja lógica) para catálogos de configuración con la
/// forma Id + Descripción + Estatus. Cada catálogo concreto provee el acceso a
/// su <see cref="DbSet{TEntity}"/> y los selectores de Id/Descripción/Estatus.
/// </summary>
/// <typeparam name="TEntity">Entidad del catálogo.</typeparam>
public abstract class ServicioCatalogoConfigBase<TEntity> where TEntity : class, new()
{
    protected readonly CobranzaDbContext Db;

    protected ServicioCatalogoConfigBase(CobranzaDbContext db) => Db = db;

    // --- Puntos de extensión por catálogo concreto ---
    protected abstract DbSet<TEntity> Set { get; }
    protected abstract long GetId(TEntity e);
    protected abstract string GetDescripcion(TEntity e);
    protected abstract void SetDescripcion(TEntity e, string valor);
    protected abstract Estatus GetEstatus(TEntity e);
    protected abstract void SetEstatus(TEntity e, Estatus valor);

    /// <summary>Nombre singular del catálogo para los mensajes (p. ej. "la clasificación").</summary>
    protected abstract string NombreSingular { get; }

    /// <summary>
    /// IDs que no pueden darse de baja porque el negocio los asume como
    /// constantes (p. ej. estatus de venta 1–4). La descripción sí es editable.
    /// </summary>
    protected virtual IReadOnlySet<long> IdsProtegidos => System.Collections.Frozen.FrozenSet<long>.Empty;

    private CatalogoConfigDto ToDto(TEntity e) =>
        new(GetId(e), GetDescripcion(e), GetEstatus(e) == Estatus.Activo);

    public async Task<List<CatalogoConfigDto>> ListarAsync(bool incluirInactivos, CancellationToken ct = default)
    {
        var q = Set.AsNoTracking().AsQueryable();
        if (!incluirInactivos)
            q = q.Where(e => GetEstatus(e) == Estatus.Activo);
        var items = await q.ToListAsync(ct);
        return items
            .OrderBy(GetDescripcion, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<CatalogoConfigDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var e = await BuscarAsync(id, noTracking: true, ct);
        return e is null ? null : ToDto(e);
    }

    public async Task<ResultadoCatalogoConfig> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCatalogoConfig.Fallo(error);

        var desc = descripcion.Trim();
        if (await ExisteDescripcionAsync(desc, excluirId: null, ct))
            return ResultadoCatalogoConfig.Fallo($"Ya existe {NombreSingular} con esa descripción.");

        var entidad = new TEntity();
        SetDescripcion(entidad, desc);
        SetEstatus(entidad, Estatus.Activo);
        Set.Add(entidad);
        await Db.SaveChangesAsync(ct);
        return ResultadoCatalogoConfig.Ok(ToDto(entidad));
    }

    public async Task<ResultadoCatalogoConfig> ActualizarAsync(long id, string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoCatalogoConfig.Fallo(error);

        var entidad = await BuscarAsync(id, noTracking: false, ct);
        if (entidad is null) return ResultadoCatalogoConfig.NoExiste($"No se encontró {NombreSingular} #{id}.");

        var desc = descripcion.Trim();
        if (await ExisteDescripcionAsync(desc, excluirId: id, ct))
            return ResultadoCatalogoConfig.Fallo($"Ya existe otro registro con esa descripción.");

        SetDescripcion(entidad, desc);
        await Db.SaveChangesAsync(ct);
        return ResultadoCatalogoConfig.Ok(ToDto(entidad));
    }

    public async Task<ResultadoCatalogoConfig> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        if (IdsProtegidos.Contains(id))
            return ResultadoCatalogoConfig.Fallo(
                $"El registro #{id} es de uso interno del sistema y no puede darse de baja.");

        var entidad = await BuscarAsync(id, noTracking: false, ct);
        if (entidad is null) return ResultadoCatalogoConfig.NoExiste($"No se encontró {NombreSingular} #{id}.");

        SetEstatus(entidad, Estatus.Inactivo);
        await Db.SaveChangesAsync(ct);
        return ResultadoCatalogoConfig.Ok(ToDto(entidad));
    }

    // --- Helpers internos que evalúan en memoria los selectores por entidad ---

    private async Task<TEntity?> BuscarAsync(long id, bool noTracking, CancellationToken ct)
    {
        var q = noTracking ? Set.AsNoTracking() : Set;
        // Los selectores son delegados C#, no traducibles a SQL; materializamos
        // el candidato por Id con una comparación en memoria acotada por el Id.
        var lista = await q.ToListAsync(ct);
        return lista.FirstOrDefault(e => GetId(e) == id);
    }

    private async Task<bool> ExisteDescripcionAsync(string descripcion, long? excluirId, CancellationToken ct)
    {
        var lista = await Set.AsNoTracking().ToListAsync(ct);
        return lista.Any(e =>
            string.Equals(GetDescripcion(e), descripcion, StringComparison.CurrentCultureIgnoreCase)
            && (excluirId is null || GetId(e) != excluirId.Value));
    }
}






