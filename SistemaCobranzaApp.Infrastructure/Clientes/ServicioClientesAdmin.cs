using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Clientes;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Clientes;

public class ResultadoCliente
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ClienteDto? Cliente { get; init; }

    public static ResultadoCliente Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoCliente Ok(ClienteDto c) => new() { Exito = true, Cliente = c };
}

/// <summary>
/// CRUD de administración de clientes con validación de datos y de FKs.
/// La baja es lógica (Estatus = Inactivo), coherente con el resto del sistema.
/// </summary>
public class ServicioClientesAdmin
{
    private readonly CobranzaDbContext _db;

    public ServicioClientesAdmin(CobranzaDbContext db)
    {
        _db = db;
    }

    public async Task<ClientesPaginaResponse> ListarAsync(
        string? texto, long? idRuta, bool incluirInactivos,
        int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = pagina < 1 ? 1 : pagina;
        tamanoPagina = tamanoPagina is < 1 or > 200 ? 50 : tamanoPagina;

        // 1. Ampliamos la consulta con JOINS para traer los nombres legibles
        var q = from c in _db.Clientes.AsNoTracking()
                join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                from r in rj.DefaultIfEmpty()
                join v in _db.Vendedores.AsNoTracking() on c.IdVendedor equals v.IdVendedor into vj
                from v in vj.DefaultIfEmpty()
                join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
                from cl in clj.DefaultIfEmpty()
                select new { c, r, v, cl };

        if (!incluirInactivos)
            q = q.Where(x => x.c.Estatus == Estatus.Activo);

        if (idRuta is { } ruta)
            q = q.Where(x => x.c.IdRuta == ruta);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            // Búsqueda por palabras: cada término debe aparecer en ALGÚN campo
            // (nombre/ap. paterno/ap. materno/teléfono). Así "alva pe sanc" encuentra
            // a "alvaro perez sanchez" (AND entre palabras, OR entre campos).
            var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var palabra in palabras)
            {
                var like = BusquedaHelper.ParaLike(palabra);
                q = q.Where(x =>
                    EF.Functions.ILike(x.c.Nombre, like)
                    || EF.Functions.ILike(x.c.ApPaterno ?? "", like)
                    || EF.Functions.ILike(x.c.ApMaterno ?? "", like)
                    || EF.Functions.ILike(x.c.TelefonoPrincipal, like));
            }
        }

        var total = await q.CountAsync(ct);

        var filas = await q
            .OrderBy(x => x.c.Nombre).ThenBy(x => x.c.ApPaterno).ThenBy(x => x.c.IdCliente)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(ct);

        var items = filas.Select(x => Mapear(x.c, x.r, x.v, x.cl)).ToList();
        return new ClientesPaginaResponse(items, total, pagina, tamanoPagina);
    }

    public async Task<ClienteDto?> ObtenerPorIdAsync(long id, CancellationToken ct = default)
    {
        var fila = await (from c in _db.Clientes.AsNoTracking()
                          where c.IdCliente == id
                          join r in _db.Rutas.AsNoTracking() on c.IdRuta equals r.IdRuta into rj
                          from r in rj.DefaultIfEmpty()
                          join v in _db.Vendedores.AsNoTracking() on c.IdVendedor equals v.IdVendedor into vj
                          from v in vj.DefaultIfEmpty()
                          join cl in _db.Clasificaciones.AsNoTracking() on c.IdClasificacion equals cl.IdClasificacion into clj
                          from cl in clj.DefaultIfEmpty()
                          select new { c, r, v, cl }).FirstOrDefaultAsync(ct);

        return fila is null ? null : Mapear(fila.c, fila.r, fila.v, fila.cl);
    }

    public async Task<ResultadoCliente> CrearAsync(CrearClienteRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCliente.Validar(req.Nombre, req.IdRuta, req.IdVendedor, req.IdClasificacion);
        if (error is not null)
            return ResultadoCliente.Fallo(error);

        var errorFk = await ValidarFksAsync(req.IdRuta, req.IdVendedor, req.IdClasificacion, ct);
        if (errorFk is not null)
            return ResultadoCliente.Fallo(errorFk);

        var ahora = DateTimeOffset.UtcNow;
        var cliente = new Cliente
        {
            Nombre = req.Nombre.Trim(),
            ApPaterno = Limpiar(req.ApPaterno),
            ApMaterno = Limpiar(req.ApMaterno),
            TelefonoPrincipal = req.TelefonoPrincipal.Trim(),      // Guardamos contacto
            TelefonoSecundario = Limpiar(req.TelefonoSecundario),
            Email = Limpiar(req.Email),
            Direccion = req.Direccion.Trim(),                      // Guardamos localización
            CodigoPostal = Limpiar(req.CodigoPostal),
            ReferenciaUbicacion = Limpiar(req.ReferenciaUbicacion),
            Latitud = req.Latitud,                                 // Guardamos GPS automático de MAUI
            Longitud = req.Longitud,
            IdRuta = req.IdRuta,
            IdVendedor = req.IdVendedor,
            IdClasificacion = req.IdClasificacion,
            Observaciones = Limpiar(req.Observaciones),
            IdUsuarioRegistro = req.IdUsuarioRegistro,
            Estatus = Estatus.Activo,
            FechaAlta = ahora,
            FechaModificacion = ahora
        };

        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(cliente.IdCliente, ct);
        return ResultadoCliente.Ok(dto!);
    }

    public async Task<ResultadoCliente> ActualizarAsync(long id, ActualizarClienteRequest req, CancellationToken ct = default)
    {
        var error = ValidacionCliente.Validar(req.Nombre, req.IdRuta, req.IdVendedor, req.IdClasificacion);
        if (error is not null)
            return ResultadoCliente.Fallo(error);

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return ResultadoCliente.Fallo($"No se encontró el cliente #{id}.");

        var errorFk = await ValidarFksAsync(req.IdRuta, req.IdVendedor, req.IdClasificacion, ct);
        if (errorFk is not null)
            return ResultadoCliente.Fallo(errorFk);

        cliente.Nombre = req.Nombre.Trim();
        cliente.ApPaterno = Limpiar(req.ApPaterno);
        cliente.ApMaterno = Limpiar(req.ApMaterno);
        cliente.TelefonoPrincipal = req.TelefonoPrincipal.Trim(); // Actualizamos contacto
        cliente.TelefonoSecundario = Limpiar(req.TelefonoSecundario);
        cliente.Email = Limpiar(req.Email);
        cliente.Direccion = req.Direccion.Trim();                 // Actualizamos localización
        cliente.CodigoPostal = Limpiar(req.CodigoPostal);
        cliente.ReferenciaUbicacion = Limpiar(req.ReferenciaUbicacion);
        cliente.Latitud = req.Latitud;                            // Por si se recalibra el GPS en campo
        cliente.Longitud = req.Longitud;
        cliente.IdRuta = req.IdRuta;
        cliente.IdVendedor = req.IdVendedor;
        cliente.IdClasificacion = req.IdClasificacion;
        cliente.Estatus = (Estatus)req.Estatus;                   // Mapeo del estatus cambiado
        cliente.Observaciones = Limpiar(req.Observaciones);
        cliente.FechaModificacion = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(id, ct);
        return ResultadoCliente.Ok(dto!);
    }

    /// <summary>
    /// Actualiza la clasificación del cliente (NUEVO/BUENO/REGULAR/MALO/MOROSO) y, opcionalmente,
    /// sus observaciones. Operación ligera usada desde el reporte de seguimiento: no toca el resto
    /// de los datos. Si <paramref name="observaciones"/> es null no modifica las observaciones;
    /// si viene vacío/espacios las limpia.
    /// </summary>
    public async Task<ResultadoCliente> ActualizarClasificacionAsync(long id, long idClasificacion, string? observaciones = null, CancellationToken ct = default)
    {
        if (idClasificacion <= 0)
            return ResultadoCliente.Fallo("La clasificación indicada no es válida.");

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return ResultadoCliente.Fallo($"No se encontró el cliente #{id}.");

        if (!await _db.Clasificaciones.AsNoTracking().AnyAsync(cl => cl.IdClasificacion == idClasificacion, ct))
            return ResultadoCliente.Fallo($"La clasificación #{idClasificacion} no existe.");

        cliente.IdClasificacion = idClasificacion;
        // null = no tocar; vacío/espacios = limpiar; con texto = actualizar.
        if (observaciones is not null)
            cliente.Observaciones = Limpiar(observaciones);
        cliente.FechaModificacion = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(id, ct);
        return ResultadoCliente.Ok(dto!);
    }

    /// <summary>Baja lógica: marca el cliente como Inactivo.</summary>
    public async Task<ResultadoCliente> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.IdCliente == id, ct);
        if (cliente is null)
            return ResultadoCliente.Fallo($"No se encontró el cliente #{id}.");

        cliente.Estatus = Estatus.Inactivo;
        cliente.FechaModificacion = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var dto = await ObtenerPorIdAsync(id, ct);
        return ResultadoCliente.Ok(dto!);
    }

    // ---------------- helpers ----------------

    private async Task<string?> ValidarFksAsync(long idRuta, long? idVendedor, long? idClasificacion, CancellationToken ct)
    {
        var ruta = await _db.Rutas.AsNoTracking().FirstOrDefaultAsync(r => r.IdRuta == idRuta, ct);
        if (ruta is null)
            return $"La ruta #{idRuta} no existe.";

        if (idVendedor is { } v)
        {
            var vendedor = await _db.Vendedores.AsNoTracking().FirstOrDefaultAsync(x => x.IdVendedor == v, ct);
            if (vendedor is null)
                return $"El vendedor #{v} no existe.";

            if (ruta.IdZona is not null && vendedor.IdZona != ruta.IdZona)
                return "El vendedor no pertenece a la zona de la ruta seleccionada.";
        }

        if (idClasificacion is { } c && !await _db.Clasificaciones.AnyAsync(x => x.IdClasificacion == c, ct))
            return $"La clasificación #{c} no existe.";

        return null;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // 2. Corregido: Mapeamos los 23 parámetros esperados por el ClienteDto unificado
    private static ClienteDto Mapear(Cliente c, Ruta? r, Vendedor? v, CatalogoClasificacion? cl) => new(
        c.IdCliente, 
        c.Nombre, 
        c.ApPaterno, 
        c.ApMaterno,
        BusquedaHelper.NombreCompleto(c.Nombre, c.ApPaterno, c.ApMaterno),
        c.TelefonoPrincipal,
        c.TelefonoSecundario,
        c.Email,
        c.Direccion,
        c.CodigoPostal,
        c.ReferenciaUbicacion,
        c.Latitud,
        c.Longitud,
        c.IdRuta, 
        r?.Descripcion, 
        r?.IdZona,
        c.IdVendedor, 
        v?.Nombre,          // Nombre legible del Vendedor mapeado desde el JOIN
        c.IdClasificacion,
        cl?.Descripcion,    // Nombre legible de la Clasificación mapeado desde el JOIN
        (int)c.Estatus,     // Casteo del Enum de dominio a int para el DTO
        c.Observaciones,
        c.FechaAlta
    );
}
