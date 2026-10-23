
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ServicioGastosAdmin
{
    private const string Proceso = "ServicioGastosAdmin";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioGastosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<Gasto>> ListarAsync(
        DateTimeOffset? desde = null,
        DateTimeOffset? hasta = null,
        string? estatus = null,
        long? idCategoria = null,
        long? idZona = null,
        long? idCobrador = null,
        long? idVendedor = null,
        string? folio = null,
        bool soloCentrales = false,
        CancellationToken ct = default)
    {
        try
        {
            desde = desde?.ToUniversalTime();
            hasta = hasta?.ToUniversalTime();

            var query = _db.Gastos
                .AsNoTracking()
                .Include(x => x.Categoria)
                .Include(x => x.Zona)
                .Include(x => x.Cobrador)
                .Include(x => x.Vendedor)
                .Include(x => x.CuentaPrevista)
                .AsQueryable();

            if (desde.HasValue)
                query = query.Where(x => x.FechaGasto >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(x => x.FechaGasto <= hasta.Value);

            if (!string.IsNullOrWhiteSpace(estatus))
                query = query.Where(x => x.Estatus == estatus.Trim().ToUpperInvariant());

            if (idCategoria.HasValue)
                query = query.Where(x => x.IdCategoriaGasto == idCategoria.Value);

            if (idZona.HasValue)
                query = query.Where(x => x.IdZona == idZona.Value);

            if (idCobrador.HasValue)
                query = query.Where(x => x.IdCobrador == idCobrador.Value);

            if (idVendedor.HasValue)
                query = query.Where(x => x.IdVendedor == idVendedor.Value);

            // Gastos "centrales" / administrativos: sin clasificación operativa.
            if (soloCentrales)
                query = query.Where(x =>
                    x.IdZona == null &&
                    x.IdCobrador == null &&
                    x.IdVendedor == null);

            if (!string.IsNullOrWhiteSpace(folio))
            {
                var f = folio.Trim();
                query = query.Where(x => EF.Functions.ILike(x.FolioGasto, $"%{f}%"));
            }

            return await query
                .OrderByDescending(x => x.FechaGasto)
                .ThenByDescending(x => x.IdGasto)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex);
            throw;
        }
    }

    public async Task<Gasto?> ObtenerAsync(
        long id, CancellationToken ct = default)
    {
        try
        {
            return await _db.Gastos
                .AsNoTracking()
                .Include(x => x.Categoria)
                .Include(x => x.Zona)
                .Include(x => x.Cobrador)
                .Include(x => x.Vendedor)
                .Include(x => x.CuentaPrevista)
                .Include(x => x.Pagos)
                .Include(x => x.Comprobantes)
                .FirstOrDefaultAsync(x => x.IdGasto == id, ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            throw;
        }
    }

    /// <summary>
    /// Total pagado por gasto, calculado a partir de GastosPagos.
    /// Útil para construir el GastoDto (TotalPagado / SaldoPendiente) sin
    /// duplicar importes al relacionar con pagos o movimientos.
    /// </summary>
    public async Task<decimal> TotalPagadoAsync(long idGasto, CancellationToken ct = default)
    {
        return await _db.GastosPagos
            .Where(x => x.IdGasto == idGasto)
            .SumAsync(x => (decimal?)x.Importe, ct) ?? 0m;
    }

    public async Task<ResultadoOperacion<Gasto>> CrearAsync(
        long idCategoriaGasto,
        DateTimeOffset fechaGasto,
        string concepto,
        decimal importe,
        long idUsuarioRegistro,
        string? descripcion = null,
        string? proveedor = null,
        string? rfcProveedor = null,
        string? referencia = null,
        long? idZona = null,
        long? idCobrador = null,
        long? idVendedor = null,
        long? idCuentaPrevista = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        string folio = GenerarCodigoGasto(idUsuarioRegistro);
        concepto = concepto?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(concepto) || concepto.Length > 250)
            return ResultadoOperacion<Gasto>
                .Fallo("El concepto es obligatorio y debe tener máximo 250 caracteres.");

        if (importe <= 0)
            return ResultadoOperacion<Gasto>
                .Fallo("El importe debe ser mayor que cero.");

        if (idUsuarioRegistro <= 0)
            return ResultadoOperacion<Gasto>
                .Fallo("El usuario que registra el gasto no es válido.");

        if (proveedor?.Length > 200 ||
            rfcProveedor?.Length > 20 ||
            referencia?.Length > 100)
        {
            return ResultadoOperacion<Gasto>
                .Fallo("Uno de los campos de proveedor o referencia excede su longitud permitida.");
        }

        try
        {
            var categoriaExiste = await _db.CatalogoCategoriasGasto
                .AnyAsync(x => x.IdCategoriaGasto == idCategoriaGasto &&
                               x.Estatus == 1, ct);

            if (!categoriaExiste)
                return ResultadoOperacion<Gasto>
                    .Fallo("La categoría no existe o está inactiva.");

            var errorClasificacion = await ValidarClasificacionAsync(
                idZona, idCobrador, idVendedor, idCuentaPrevista, ct);

            if (errorClasificacion is not null)
                return ResultadoOperacion<Gasto>.Fallo(errorClasificacion);

            if (await _db.Gastos.AnyAsync(x => x.FolioGasto == folio, ct))
                return ResultadoOperacion<Gasto>
                    .Fallo("Ya existe un gasto con ese folio.");

            var entidad = new Gasto
            {
                IdCategoriaGasto = idCategoriaGasto,
                FolioGasto = folio,
                FechaGasto = fechaGasto.ToUniversalTime(),
                Concepto = concepto,
                Descripcion = descripcion?.Trim(),
                Proveedor = proveedor?.Trim(),
                RFCProveedor = rfcProveedor?.Trim(),
                Referencia = referencia?.Trim(),
                Importe = importe,
                Estatus = "PENDIENTE",
                IdZona = idZona,
                IdCobrador = idCobrador,
                IdVendedor = idVendedor,
                IdCuentaPrevista = idCuentaPrevista,
                Observaciones = observaciones?.Trim(),
                IdUsuarioRegistro = idUsuarioRegistro,
                FechaAlta = DateTimeOffset.UtcNow,
                FechaModificacion = DateTimeOffset.UtcNow
            };

            _db.Gastos.Add(entidad);
            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<Gasto>.Ok(entidad);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { folio, idCategoriaGasto });
            return ResultadoOperacion<Gasto>
                .Fallo("No fue posible registrar el gasto.");
        }
    }

    /// <summary>
    /// Edita los datos de un gasto. Solo se permite mientras el gasto no
    /// tenga pagos aplicados y no esté cancelado, para no romper el
    /// historial financiero (importe pagado / movimientos de cuenta).
    /// </summary>
    public async Task<ResultadoOperacion<Gasto>> ActualizarAsync(
        long id,
        long idCategoriaGasto,
        DateTimeOffset fechaGasto,
        string concepto,
        decimal importe,
        string? descripcion = null,
        string? proveedor = null,
        string? rfcProveedor = null,
        string? referencia = null,
        long? idZona = null,
        long? idCobrador = null,
        long? idVendedor = null,
        long? idCuentaPrevista = null,
        string? observaciones = null,
        CancellationToken ct = default)
    {
        concepto = concepto?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(concepto) || concepto.Length > 250)
            return ResultadoOperacion<Gasto>
                .Fallo("El concepto es obligatorio y debe tener máximo 250 caracteres.");

        if (importe <= 0)
            return ResultadoOperacion<Gasto>
                .Fallo("El importe debe ser mayor que cero.");

        if (proveedor?.Length > 200 ||
            rfcProveedor?.Length > 20 ||
            referencia?.Length > 100)
        {
            return ResultadoOperacion<Gasto>
                .Fallo("Uno de los campos de proveedor o referencia excede su longitud permitida.");
        }

        try
        {
            var gasto = await _db.Gastos
                .FirstOrDefaultAsync(x => x.IdGasto == id, ct);

            if (gasto is null)
                return ResultadoOperacion<Gasto>.NoExiste(
                    $"No se encontró el gasto #{id}.");

            if (gasto.Estatus == "CANCELADO")
                return ResultadoOperacion<Gasto>.Fallo(
                    "No se puede editar un gasto cancelado.");

            var tienePagos = await _db.GastosPagos
                .AnyAsync(x => x.IdGasto == id, ct);

            if (tienePagos)
                return ResultadoOperacion<Gasto>.Fallo(
                    "No se puede editar un gasto que ya tiene pagos registrados.");

            // Si cambia el importe, no debe quedar por debajo de lo pagado
            // (defensa adicional aunque aquí ya sabemos que no hay pagos).
            var totalPagado = await TotalPagadoAsync(id, ct);
            if (importe < totalPagado)
                return ResultadoOperacion<Gasto>.Fallo(
                    $"El importe no puede ser menor al total ya pagado ({totalPagado:C2}).");

            var categoriaExiste = await _db.CatalogoCategoriasGasto
                .AnyAsync(x => x.IdCategoriaGasto == idCategoriaGasto &&
                               x.Estatus == 1, ct);

            if (!categoriaExiste)
                return ResultadoOperacion<Gasto>
                    .Fallo("La categoría no existe o está inactiva.");

            var errorClasificacion = await ValidarClasificacionAsync(
                idZona, idCobrador, idVendedor, idCuentaPrevista, ct);

            if (errorClasificacion is not null)
                return ResultadoOperacion<Gasto>.Fallo(errorClasificacion);

            gasto.IdCategoriaGasto = idCategoriaGasto;
            gasto.FechaGasto = fechaGasto.ToUniversalTime();
            gasto.Concepto = concepto;
            gasto.Descripcion = descripcion?.Trim();
            gasto.Proveedor = proveedor?.Trim();
            gasto.RFCProveedor = rfcProveedor?.Trim();
            gasto.Referencia = referencia?.Trim();
            gasto.Importe = importe;
            gasto.IdZona = idZona;
            gasto.IdCobrador = idCobrador;
            gasto.IdVendedor = idVendedor;
            gasto.IdCuentaPrevista = idCuentaPrevista;
            gasto.Observaciones = observaciones?.Trim();
            gasto.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<Gasto>.Ok(gasto);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, idCategoriaGasto });
            return ResultadoOperacion<Gasto>
                .Fallo("No fue posible actualizar el gasto.");
        }
    }

    public async Task<ResultadoOperacion<Gasto>> AutorizarAsync(
        long id, long idUsuarioAutoriza, CancellationToken ct = default)
    {
        try
        {
            var gasto = await _db.Gastos
                .FirstOrDefaultAsync(x => x.IdGasto == id, ct);

            if (gasto is null)
                return ResultadoOperacion<Gasto>.NoExiste(
                    $"No se encontró el gasto #{id}.");

            if (gasto.Estatus != "PENDIENTE")
                return ResultadoOperacion<Gasto>.Fallo(
                    "Solo se pueden autorizar gastos pendientes.");

            if (idUsuarioAutoriza <= 0)
                return ResultadoOperacion<Gasto>.Fallo(
                    "El usuario que autoriza no es válido.");

            gasto.Estatus = "AUTORIZADO";
            gasto.IdUsuarioAutoriza = idUsuarioAutoriza;
            gasto.FechaAutorizacion = DateTimeOffset.UtcNow;
            gasto.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);

            return ResultadoOperacion<Gasto>.Ok(gasto);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, idUsuarioAutoriza });
            return ResultadoOperacion<Gasto>
                .Fallo("No fue posible autorizar el gasto.");
        }
    }

    public async Task<ResultadoOperacion<Gasto>> CancelarAsync(
        long id,
        long idUsuarioCancelacion,
        string motivo,
        CancellationToken ct = default)
    {
        motivo = motivo?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 500)
            return ResultadoOperacion<Gasto>
                .Fallo("El motivo de cancelación es obligatorio y debe tener máximo 500 caracteres.");

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var gasto = await _db.Gastos
                .FirstOrDefaultAsync(x => x.IdGasto == id, ct);

            if (gasto is null)
                return ResultadoOperacion<Gasto>.NoExiste(
                    $"No se encontró el gasto #{id}.");

            if (gasto.Estatus == "CANCELADO")
                return ResultadoOperacion<Gasto>.Fallo(
                    "El gasto ya está cancelado.");

            if (gasto.Estatus == "PARCIAL" || gasto.Estatus == "PAGADO")
                return ResultadoOperacion<Gasto>.Fallo(
                    "No se puede cancelar un gasto que ya tiene pagos. Primero debe realizarse un proceso de reversión de pagos y movimientos.");

            var tienePagos = await _db.GastosPagos
                .AnyAsync(x => x.IdGasto == id, ct);

            if (tienePagos)
                return ResultadoOperacion<Gasto>.Fallo(
                    "No se puede cancelar un gasto con pagos registrados.");

            gasto.Estatus = "CANCELADO";
            gasto.IdUsuarioCancelacion = idUsuarioCancelacion;
            gasto.FechaCancelacion = DateTimeOffset.UtcNow;
            gasto.MotivoCancelacion = motivo;
            gasto.FechaModificacion = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoOperacion<Gasto>.Ok(gasto);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, idUsuarioCancelacion });
            return ResultadoOperacion<Gasto>
                .Fallo("No fue posible cancelar el gasto.");
        }
    }

    /// <summary>
    /// Valida que las referencias de clasificación operativa existan.
    /// Devuelve el mensaje de error o null si todo es válido. Todas son
    /// opcionales: un gasto sin ninguna es un gasto central/administrativo.
    /// </summary>
    private async Task<string?> ValidarClasificacionAsync(
        long? idZona, long? idCobrador, long? idVendedor, long? idCuentaPrevista, CancellationToken ct)
    {
        if (idZona is { } z && !await _db.Zonas.AnyAsync(x => x.IdZona == z, ct))
            return "La zona indicada no existe.";

        if (idCobrador is { } c && !await _db.Cobradores.AnyAsync(x => x.IdCobrador == c, ct))
            return "El cobrador indicado no existe.";

        if (idVendedor is { } v && !await _db.Vendedores.AnyAsync(x => x.IdVendedor == v, ct))
            return "El vendedor indicado no existe.";

        if (idCuentaPrevista is { } cu && !await _db.CuentasGasto.AnyAsync(x => x.IdCuentaGasto == cu, ct))
            return "La cuenta prevista indicada no existe.";

        return null;
    }

    public static string GenerarCodigoGasto(long idUsuario)
    {
        string timestamp = DateTime.UtcNow.ToString("yyMMdd-HHmmss");

        const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] sufijo = new char[3];

        for (int i = 0; i < sufijo.Length; i++)
        {
            sufijo[i] = caracteres[RandomNumberGenerator.GetInt32(caracteres.Length)];
        }

        return $"G{idUsuario}-{timestamp}-{new string(sufijo)}";
    }
}
