using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Consulta de la tabla de paso colamensajeswhatsapp (mensajes encolados, enviados y fallidos).
/// Solo lectura: el envío lo controla WorkerWhatsApp y la purga WorkerLimpiezaWhatsApp.
/// </summary>
public class ServicioMensajesWhatsAppAdmin
{
    private const string Proceso = "ServicioMensajesWhatsAppAdmin (BD)";
    private const int LimiteMaximo = 1000;

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioMensajesWhatsAppAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>
    /// Lista los mensajes más recientes primero.
    /// </summary>
    /// <param name="estado">Pendiente | Procesando | Enviado | Fallido. Null = todos.</param>
    /// <param name="texto">Busca en el teléfono enviado, mensaje, error, nombre del cliente
    /// y los teléfonos principal/secundario registrados del cliente.</param>
    /// <param name="limite">Máximo de filas (1-1000).</param>
    public async Task<List<MensajeWhatsAppDto>> ListarAsync(
        string? estado, string? texto, int limite, CancellationToken ct = default)
    {
        try
        {
            limite = Math.Clamp(limite, 1, LimiteMaximo);

            var q =
                from m in _db.ColaMensajesWhatsApp.AsNoTracking()
                join v in _db.Ventas.AsNoTracking() on m.IdVenta equals (long?)v.IdVenta into gv
                from v in gv.DefaultIfEmpty()
                join c in _db.Clientes.AsNoTracking() on (v != null ? (long?)v.IdCliente : null) equals (long?)c.IdCliente into gc
                from c in gc.DefaultIfEmpty()
                select new
                {
                    m,
                    CodigoVenta = v != null ? v.CodigoVenta : null,
                    Cliente = c != null
                        ? (c.Nombre + " " + (c.ApPaterno ?? "") + " " + (c.ApMaterno ?? "")).Trim()
                        : null,
                    TelefonoPrincipal = c != null ? c.TelefonoPrincipal : null,
                    TelefonoSecundario = c != null ? c.TelefonoSecundario : null
                };

            if (!string.IsNullOrWhiteSpace(estado))
                q = q.Where(x => x.m.Estado == estado);

            if (!string.IsNullOrWhiteSpace(texto))
            {
                var like = $"%{texto.Trim()}%";
                q = q.Where(x => EF.Functions.ILike(x.m.Telefono, like)
                                 || EF.Functions.ILike(x.m.Mensaje, like)
                                 || (x.m.ErrorLog != null && EF.Functions.ILike(x.m.ErrorLog, like))
                                 || (x.Cliente != null && EF.Functions.ILike(x.Cliente, like))
                                 || (x.TelefonoPrincipal != null && EF.Functions.ILike(x.TelefonoPrincipal, like))
                                 || (x.TelefonoSecundario != null && EF.Functions.ILike(x.TelefonoSecundario, like)));
            }

            return await q
                .OrderByDescending(x => x.m.FechaCreacion)
                .ThenByDescending(x => x.m.Id)
                .Take(limite)
                .Select(x => new MensajeWhatsAppDto(
                    x.m.Id, x.m.Telefono, x.Cliente, x.m.Mensaje, x.m.Estado, x.m.Intentos,
                    x.m.TipoOrigen, x.m.IdVenta, x.CodigoVenta, x.m.IdPago, x.m.ErrorLog,
                    x.m.FechaCreacion, x.m.FechaEnvio))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { estado, texto, limite });
            _log.Error(Proceso, "Error en la base de datos al consultar la cola de mensajes de WhatsApp.");
            throw;
        }
    }
}
