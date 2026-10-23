using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Pagos;

public class ServicioPagosAdmin
{
    private const string Proceso = "Revertir Pago (C#)";
    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioPagosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Elimina el abono físico principal. El trigger de PostgreSQL 
    /// se encargará de borrar los ajustes/bonificaciones vinculados en cascada.
    /// </summary>
    public async Task<(bool Exito, string? Error)> RevertirPagoAsync(long idPago, CancellationToken ct = default)
    {
        try
        {
            // 1. Buscar el pago físico principal en la base de datos
            var pagoPrincipal = await _db.Pagos
                .FirstOrDefaultAsync(p => p.IdPago == idPago, ct);

            if (pagoPrincipal is null)
            {
                _log.Warn(Proceso, $"Intento de reversión fallido. No existe el pago #{idPago}.");
                return (false, "El pago que intenta revertir no existe.");
            }

            // 2. Validación de seguridad opcional: Asegurar que es un abono físico (ID 1)
            if (pagoPrincipal.IdMovimiento != 1)
            {
                _log.Warn(Proceso, $"Intento de revertir un movimiento automático de forma directa #{idPago}.");
                return (false, "Solo se pueden revertir abonos físicos directamente. Los ajustes se borran automáticamente.");
            }

            _log.Info(Proceso, $"Iniciando eliminación del pago #{idPago} (Folio: {pagoPrincipal.FolioPago}).");

            // 3. Marcar para eliminación en EF Core
            _db.Pagos.Remove(pagoPrincipal);

            // 4. Guardar cambios. Aquí EF Core envía el comando "DELETE FROM Pagos WHERE IdPago = ..."
            // y PostgreSQL dispara tu función fn_revertir_abono_cascada().
            await _db.SaveChangesAsync(ct);

            _log.Info(Proceso, $"Pago #{idPago} y sus movimientos vinculados eliminados correctamente en la BD.");
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { idPago });
            return (false, "Ocurrió un error en la base de datos al intentar revertir el pago.");
        }
    }
}
