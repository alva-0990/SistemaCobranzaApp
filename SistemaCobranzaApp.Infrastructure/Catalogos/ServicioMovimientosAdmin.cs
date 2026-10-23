using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Catalogos;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Catalogos;

public class ResultadoMovimiento
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public MovimientoDto? Movimiento { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoMovimiento Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoMovimiento NoExiste(string error) => new() { Exito = false, Error = error, NoEncontrado = true };
    public static ResultadoMovimiento Ok(MovimientoDto m) => new() { Exito = true, Movimiento = m };
}

public class ServicioMovimientosAdmin
{
    private const string Proceso = "ServicioMovimientosAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioMovimientosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listado operativo (Solo activos)
    public async Task<List<MovimientoDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Movimientos
                .AsNoTracking()
                .Where(m => m.Estatus == Estatus.Activo)
                .OrderBy(m => m.Descripcion)
                .Select(m => new MovimientoDto(m.IdMovimiento, m.Descripcion, true))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener los movimientos activos.");
            throw;
        }
    }

    // 2. Listado de administración (Trae absolutamente todo)
    public async Task<List<MovimientoDto>> ListarTodasAsync(CancellationToken ct = default)
    {
        try
        {
            return await _db.Movimientos
                .AsNoTracking()
                .IgnoreQueryFilters()
                .OrderBy(m => m.Descripcion)
                .Select(m => new MovimientoDto(m.IdMovimiento, m.Descripcion, m.Estatus == Estatus.Activo))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            _log.Error(Proceso, "Error en la base de datos al obtener todos los movimientos.");
            throw;
        }
    }

    // 3. Obtener por ID
    public async Task<MovimientoDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var m = await _db.Movimientos
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.IdMovimiento == id, ct);

            return m is null ? null : new MovimientoDto(m.IdMovimiento, m.Descripcion, m.Estatus == Estatus.Activo);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el movimiento #{id}.");
            throw;
        }
    }

    // 4. Crear (Alta)
    public async Task<ResultadoMovimiento> CrearAsync(string descripcion, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMovimiento.Fallo(error);

        var desc = descripcion.Trim();

        try
        {
            if (await _db.Movimientos.IgnoreQueryFilters().AnyAsync(m => m.Descripcion == desc, ct))
                return ResultadoMovimiento.Fallo("Ya existe un movimiento con esa descripción.");

            var nuevoMovimiento = new CatalogoMovimientos { Descripcion = desc, Estatus = Estatus.Activo };
            _db.Movimientos.Add(nuevoMovimiento);
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMovimiento.Ok(new MovimientoDto(nuevoMovimiento.IdMovimiento, nuevoMovimiento.Descripcion, true));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { descripcion = desc });
            return ResultadoMovimiento.Fallo("Error en la base de datos al crear el movimiento.");
        }
    }

    // 5. Actualizar
    public async Task<ResultadoMovimiento> ActualizarAsync(long id, string descripcion, bool activo, CancellationToken ct = default)
    {
        var error = ValidacionCatalogo.ValidarTextoRequerido(descripcion, "La descripción");
        if (error is not null) return ResultadoMovimiento.Fallo(error);

        try
        {
            var movimiento = await _db.Movimientos.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.IdMovimiento == id, ct);
            if (movimiento is null) return ResultadoMovimiento.NoExiste($"No se encontró el movimiento #{id}.");

            var desc = descripcion.Trim();
            if (await _db.Movimientos.IgnoreQueryFilters().AnyAsync(m => m.Descripcion == desc && m.IdMovimiento != id, ct))
                return ResultadoMovimiento.Fallo("Ya existe otro movimiento con esa descripción.");

            movimiento.Descripcion = desc;
            movimiento.Estatus = activo ? Estatus.Activo : Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMovimiento.Ok(new MovimientoDto(movimiento.IdMovimiento, movimiento.Descripcion, movimiento.Estatus == Estatus.Activo));
        }
        catch (Exception ex) {
            _log.Error(Proceso, ex, datos: new { id, descripcion });
            return ResultadoMovimiento.Fallo("Error en la base de datos al actualizar el movimiento.");
        }
    }

    // 6. Dar de Baja (Inactivar)
    public async Task<ResultadoMovimiento> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var movimiento = await _db.Movimientos.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.IdMovimiento == id, ct);
            if (movimiento is null) return ResultadoMovimiento.NoExiste($"No se encontró el movimiento #{id}.");

            movimiento.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            
            return ResultadoMovimiento.Ok(new MovimientoDto(movimiento.IdMovimiento, movimiento.Descripcion, false));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoMovimiento.Fallo("Error en la base de datos al dar de baja el movimiento.");
        }
    }
}
