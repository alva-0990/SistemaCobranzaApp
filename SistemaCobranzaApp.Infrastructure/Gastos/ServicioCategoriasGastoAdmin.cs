
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioCategoriasGastoAdmin
{
    private const string Proceso = "ServicioCategoriasGastoAdmin";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCategoriasGastoAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<CatalogoCategoriaGasto>> ListarAsync(
        bool incluirInactivas = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _db.CatalogoCategoriasGasto.AsNoTracking();

            if (!incluirInactivas)
                query = query.Where(x => x.Estatus == 1);

            return await query.OrderBy(x => x.Nombre).ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            throw;
        }
    }

    public async Task<CatalogoCategoriaGasto?> ObtenerAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            return await _db.CatalogoCategoriasGasto
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdCategoriaGasto == id, ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<ResultadoOperacion<CatalogoCategoriaGasto>> CrearAsync(
        string nombre,
        string? descripcion,
        bool requiereComprobante,
        CancellationToken ct = default)
    {
        nombre = nombre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nombre))
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("El nombre de la categoría es obligatorio.");

        if (nombre.Length > 100)
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("El nombre no puede exceder 100 caracteres.");

        descripcion = string.IsNullOrWhiteSpace(descripcion)
            ? null
            : descripcion.Trim();

        if (descripcion?.Length > 250)
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("La descripción no puede exceder 250 caracteres.");

        try
        {
            var existe = await _db.CatalogoCategoriasGasto
                .AnyAsync(x => x.Nombre.ToLower() == nombre.ToLower(), ct);

            if (existe)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .Fallo("Ya existe una categoría con ese nombre.");

            var entidad = new CatalogoCategoriaGasto
            {
                Nombre = nombre,
                Descripcion = descripcion,
                RequiereComprobante = requiereComprobante,
                Estatus = 1,
                FechaAlta = DateTimeOffset.UtcNow,
                FechaModificacion = DateTimeOffset.UtcNow
            };

            _db.CatalogoCategoriasGasto.Add(entidad);
            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CatalogoCategoriaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { nombre });
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("No fue posible crear la categoría.");
        }
    }

    public async Task<ResultadoOperacion<CatalogoCategoriaGasto>> ActualizarAsync(
        long id,
        string nombre,
        string? descripcion,
        bool requiereComprobante,
        bool activo,
        CancellationToken ct = default)
    {
        nombre = nombre?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("El nombre es obligatorio y debe tener máximo 100 caracteres.");

        descripcion = string.IsNullOrWhiteSpace(descripcion)
            ? null
            : descripcion.Trim();

        if (descripcion?.Length > 250)
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("La descripción no puede exceder 250 caracteres.");

        try
        {
            var entidad = await _db.CatalogoCategoriasGasto
                .FirstOrDefaultAsync(x => x.IdCategoriaGasto == id, ct);

            if (entidad is null)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .NoExiste($"No se encontró la categoría #{id}.");

            var duplicada = await _db.CatalogoCategoriasGasto
                .AnyAsync(x =>
                    x.IdCategoriaGasto != id &&
                    x.Nombre.ToLower() == nombre.ToLower(), ct);

            if (duplicada)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .Fallo("Ya existe otra categoría con ese nombre.");

            // Regla de negocio: no se puede desactivar una categoría que
            // tenga gastos relacionados (preserva el historial financiero).
            if (!activo && entidad.Estatus == 1)
            {
                var tieneGastos = await _db.Gastos
                    .AnyAsync(x => x.IdCategoriaGasto == id, ct);

                if (tieneGastos)
                    return ResultadoOperacion<CatalogoCategoriaGasto>
                        .Fallo("No se puede desactivar la categoría porque tiene gastos relacionados.");
            }

            entidad.Nombre = nombre;
            entidad.Descripcion = descripcion;
            entidad.RequiereComprobante = requiereComprobante;
            entidad.Estatus = activo ? 1 : 0;
            entidad.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CatalogoCategoriaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("No fue posible actualizar la categoría.");
        }
    }

    public async Task<ResultadoOperacion<CatalogoCategoriaGasto>> DarDeBajaAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            var entidad = await _db.CatalogoCategoriasGasto
                .FirstOrDefaultAsync(x => x.IdCategoriaGasto == id, ct);

            if (entidad is null)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .NoExiste($"No se encontró la categoría #{id}.");

            // Regla de negocio: no se puede dar de baja una categoría con
            // gastos relacionados (no se elimina el historial financiero).
            var tieneGastos = await _db.Gastos
                .AnyAsync(x => x.IdCategoriaGasto == id, ct);

            if (tieneGastos)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .Fallo("No se puede dar de baja la categoría porque tiene gastos relacionados.");

            entidad.Estatus = 0;
            entidad.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CatalogoCategoriaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("No fue posible dar de baja la categoría.");
        }
    }

    public async Task<ResultadoOperacion<CatalogoCategoriaGasto>> CambiarEstatusAsync(
        long id, bool activo, CancellationToken ct = default)
    {
        try
        {
            var entidad = await _db.CatalogoCategoriasGasto
                .FirstOrDefaultAsync(x => x.IdCategoriaGasto == id, ct);

            if (entidad is null)
                return ResultadoOperacion<CatalogoCategoriaGasto>
                    .NoExiste($"No se encontró la categoría #{id}.");

            // No se permite desactivar una categoría con gastos relacionados.
            if (!activo && entidad.Estatus == 1)
            {
                var tieneGastos = await _db.Gastos
                    .AnyAsync(x => x.IdCategoriaGasto == id, ct);

                if (tieneGastos)
                    return ResultadoOperacion<CatalogoCategoriaGasto>
                        .Fallo("No se puede desactivar la categoría porque tiene gastos relacionados.");
            }

            entidad.Estatus = activo ? 1 : 0;
            entidad.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CatalogoCategoriaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, activo });
            return ResultadoOperacion<CatalogoCategoriaGasto>
                .Fallo("No fue posible actualizar el estatus de la categoría.");
        }
    }
}
