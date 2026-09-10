using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Cobranza;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;
using SistemaCobranzaApp.Domain.Inventario;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Shared.Dtos;

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
    private readonly CobranzaDbContext _db;

    public ServicioProductosAdmin(CobranzaDbContext db) => _db = db;

    public async Task<ProductosPaginaResponse> ListarAsync(
        string? texto, bool incluirInactivos, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        pagina = pagina < 1 ? 1 : pagina;
        tamanoPagina = tamanoPagina is < 1 or > 200 ? 50 : tamanoPagina;

        var q = _db.Productos.AsNoTracking().AsQueryable();
        if (!incluirInactivos) q = q.Where(p => p.Estatus == Estatus.Activo);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var like = BusquedaHelper.ParaLike(texto);
            q = q.Where(p => EF.Functions.ILike(p.CodigoClave, like)
                          || EF.Functions.ILike(p.Descripcion, like));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(p => p.Descripcion)
            .Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina)
            .Select(p => Map(p)).ToListAsync(ct);

        return new ProductosPaginaResponse(items, total, pagina, tamanoPagina);
    }

    public async Task<ProductoDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        var p = await _db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.IdProducto == id, ct);
        return p is null ? null : Map(p);
    }

    public async Task<ResultadoProducto> CrearAsync(CrearProductoRequest req, CancellationToken ct = default)
    {
        var error = ValidacionProducto.Validar(req.CodigoClave, req.Descripcion, req.Costo,
            req.PrecioSugerido, req.PrecioMinimo, req.StockMini);
        if (error is not null) return ResultadoProducto.Fallo(error);

        var codigo = req.CodigoClave.Trim();
        if (await _db.Productos.AnyAsync(p => p.CodigoClave == codigo, ct))
            return ResultadoProducto.Fallo("Ya existe un producto con ese código/clave.");

        var prod = new Producto
        {
            CodigoClave = codigo,
            Descripcion = req.Descripcion.Trim(),
            Costo = req.Costo,
            PrecioSugerido = req.PrecioSugerido,
            PrecioMinimo = req.PrecioMinimo,
            StockMini = req.StockMini,
            Estatus = Estatus.Activo
        };
        _db.Productos.Add(prod);
        await _db.SaveChangesAsync(ct);
        return ResultadoProducto.Ok(Map(prod));
    }

    public async Task<ResultadoProducto> ActualizarAsync(long id, ActualizarProductoRequest req, CancellationToken ct = default)
    {
        var error = ValidacionProducto.Validar(req.CodigoClave, req.Descripcion, req.Costo,
            req.PrecioSugerido, req.PrecioMinimo, req.StockMini);
        if (error is not null) return ResultadoProducto.Fallo(error);

        var prod = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
        if (prod is null) return ResultadoProducto.NoExiste($"No se encontró el producto #{id}.");

        var codigo = req.CodigoClave.Trim();
        if (await _db.Productos.AnyAsync(p => p.CodigoClave == codigo && p.IdProducto != id, ct))
            return ResultadoProducto.Fallo("Ya existe otro producto con ese código/clave.");

        prod.CodigoClave = codigo;
        prod.Descripcion = req.Descripcion.Trim();
        prod.Costo = req.Costo;
        prod.PrecioSugerido = req.PrecioSugerido;
        prod.PrecioMinimo = req.PrecioMinimo;
        prod.StockMini = req.StockMini;
        await _db.SaveChangesAsync(ct);
        return ResultadoProducto.Ok(Map(prod));
    }

    public async Task<ResultadoProducto> DarDeBajaAsync(long id, CancellationToken ct = default)
    {
        var prod = await _db.Productos.FirstOrDefaultAsync(p => p.IdProducto == id, ct);
        if (prod is null) return ResultadoProducto.NoExiste($"No se encontró el producto #{id}.");

        prod.Estatus = Estatus.Inactivo;
        await _db.SaveChangesAsync(ct);
        return ResultadoProducto.Ok(Map(prod));
    }

    private static ProductoDto Map(Producto p) => new(
        p.IdProducto, p.CodigoClave, p.Descripcion, p.Costo, p.PrecioSugerido,
        p.PrecioMinimo, p.StockMini, p.Estatus == Estatus.Activo);
}
