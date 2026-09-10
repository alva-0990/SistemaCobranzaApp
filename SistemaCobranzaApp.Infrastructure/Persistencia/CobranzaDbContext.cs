using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Infrastructure.Persistencia;

public class CobranzaDbContext : DbContext
{
    public CobranzaDbContext(DbContextOptions<CobranzaDbContext> options) : base(options)
    {
    }

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<CatalogoCondicion> Condiciones => Set<CatalogoCondicion>();
    public DbSet<CatalogoClasificacion> Clasificaciones => Set<CatalogoClasificacion>();
    public DbSet<CatalogoMovimientos> Movimientos => Set<CatalogoMovimientos>();
    public DbSet<CatalogoEstatusVenta> EstatusVenta => Set<CatalogoEstatusVenta>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cobrador> Cobradores => Set<Cobrador>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<Ruta> Rutas => Set<Ruta>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<VentaPlan> VentaPlanes => Set<VentaPlan>();
    public DbSet<InventarioCentral> InventarioCentral => Set<InventarioCentral>();
    public DbSet<InventarioZona> InventarioZona => Set<InventarioZona>();
    public DbSet<InventarioMovimiento> InventarioMovimientos => Set<InventarioMovimiento>();
    public DbSet<PropuestaEnvio> PropuestasEnvio => Set<PropuestaEnvio>();
    public DbSet<PropuestaEnvioDetalle> PropuestasEnvioDetalle => Set<PropuestaEnvioDetalle>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Tipo enum de PostgreSQL
        b.HasPostgresEnum<Estatus>("estatus_tipo");

        // 🟢 CONVERSOR UNIVERSAL (Compatible con SQLite y Postgres)
        var estatusConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Estatus, string>(
            v => v.ToString(), // Al guardar: Envía "Activo" o "Inactivo" como texto
            v => (Estatus)Enum.Parse(typeof(Estatus), v) // Al leer: Lo transforma al Enum de C#
        );

        
        b.Entity<Rol>(e =>
        {
            e.ToTable("rol");
            e.HasKey(x => x.IdRol);
            e.Property(x => x.IdRol).HasColumnName("idrol");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<CatalogoCondicion>(e =>
        {
            e.ToTable("catalogocondicion");
            e.HasKey(x => x.IdCondicion);
            e.Property(x => x.IdCondicion).HasColumnName("idcondicion");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<CatalogoClasificacion>(e =>
        {
            e.ToTable("catalogoclasificacion");
            e.HasKey(x => x.IdClasificacion);
            e.Property(x => x.IdClasificacion).HasColumnName("idclasificacion");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<CatalogoMovimientos>(e =>
        {
            e.ToTable("catalogomovimientos");
            e.HasKey(x => x.IdMovimiento);
            e.Property(x => x.IdMovimiento).HasColumnName("idmovimiento");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<CatalogoEstatusVenta>(e =>
        {
            e.ToTable("catalogoestatusventa");
            e.HasKey(x => x.IdEstatusVenta);
            e.Property(x => x.IdEstatusVenta).HasColumnName("idestatusventa");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<Zona>(e =>
        {
            e.ToTable("zona");
            e.HasKey(x => x.IdZona);
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
        });

        b.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.IdUsuario);
            e.Property(x => x.IdUsuario).HasColumnName("idusuario");
            e.Property(x => x.IdRol).HasColumnName("idrol");
            e.Property(x => x.Usuario_).HasColumnName("usuario");
            e.Property(x => x.PasswordHash).HasColumnName("passwordhash");
            e.Property(x => x.NombreCompleto).HasColumnName("nombrecompleto");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
            e.Property(x => x.FechaAlta).HasColumnName("fechaalta");
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.IdRol);
        });

        b.Entity<Cobrador>(e =>
        {
            e.ToTable("cobradores");
            e.HasKey(x => x.IdCobrador);
            e.Property(x => x.IdCobrador).HasColumnName("idcobrador");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Vendedor>(e =>
        {
            e.ToTable("vendedores");
            e.HasKey(x => x.IdVendedor);
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Ruta>(e =>
        {
            e.ToTable("ruta");
            e.HasKey(x => x.IdRuta);
            e.Property(x => x.IdRuta).HasColumnName("idruta");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Cliente>(e =>
        {
            e.ToTable("clientes");
            e.HasKey(x => x.IdCliente);
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.ApPaterno).HasColumnName("appaterno");
            e.Property(x => x.ApMaterno).HasColumnName("apmaterno");
            e.Property(x => x.IdRuta).HasColumnName("idruta");
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.IdClasificacion).HasColumnName("idclasificacion");
            e.Property(x => x.FechaAlta).HasColumnName("fechaalta");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion(estatusConverter).HasColumnType("text");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
            e.HasOne(x => x.Ruta).WithMany().HasForeignKey(x => x.IdRuta);
            e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.IdVendedor);
            e.HasOne(x => x.Clasificacion).WithMany().HasForeignKey(x => x.IdClasificacion);
        });

        b.Entity<Producto>(e =>
        {
            e.ToTable("productos");
            e.HasKey(x => x.IdProducto);
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.CodigoClave).HasColumnName("codigoclave");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Costo).HasColumnName("costo").HasColumnType("numeric(14,2)");
            e.Property(x => x.PrecioSugerido).HasColumnName("preciosugerido").HasColumnType("numeric(14,2)");
            e.Property(x => x.PrecioMinimo).HasColumnName("preciominimo").HasColumnType("numeric(14,2)");
            e.Property(x => x.StockMini).HasColumnName("stockmini");
            e.Property(x => x.Estatus)
                .HasColumnName("estatus")
                .HasConversion(estatusConverter)
                .HasColumnType("text"); 
        });

        b.Entity<Venta>(e =>
        {
            e.ToTable("venta");
            e.HasKey(x => x.IdVenta);
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.FechaCompra).HasColumnName("fechacompra");
            e.Property(x => x.FechaUltimoPago).HasColumnName("fechaultimopago");
            e.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(14,2)");
            e.Property(x => x.PagoRequerido).HasColumnName("pagorequerido").HasColumnType("numeric(14,2)");
            e.Property(x => x.Enganche).HasColumnName("enganche").HasColumnType("numeric(14,2)");
            e.Property(x => x.TotalAbonos).HasColumnName("totalabonos").HasColumnType("numeric(14,2)");
            e.Property(x => x.SaldoPagado).HasColumnName("saldopagado").HasColumnType("numeric(14,2)");
            e.Property(x => x.SaldoPendiente).HasColumnName("saldopendiente").HasColumnType("numeric(14,2)");
            e.Property(x => x.SaldoPagadoDesc).HasColumnName("saldopagadodesc").HasColumnType("numeric(14,2)");
            e.Property(x => x.TotalFinal).HasColumnName("totalfinal").HasColumnType("numeric(14,2)");
            e.Property(x => x.IdCondicion).HasColumnName("idcondicion");
            e.Property(x => x.IdEstatusVenta).HasColumnName("idestatusventa");
            e.Property(x => x.CodigoVenta).HasColumnName("codigoventa");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.IdCliente);
            e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.IdVendedor);
            e.HasOne(x => x.Condicion).WithMany().HasForeignKey(x => x.IdCondicion);
            e.HasOne(x => x.EstatusVenta).WithMany().HasForeignKey(x => x.IdEstatusVenta);
        });

        b.Entity<DetalleVenta>(e =>
        {
            e.ToTable("detallesventa");
            e.HasKey(x => x.IdDetalle);
            e.Property(x => x.IdDetalle).HasColumnName("iddetalle");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.PrecioCobrado).HasColumnName("preciocobrado").HasColumnType("numeric(14,2)");
            e.Property(x => x.Importe).HasColumnName("importe").HasColumnType("numeric(14,2)");
            e.HasOne(x => x.Venta).WithMany(v => v.Detalles).HasForeignKey(x => x.IdVenta);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto);
        });

        b.Entity<Pago>(e =>
        {
            e.ToTable("pagos");
            e.HasKey(x => x.IdPago);
            e.Property(x => x.IdPago).HasColumnName("idpago");
            e.Property(x => x.PagoUuid).HasColumnName("pagouuid");
            e.HasIndex(x => x.PagoUuid).IsUnique();
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.SaldoAnterior).HasColumnName("saldoanterior").HasColumnType("numeric(14,2)");
            e.Property(x => x.SaldoActual).HasColumnName("saldoactual").HasColumnType("numeric(14,2)");
            e.Property(x => x.MontoAbona).HasColumnName("montoabona").HasColumnType("numeric(14,2)");
            e.Property(x => x.IdCobrador).HasColumnName("idcobrador");
            e.Property(x => x.FechaPago).HasColumnName("fechapago");
            e.Property(x => x.IdMovimiento).HasColumnName("idmovimiento");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FolioPago).HasColumnName("foliopago");
            e.Property(x => x.ConsecutivoPago).HasColumnName("consecutivopago");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
            e.HasOne(x => x.Venta).WithMany(v => v.Pagos).HasForeignKey(x => x.IdVenta);
            e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.IdCliente);
            e.HasOne(x => x.Cobrador).WithMany().HasForeignKey(x => x.IdCobrador);
            e.HasOne(x => x.Movimiento).WithMany().HasForeignKey(x => x.IdMovimiento);
        });

        b.Entity<VentaPlan>(e =>
        {
            e.ToTable("ventaplan");
            e.HasKey(x => x.IdPlan);
            e.Property(x => x.IdPlan).HasColumnName("idplan");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.NumeroMeses).HasColumnName("numeromeses");
            e.Property(x => x.MontoLiquidacion).HasColumnName("montoliquidacion").HasColumnType("numeric(14,2)");
            e.Property(x => x.Activo).HasColumnName("activo");
            e.HasOne(x => x.Venta).WithMany().HasForeignKey(x => x.IdVenta);
        });

        b.Entity<InventarioCentral>(e =>
        {
            e.ToTable("inventariocentral");
            e.HasKey(x => x.IdInventarioCentral);
            e.Property(x => x.IdInventarioCentral).HasColumnName("idinventariocentral");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.FechaActualizacion).HasColumnName("fechaactualizacion");
            e.HasIndex(x => x.IdProducto).IsUnique();
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto);
        });

        b.Entity<InventarioZona>(e =>
        {
            e.ToTable("inventariozona");
            e.HasKey(x => x.IdInventarioZona);
            e.Property(x => x.IdInventarioZona).HasColumnName("idinventariozona");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.FechaActualizacion).HasColumnName("fechaactualizacion");
            e.HasIndex(x => new { x.IdZona, x.IdProducto }).IsUnique();
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto);
        });

        b.Entity<InventarioMovimiento>(e =>
        {
            e.ToTable("inventariomovimiento");
            e.HasKey(x => x.IdMovimientoInv);
            e.Property(x => x.IdMovimientoInv).HasColumnName("idmovimientoinv");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.TipoMovimiento).HasColumnName("tipomovimiento");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.ExistenciaAnterior).HasColumnName("existenciaanterior");
            e.Property(x => x.ExistenciaNueva).HasColumnName("existencianueva");
            e.Property(x => x.IdZonaOrigen).HasColumnName("idzonaorigen");
            e.Property(x => x.IdZonaDestino).HasColumnName("idzonadestino");
            e.Property(x => x.IdPropuesta).HasColumnName("idpropuesta");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FechaMovimiento).HasColumnName("fechamovimiento");
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto);
        });

        b.Entity<PropuestaEnvio>(e =>
        {
            e.ToTable("propuestaenvio");
            e.HasKey(x => x.IdPropuesta);
            e.Property(x => x.IdPropuesta).HasColumnName("idpropuesta");
            e.Property(x => x.IdZonaDestino).HasColumnName("idzonadestino");
            e.Property(x => x.Estatus).HasColumnName("estatus");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.IdUsuarioConfirma).HasColumnName("idusuarioconfirma");
            e.Property(x => x.FechaCreacion).HasColumnName("fechacreacion");
            e.Property(x => x.FechaResolucion).HasColumnName("fecharesolucion");
            e.HasOne(x => x.ZonaDestino).WithMany().HasForeignKey(x => x.IdZonaDestino);
        });

        b.Entity<PropuestaEnvioDetalle>(e =>
        {
            e.ToTable("propuestaenviodetalle");
            e.HasKey(x => x.IdPropuestaDetalle);
            e.Property(x => x.IdPropuestaDetalle).HasColumnName("idpropuestadetalle");
            e.Property(x => x.IdPropuesta).HasColumnName("idpropuesta");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.HasOne(x => x.Propuesta).WithMany(p => p.Detalles).HasForeignKey(x => x.IdPropuesta);
            e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.IdProducto);
        });
    }
}
