using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Logging;

namespace SistemaCobranzaApp.Infrastructure.Inventario;

public class ResultadoProducto
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public ProductoDto? Producto { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoProducto Fallo(string e) => new() { Exito = false, Error = e };
    public static ResultadoProducto NoExiste(string e) => new() { Exito = false, Error = e, NoEncontrado = true };
    public static ResultadoProducto Ok(ProductoDto p) => new() { Exito = true, Producto = p };
}

public class ServicioProductosAdmin
{
    private const string Proceso = "ServicioProductosAdmin (BD)";

    private readonly CobranzaDbContext _db;
    private readonly IServicioLog _log;

    public ServicioProductosAdmin(CobranzaDbContext db, IServicioLog log)
    {
        _db = db;
        _log = log;
    }

    // 1. Listar productos paginados usando tu estructura original de Include
    public async Task<ProductosPaginaResponse> ListarAsync(
        string? texto, bool? incluirInactivos, int? pagina, int? tamanoPagina, CancellationToken ct = default)
    {
        var incluir = incluirInactivos ?? false;
        var pag = pagina is null or < 1 ? 1 : pagina.Value;
        var tam = tamanoPagina is null or < 1 or > 200 ? 50 : tamanoPagina.Value;

        try
        {
            var q = _db.Productos.AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Color)
                .Include(p => p.Talla)
                .AsQueryable();

            if (!incluir) q = q.Where(p => p.Estatus == Estatus.Activo);

            if (!string.IsNullOrWhiteSpace(texto))
            {
                var like = BusquedaHelper.ParaLike(texto);
                q = q.Where(p => EF.Functions.ILike(p.CodigoClave, like)
                                 || EF.Functions.ILike(p.Descripcion, like));
            }

            var total = await q.CountAsync(ct);

            // Traemos los items e incluimos la consulta del catálogo de comisiones de forma segura
            var listaProductos = await q.OrderBy(p => p.Descripcion)
                .Skip((pag - 1) * tam).Take(tam)
                .ToListAsync(ct);

            // Obtenemos las configuraciones de comisiones de este bloque de productos para rellenar el DTO
            var idsProductos = listaProductos.Select(p => p.IdProducto).ToList();
            var comisiones = await _db.ConfiguracionesComisiones.AsNoTracking()
                .Include(cfg => cfg.CatalogoComision)
                .Where(cfg => idsProductos.Contains(cfg.IdProducto))
                .ToDictionaryAsync(cfg => cfg.IdProducto, cfg => cfg.CatalogoComision, ct);

            var items = listaProductos.Select(p =>
            {
                comisiones.TryGetValue(p.IdProducto, out var cc);
                return Map(p, cc);
            }).ToList();

            return new ProductosPaginaResponse(items, total, pag, tam);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { texto, incluirInactivos, pagina, tamanoPagina });
            _log.Error(Proceso, "Error en la base de datos al obtener los productos.");
            throw;
        }
    }

    // 2. Obtener un producto por ID con su comisión asignada
    public async Task<ProductoDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var p = await _db.Productos.AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Color)
                .Include(p => p.Talla)
                .FirstOrDefaultAsync(x => x.IdProducto == id, ct);

            if (p is null) return null;

            var config = await _db.ConfiguracionesComisiones.AsNoTracking()
                .Include(cfg => cfg.CatalogoComision)
                .FirstOrDefaultAsync(cfg => cfg.IdProducto == id, ct);

            return Map(p, config?.CatalogoComision);
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            _log.Error(Proceso, $"Error en la base de datos al obtener el producto #{id}.");
            throw;
        }
    }

    // 3. Crear Producto + Configuración de Comisión inicial de forma Transaccional
    public async Task<ResultadoProducto> CrearAsync(CrearProductoRequest req, CancellationToken ct = default)
    {
        var error = ValidacionProducto.Validar(req.CodigoClave, req.Descripcion, req.Costo,
            req.PrecioSugerido, req.PrecioMinimo, req.StockMini);
        if (error is not null) return ResultadoProducto.Fallo(error);

        var codigo = req.CodigoClave.Trim();
        if (await _db.Productos.AnyAsync(p => p.CodigoClave == codigo, ct))
            return ResultadoProducto.Fallo("Ya existe un producto con ese código/clave.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var prod = new Producto
            {
                CodigoClave = codigo,
                Descripcion = req.Descripcion.Trim(),
                Costo = req.Costo,
                PrecioSugerido = req.PrecioSugerido,
                PrecioMinimo = req.PrecioMinimo,
                StockMini = req.StockMini,
                IdCategoria = req.IdCategoria,
                IdMarca = req.IdMarca,
                IdColor = req.IdColor,
                IdTalla = req.IdTalla,
                Estatus = Estatus.Activo
            };
            _db.Productos.Add(prod);
            await _db.SaveChangesAsync(ct);

            // Inserción de la configuración comercial obligatoria
            var configComision = new ConfiguracionComision
            {
                IdProducto = prod.IdProducto,
                IdCatalogoComision = req.IdCatalogoComision,
                FechaAsignacion = DateTimeOffset.UtcNow
            };
            _db.ConfiguracionesComisiones.Add(configComision);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return ResultadoProducto.Ok(await ObtenerAsync(prod.IdProducto, ct) ?? Map(prod, null));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.Error(Proceso, ex, datos: new { req.CodigoClave, req.Descripcion });
            return ResultadoProducto.Fallo("Error en la base de datos al crear el producto.");
        }
    }

    // 4. Actualizar Producto + Reasignación de Comisión (Upsert) Transaccional
    public async Task<ResultadoProducto> ActualizarAsync(long id, ActualizarProductoRequest req,
        CancellationToken ct = default)
    {
        var error = ValidacionProducto.Validar(req.CodigoClave, req.Descripcion, req.Costo,
            req.PrecioSugerido, req.PrecioMinimo, req.StockMini);
        if (error is not null) return ResultadoProducto.Fallo(error);

        try
        {
            var prod = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
            if (prod is null) return ResultadoProducto.NoExiste($"No se encontró el producto #{id}.");

            var codigo = req.CodigoClave.Trim();
            if (await _db.Productos.AnyAsync(p => p.CodigoClave == codigo && p.IdProducto != id, ct))
                return ResultadoProducto.Fallo("Ya existe otro producto con ese código/clave.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            prod.CodigoClave = codigo;
            prod.Descripcion = req.Descripcion.Trim();
            prod.Costo = req.Costo;
            prod.PrecioSugerido = req.PrecioSugerido;
            prod.PrecioMinimo = req.PrecioMinimo;
            prod.StockMini = req.StockMini;
            prod.IdCategoria = req.IdCategoria;
            prod.IdMarca = req.IdMarca;
            prod.IdColor = req.IdColor;
            prod.IdTalla = req.IdTalla;
            prod.Estatus = req.Activo ? Estatus.Activo : Estatus.Inactivo;

            _db.Productos.Update(prod);

            // Control Upsert de la regla de comisión
            var configVigente =
                await _db.ConfiguracionesComisiones.FirstOrDefaultAsync(cfg => cfg.IdProducto == id, ct);
            if (configVigente is null)
            {
                _db.ConfiguracionesComisiones.Add(new ConfiguracionComision
                {
                    IdProducto = id,
                    IdCatalogoComision = req.IdCatalogoComision,
                    FechaAsignacion = DateTimeOffset.UtcNow
                });
            }
            else
            {
                configVigente.IdCatalogoComision = req.IdCatalogoComision;
                configVigente.FechaAsignacion = DateTimeOffset.UtcNow;
                _db.ConfiguracionesComisiones.Update(configVigente);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return ResultadoProducto.Ok(await ObtenerAsync(prod.IdProducto, ct) ?? Map(prod, null));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id, req.CodigoClave, req.Descripcion });
            return ResultadoProducto.Fallo("Error en la base de datos al actualizar el producto.");
        }
    }

    // 5. Baja lógica
    public async Task<ResultadoProducto> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        try
        {
            var prod = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
            if (prod is null) return ResultadoProducto.NoExiste($"No se encontró el producto #{id}.");

            prod.Estatus = Estatus.Inactivo;
            await _db.SaveChangesAsync(ct);
            return ResultadoProducto.Ok(await ObtenerAsync(id, ct) ?? Map(prod, null));
        }
        catch (Exception ex)
        {
            _log.Error(Proceso, ex, datos: new { id });
            return ResultadoProducto.Fallo("Error en la base de datos al dar de baja el producto.");
        }
    }

// 6. El método Map corregido con tu firma original más el objeto CatalogoComision
    private static ProductoDto Map(Producto p, CatalogoComision? cc) => new(
        p.IdProducto,
        p.CodigoClave,
        p.Descripcion,
        p.Costo,
        p.PrecioSugerido,
        p.PrecioMinimo,
        p.StockMini,
        p.IdCategoria,
        p.Categoria?.Descripcion ?? string.Empty,
        p.IdMarca,
        p.Marca?.Descripcion ?? string.Empty,
        p.IdColor,
        p.Color?.Descripcion,
        p.IdTalla,
        p.Talla?.Descripcion,
// Propiedades de la comisión (Pone valores por defecto si viene nulo)
        cc?.IdCatalogoComision ?? 0,
        cc?.NombreEsquema ?? "SIN ASIGNAR",
        cc?.TipoCalculo ?? "PORCENTAJE",
        cc?.ComisionContado ?? 0.00m,
        cc?.ComisionCredito ?? 0.00m,
        p.Estatus == Estatus.Activo
    );
}