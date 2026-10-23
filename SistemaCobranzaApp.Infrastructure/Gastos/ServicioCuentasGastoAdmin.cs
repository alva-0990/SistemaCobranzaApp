
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioCuentasGastoAdmin
{
    private const string Proceso = "ServicioCuentasGastoAdmin";
    private static readonly string[] TiposValidos =
        ["EFECTIVO", "CAJA_CHICA", "BANCO", "OTRO"];

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioCuentasGastoAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<CuentaGasto>> ListarAsync(
        bool incluirInactivas = false,
        CancellationToken ct = default)
    {
        try
        {
            var query = _db.CuentasGasto.AsNoTracking();

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

    public async Task<CuentaGasto?> ObtenerAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            return await _db.CuentasGasto
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == id, ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    public async Task<decimal?> ObtenerSaldoAsync(
        long idCuenta, CancellationToken ct = default)
    {
        try
        {
            var cuenta = await _db.CuentasGasto
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == idCuenta, ct);

            if (cuenta is null)
                return null;

            var entradas = await _db.MovimientosCuentaGasto
                .Where(x => x.IdCuentaGasto == idCuenta &&
                            x.TipoMovimiento == "ENTRADA")
                .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;

            var salidas = await _db.MovimientosCuentaGasto
                .Where(x => x.IdCuentaGasto == idCuenta &&
                            x.TipoMovimiento == "SALIDA")
                .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;

            return cuenta.SaldoInicial + entradas - salidas;
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idCuenta });
            throw;
        }
    }

    public async Task<ResultadoOperacion<CuentaGasto>> CrearAsync(
        string nombre,
        string tipoCuenta,
        string moneda,
        decimal saldoInicial,
        string? observaciones,
        CancellationToken ct = default)
    {
        nombre = nombre?.Trim() ?? string.Empty;
        tipoCuenta = tipoCuenta?.Trim().ToUpperInvariant() ?? string.Empty;
        moneda = string.IsNullOrWhiteSpace(moneda)
            ? "MXN"
            : moneda.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("El nombre es obligatorio y debe tener máximo 100 caracteres.");

        if (!TiposValidos.Contains(tipoCuenta))
            return ResultadoOperacion<CuentaGasto>
                .Fallo("El tipo de cuenta no es válido.");

        if (moneda.Length > 5)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("La moneda no puede exceder 5 caracteres.");

        if (saldoInicial < 0)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("El saldo inicial no puede ser negativo.");

        if (observaciones?.Length > 250)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("Las observaciones no pueden exceder 250 caracteres.");

        try
        {
            if (await _db.CuentasGasto.AnyAsync(x => x.Nombre == nombre, ct))
                return ResultadoOperacion<CuentaGasto>
                    .Fallo("Ya existe una cuenta con ese nombre.");

            var entidad = new CuentaGasto
            {
                Nombre = nombre,
                TipoCuenta = tipoCuenta,
                Moneda = moneda,
                SaldoInicial = saldoInicial,
                Estatus = 1,
                Observaciones = observaciones?.Trim(),
                FechaAlta = DateTimeOffset.UtcNow,
                FechaModificacion = DateTimeOffset.UtcNow
            };

            _db.CuentasGasto.Add(entidad);
            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CuentaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { nombre });
            return ResultadoOperacion<CuentaGasto>
                .Fallo("No fue posible crear la cuenta.");
        }
    }

    public async Task<ResultadoOperacion<CuentaGasto>> ActualizarAsync(
        long id,
        string nombre,
        string tipoCuenta,
        string moneda,
        string? observaciones,
        bool activo,
        CancellationToken ct = default)
    {
        nombre = nombre?.Trim() ?? string.Empty;
        tipoCuenta = tipoCuenta?.Trim().ToUpperInvariant() ?? string.Empty;
        moneda = string.IsNullOrWhiteSpace(moneda)
            ? "MXN"
            : moneda.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("El nombre es obligatorio y debe tener máximo 100 caracteres.");

        if (!TiposValidos.Contains(tipoCuenta))
            return ResultadoOperacion<CuentaGasto>
                .Fallo("El tipo de cuenta no es válido.");

        if (moneda.Length > 5)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("La moneda no puede exceder 5 caracteres.");

        if (observaciones?.Length > 250)
            return ResultadoOperacion<CuentaGasto>
                .Fallo("Las observaciones no pueden exceder 250 caracteres.");

        try
        {
            var entidad = await _db.CuentasGasto
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == id, ct);

            if (entidad is null)
                return ResultadoOperacion<CuentaGasto>
                    .NoExiste($"No se encontró la cuenta #{id}.");

            var duplicada = await _db.CuentasGasto
                .AnyAsync(x => x.IdCuentaGasto != id && x.Nombre == nombre, ct);

            if (duplicada)
                return ResultadoOperacion<CuentaGasto>
                    .Fallo("Ya existe otra cuenta con ese nombre.");

            entidad.Nombre = nombre;
            entidad.TipoCuenta = tipoCuenta;
            entidad.Moneda = moneda;
            entidad.Observaciones = observaciones?.Trim();
            entidad.Estatus = activo ? 1 : 0;
            entidad.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CuentaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoOperacion<CuentaGasto>
                .Fallo("No fue posible actualizar la cuenta.");
        }
    }

    public async Task<ResultadoOperacion<CuentaGasto>> CambiarEstatusAsync(
        long id, bool activo, CancellationToken ct = default)
    {
        try
        {
            var entidad = await _db.CuentasGasto
                .FirstOrDefaultAsync(x => x.IdCuentaGasto == id, ct);

            if (entidad is null)
                return ResultadoOperacion<CuentaGasto>
                    .NoExiste($"No se encontró la cuenta #{id}.");

            entidad.Estatus = activo ? 1 : 0;
            entidad.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<CuentaGasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, activo });
            return ResultadoOperacion<CuentaGasto>
                .Fallo("No fue posible actualizar el estatus de la cuenta.");
        }
    }
}
