using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Domain.Enums;

namespace SistemaCobranzaApp.Infrastructure.Persistencia;

public class CobranzaDbContext : DbContext
{
    public CobranzaDbContext(DbContextOptions<CobranzaDbContext> options) : base(options)
    {
    }

    public DbSet<AsignacionJornada> AsignacionesJornada => Set<AsignacionJornada>();

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<CatalogoCondicion> Condiciones => Set<CatalogoCondicion>();
    public DbSet<CatalogoClasificacion> Clasificaciones => Set<CatalogoClasificacion>();
    public DbSet<CatalogoMovimientos> Movimientos => Set<CatalogoMovimientos>();
    public DbSet<CatalogoEstatusVenta> EstatusVenta => Set<CatalogoEstatusVenta>();
    public DbSet<CatalogoMes> CatalogoMeses => Set<CatalogoMes>();
    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cobrador> Cobradores => Set<Cobrador>();
    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<Ruta> Rutas => Set<Ruta>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<CatalogoCategoria> CatalogoCategorias => Set<CatalogoCategoria>();
    public DbSet<CatalogoMarca> CatalogoMarcas => Set<CatalogoMarca>();
    public DbSet<CatalogoColor> CatalogoColores => Set<CatalogoColor>();
    public DbSet<CatalogoTalla> CatalogoTallas => Set<CatalogoTalla>();

    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<VentaPlan> VentaPlan => Set<VentaPlan>();
    public DbSet<InventarioCentral> InventarioCentral => Set<InventarioCentral>();
    public DbSet<InventarioZona> InventarioZona => Set<InventarioZona>();
    public DbSet<InventarioMovimiento> InventarioMovimientos => Set<InventarioMovimiento>();
    public DbSet<PropuestaEnvio> PropuestasEnvio => Set<PropuestaEnvio>();
    public DbSet<PropuestaEnvioDetalle> PropuestasEnvioDetalle => Set<PropuestaEnvioDetalle>();
    public DbSet<CatalogoFrecuenciaPago> CatalogoFrecuenciasPago => Set<CatalogoFrecuenciaPago>();
    public DbSet<VentaCuota> VentaCuotas => Set<VentaCuota>();
    public DbSet<ParametroSistema> ParametrosSistema => Set<ParametroSistema>();
    public DbSet<Empresa> Empresas => Set<Empresa>();

    public DbSet<CatalogoCategoriaGasto> CatalogoCategoriasGasto => Set<CatalogoCategoriaGasto>();

    public DbSet<CuentaGasto> CuentasGasto => Set<CuentaGasto>();

    public DbSet<Gasto> Gastos => Set<Gasto>();

    public DbSet<GastoPago> GastosPagos => Set<GastoPago>();

    public DbSet<MovimientoCuentaGasto> MovimientosCuentaGasto => Set<MovimientoCuentaGasto>();

    public DbSet<GastoComprobante> GastosComprobantes => Set<GastoComprobante>();

    public DbSet<CatalogoComision> CatalogoComisiones => Set<CatalogoComision>();
    public DbSet<ConfiguracionComision> ConfiguracionesComisiones => Set<ConfiguracionComision>();
    public DbSet<ComisionPorPagar> ComisionesPorPagar => Set<ComisionPorPagar>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AsignacionJornada>(e =>
        {
            e.ToTable("asignacionjornada");
            e.HasKey(x => new { x.IdUsuario, x.IdRuta });
            e.Property(x => x.IdUsuario).HasColumnName("idusuario");
            e.Property(x => x.IdRuta).HasColumnName("idruta");
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.IdCobrador).HasColumnName("idcobrador");
        });

        b.Entity<Rol>(e =>
        {
            e.ToTable("rol");
            e.HasKey(x => x.IdRol);
            e.Property(x => x.IdRol).HasColumnName("idrol");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<CatalogoCondicion>(e =>
        {
            e.ToTable("catalogocondicion");
            e.HasKey(x => x.IdCondicion);
            e.Property(x => x.IdCondicion).HasColumnName("idcondicion");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<CatalogoClasificacion>(e =>
        {
            e.ToTable("catalogoclasificacion");
            e.HasKey(x => x.IdClasificacion);
            e.Property(x => x.IdClasificacion).HasColumnName("idclasificacion");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<CatalogoMovimientos>(e =>
        {
            e.ToTable("catalogomovimientos");
            e.HasKey(x => x.IdMovimiento);
            e.Property(x => x.IdMovimiento).HasColumnName("idmovimiento");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<CatalogoEstatusVenta>(e =>
        {
            e.ToTable("catalogoestatusventa");
            e.HasKey(x => x.IdEstatusVenta);
            e.Property(x => x.IdEstatusVenta).HasColumnName("idestatusventa");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<CatalogoMes>(e =>
        {
            e.ToTable("catalogomeses");
            e.HasKey(x => x.IdMes);
            e.Property(x => x.IdMes).HasColumnName("idmes");
            e.Property(x => x.NumMeses).HasColumnName("nummeses");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        b.Entity<Zona>(e =>
        {
            e.ToTable("zona");
            e.HasKey(x => x.IdZona);
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
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
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
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
            e.Property(x => x.IdUsuario).HasColumnName("idusuario");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Vendedor>(e =>
        {
            e.ToTable("vendedores");
            e.HasKey(x => x.IdVendedor);
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.IdUsuario).HasColumnName("idusuario");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Ruta>(e =>
        {
            e.ToTable("ruta");
            e.HasKey(x => x.IdRuta);
            e.Property(x => x.IdRuta).HasColumnName("idruta");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.IdZona).HasColumnName("idzona");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
            e.HasOne(x => x.Zona).WithMany().HasForeignKey(x => x.IdZona);
        });

        b.Entity<Cliente>(e =>
        {
            e.ToTable("clientes");
            e.Property(x => x.ClienteUuid).HasColumnName("clienteuuid");
            e.HasIndex(x => x.ClienteUuid).IsUnique();
            e.HasKey(x => x.IdCliente);
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.ApPaterno).HasColumnName("appaterno");
            e.Property(x => x.ApMaterno).HasColumnName("apmaterno");
            e.Property(x => x.TelefonoPrincipal).HasColumnName("telefonoprincipal");
            e.Property(x => x.TelefonoSecundario).HasColumnName("telefonosecundario");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.Direccion).HasColumnName("direccion");
            e.Property(x => x.CodigoPostal).HasColumnName("codigopostal");
            e.Property(x => x.ReferenciaUbicacion).HasColumnName("referenciaubicacion");
            e.Property(x => x.Latitud).HasColumnName("latitud").HasColumnType("numeric(10,8)");
            e.Property(x => x.Longitud).HasColumnName("longitud").HasColumnType("numeric(11,8)");
            e.Property(x => x.IdRuta).HasColumnName("idruta");
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.IdClasificacion).HasColumnName("idclasificacion");
            e.Property(x => x.FechaAlta).HasColumnName("fechaalta");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
            e.HasOne(x => x.Ruta).WithMany().HasForeignKey(x => x.IdRuta);
            e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.IdVendedor);
            e.HasOne(x => x.Clasificacion).WithMany().HasForeignKey(x => x.IdClasificacion);
        });


// ---- PLAN DE AMORTIZACIÓN (CUOTAS INDIVIDUALES) ----
        b.Entity<VentaCuota>(e =>
        {
            e.ToTable("ventacuotas");
            e.HasKey(x => x.IdCuota);
            e.Property(x => x.IdCuota).HasColumnName("idcuota");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.NumeroCuota).HasColumnName("numerocuota");
            e.Property(x => x.FechaVencimiento).HasColumnName("fechavencimiento");
            e.Property(x => x.MontoCuota).HasColumnName("montocuota");
            e.Property(x => x.MontoInteres).HasColumnName("montointeres");
            e.Property(x => x.SaldoCuota).HasColumnName("saldocuota");
            e.Property(x => x.EstatusCuota).HasColumnName("estatuscuota");

            // Relación hija -> padre con borrado en cascada
            e.HasOne(x => x.Venta)
                .WithMany(v => v.Cuotas)
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- PARÁMETROS DEL SISTEMA (promociones y configuración dinámica) ----
        b.Entity<ParametroSistema>(e =>
        {
            e.ToTable("parametrossistema");
            e.HasKey(x => x.Clave);
            e.Property(x => x.Clave).HasColumnName("clave");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.ValorNumerico).HasColumnName("valornumerico");
            e.Property(x => x.ValorTexto).HasColumnName("valortexto");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
        });

// ---- CABECERA PRINCIPAL DE VENTAS ----
        b.Entity<Venta>(e =>
        {
            e.Property(x => x.VentaUuid).HasColumnName("ventauuid");
            e.HasIndex(x => x.VentaUuid).IsUnique();
            e.ToTable("venta");
            e.HasKey(x => x.IdVenta);

            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor");
            e.Property(x => x.CodigoVenta).HasColumnName("codigoventa");
            e.Property(x => x.FechaCompra).HasColumnName("fechacompra");
            e.Property(x => x.IdCondicion).HasColumnName("idcondicion");
            e.Property(x => x.Subtotal).HasColumnName("subtotal");
            e.Property(x => x.DescuentoGlobal).HasColumnName("descuentoglobal");
            e.Property(x => x.TotalFinal).HasColumnName("totalfinal");
            e.Property(x => x.Enganche).HasColumnName("enganche");
            e.Property(x => x.MontoFinanciado).HasColumnName("montofinanciado")
                .ValueGeneratedOnAddOrUpdate(); // Campo STORED en Postgres
            e.Property(x => x.TotalComisionVenta).HasColumnName("totalcomisionventa");
            e.Property(x => x.IdFrecuenciaPago).HasColumnName("idfrecuenciapago");
            e.Property(x => x.NumeroCuotas).HasColumnName("numerocuotas");
            e.Property(x => x.MontoCuotaFija).HasColumnName("montocuotafija");
            e.Property(x => x.TasaInteres).HasColumnName("tasainteres");
            e.Property(x => x.FechaPrimerVence).HasColumnName("fechaprimervence");
            e.Property(x => x.TotalAbonado).HasColumnName("totalabonado");
            e.Property(x => x.SaldoPendiente).HasColumnName("saldopendiente");
            e.Property(x => x.FechaUltimoPago).HasColumnName("fechaultimopago");
            e.Property(x => x.IdEstatusVenta).HasColumnName("idestatusventa");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");
            e.Property(x => x.PagosConsecutivosPuntuales).HasColumnName("pagosconsecutivospuntuales");
            e.Property(x => x.VentasUnificadas).HasColumnName("ventasunificadas");

            // ===========================================================================
            // 🎯 RELACIONES BIDIRECCIONALES COMPLETAS (Cierran el circuito con las hijas)
            // ===========================================================================

            // Enlace explícito con la lista de Cuotas de Amortización
            e.HasMany(x => x.Cuotas)
                .WithOne(c => c.Venta)
                .HasForeignKey(c => c.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // Enlace explícito con la lista de Detalles de Artículos
            e.HasMany(x => x.Detalles)
                .WithOne(d => d.Venta)
                .HasForeignKey(d => d.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // Enlace explícito con la lista de Planes Promocionales por Meses
            e.HasMany(x => x.PlanesPromocionales)
                .WithOne(p => p.Venta)
                .HasForeignKey(p => p.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // Enlace explícito con la lista de Historial de Pagos
            e.HasMany(x => x.Pagos)
                .WithOne(p => p.Venta)
                .HasForeignKey(p => p.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // ===========================================================================
            // 🎯 RESTRICCIONES DE OBJETOS PADRE (Catálogos y Entidades Maestras)
            // ===========================================================================

            e.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Vendedor)
                .WithMany()
                .HasForeignKey(x => x.IdVendedor)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Condicion)
                .WithMany()
                .HasForeignKey(x => x.IdCondicion)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.FrecuenciaPago)
                .WithMany()
                .HasForeignKey(x => x.IdFrecuenciaPago)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.EstatusVenta)
                .WithMany()
                .HasForeignKey(x => x.IdEstatusVenta)
                .OnDelete(DeleteBehavior.Restrict);
        });

// ---- DETALLE DE ARTÍCULOS VENDIDOS ----
        b.Entity<DetalleVenta>(e =>
        {
            e.ToTable("detallesventa");
            e.HasKey(x => x.IdDetalle);
            e.Property(x => x.IdDetalle).HasColumnName("iddetalle");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.Cantidad).HasColumnName("cantidad");
            e.Property(x => x.PrecioCobrado).HasColumnName("preciocobrado");
            e.Property(x => x.Importe).HasColumnName("importe");
            e.Property(x => x.PorcentajeComisionAplicado).HasColumnName("porcentajecomisionaplicado");
            e.Property(x => x.MontoComisionGenerado).HasColumnName("montocomisiongenerado");

            // DetalleVenta -> Venta
            e.HasOne(x => x.Venta)
                .WithMany(v => v.Detalles)
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // DetalleVenta -> Producto
            e.HasOne(x => x.Producto)
                .WithMany()
                .HasForeignKey(x => x.IdProducto)
                .OnDelete(DeleteBehavior.Restrict);
        });

// ---- OPCIONES DE LIQUIDACIÓN / PLANES POR MESES ----
        b.Entity<VentaPlan>(e =>
        {
            e.ToTable("ventaplan");
            e.HasKey(x => x.IdPlan);
            e.Property(x => x.IdPlan).HasColumnName("idplan");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.NumeroMeses).HasColumnName("numeromeses");
            e.Property(x => x.MontoLiquidacion).HasColumnName("montoliquidacion");
            e.Property(x => x.Activo).HasColumnName("activo");

            e.HasOne(x => x.Venta)
                .WithMany(v => v.PlanesPromocionales)
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);
        });

// ---- HISTORIAL DE PAGOS ----
        b.Entity<Pago>(e =>
        {
            e.ToTable("pagos");
            e.HasKey(x => x.IdPago);
            e.Property(x => x.IdPago).HasColumnName("idpago");
            e.Property(x => x.PagoUuid).HasColumnName("pagouuid");
            e.Property(x => x.IdVenta).HasColumnName("idventa");
            e.Property(x => x.IdCliente).HasColumnName("idcliente");
            e.Property(x => x.SaldoAnterior).HasColumnName("saldoanterior");
            e.Property(x => x.SaldoActual).HasColumnName("saldoactual");
            e.Property(x => x.MontoAbona).HasColumnName("montoabona");
            e.Property(x => x.IdCobrador).HasColumnName("idcobrador");
            e.Property(x => x.FechaPago).HasColumnName("fechapago");
            e.Property(x => x.IdMovimiento).HasColumnName("idmovimiento");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");
            e.Property(x => x.FolioPago).HasColumnName("foliopago");
            e.Property(x => x.ConsecutivoPago).HasColumnName("consecutivopago");
            e.Property(x => x.FechaModificacion).HasColumnName("fechamodificacion");

            e.HasOne(x => x.Venta)
                .WithMany(v => v.Pagos)
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            // Navegaciones restantes mapeadas a sus columnas reales para evitar que EF
            // genere columnas "shadow" (ClienteIdCliente, CobradorIdCobrador, MovimientoIdMovimiento).
            e.HasOne(x => x.Cliente)
                .WithMany()
                .HasForeignKey(x => x.IdCliente)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Cobrador)
                .WithMany()
                .HasForeignKey(x => x.IdCobrador)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Movimiento)
                .WithMany()
                .HasForeignKey(x => x.IdMovimiento)
                .OnDelete(DeleteBehavior.Restrict);
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

        // ---- CATÁLOGO CATEGORÍAS ----
        b.Entity<CatalogoCategoria>(e =>
        {
            e.ToTable("catalogocategorias");
            e.HasKey(x => x.IdCategoria);
            e.Property(x => x.IdCategoria).HasColumnName("idcategoria");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        // ---- CATÁLOGO MARCAS ----
        b.Entity<CatalogoMarca>(e =>
        {
            e.ToTable("catalogomarcas");
            e.HasKey(x => x.IdMarca);
            e.Property(x => x.IdMarca).HasColumnName("idmarca");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        // ---- CATÁLOGO COLORES ----
        b.Entity<CatalogoColor>(e =>
        {
            e.ToTable("catalogocolores");
            e.HasKey(x => x.IdColor);
            e.Property(x => x.IdColor).HasColumnName("idcolor");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        // ---- CATÁLOGO TALLAS ----
        b.Entity<CatalogoTalla>(e =>
        {
            e.ToTable("catalogotallas");
            e.HasKey(x => x.IdTalla);
            e.Property(x => x.IdTalla).HasColumnName("idtalla");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        // ---- TABLA PRODUCTOS AMPLIADA ----
        b.Entity<Producto>(e =>
        {
            e.ToTable("productos");
            e.HasKey(x => x.IdProducto);
            e.Property(x => x.IdProducto).HasColumnName("idproducto");
            e.Property(x => x.CodigoClave).HasColumnName("codigoclave");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.Costo).HasColumnName("costo");
            e.Property(x => x.PrecioSugerido).HasColumnName("preciosugerido");
            e.Property(x => x.PrecioMinimo).HasColumnName("preciominimo");
            e.Property(x => x.StockMini).HasColumnName("stockmini");

            // Columnas de llaves foráneas
            e.Property(x => x.IdCategoria).HasColumnName("idcategoria");
            e.Property(x => x.IdMarca).HasColumnName("idmarca");
            e.Property(x => x.IdColor).HasColumnName("idcolor");
            e.Property(x => x.IdTalla).HasColumnName("idtalla");

            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();

            // Relaciones (Llaves Foráneas)
            e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.IdCategoria);
            e.HasOne(x => x.Marca).WithMany().HasForeignKey(x => x.IdMarca);
            e.HasOne(x => x.Color).WithMany().HasForeignKey(x => x.IdColor);
            e.HasOne(x => x.Talla).WithMany().HasForeignKey(x => x.IdTalla);
        });

        // ---- CATÁLOGO FRECUENCIA PAGO ----
        b.Entity<CatalogoFrecuenciaPago>(e =>
        {
            e.ToTable("catalogofrecuenciapago");
            e.HasKey(x => x.IdFrecuenciaPago);
            e.Property(x => x.IdFrecuenciaPago).HasColumnName("idfrecuenciapago");
            e.Property(x => x.Descripcion).HasColumnName("descripcion");
            e.Property(x => x.DiasPeriodo).HasColumnName("diasperiodo");
            e.Property(x => x.Estatus).HasColumnName("estatus").HasConversion<int>();
        });

        // ---- EMPRESAS ----
        // ---- EMPRESAS (Optimizado para PostgreSQL con snake_case) ----
        b.Entity<Empresa>(e =>
        {
            e.ToTable("empresas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");

            // Datos Identificativos y Fiscales
            e.Property(x => x.RazonSocial).HasColumnName("razon_social").HasMaxLength(250).IsRequired();
            e.Property(x => x.NombreComercial).HasColumnName("nombre_comercial").HasMaxLength(200).IsRequired();
            e.Property(x => x.NombreCorto).HasColumnName("nombre_corto").HasMaxLength(50);
            e.Property(x => x.Rfc_o_Nit).HasColumnName("rfc_o_nit").HasMaxLength(20).IsRequired();
            e.Property(x => x.RegimenFiscal).HasColumnName("regimen_fiscal").HasMaxLength(150);
            e.Property(x => x.AnioFundacion).HasColumnName("anio_fundacion"); // 👈 Aquí tronaba el query

            // Identidad de Marca
            e.Property(x => x.Lema).HasColumnName("lema").HasMaxLength(250);
            e.Property(x => x.LogoUrl).HasColumnName("logo_url").HasColumnType("text");
            e.Property(x => x.LogoBlancoUrl).HasColumnName("logo_blanco_url").HasColumnType("text");

            // Información de Contacto
            e.Property(x => x.TelefonoPrincipal).HasColumnName("telefono_principal").HasMaxLength(20);
            e.Property(x => x.TelefonoSecundario).HasColumnName("telefono_secundario").HasMaxLength(20);
            e.Property(x => x.CorreoContacto).HasColumnName("correo_contacto").HasMaxLength(150);
            e.Property(x => x.CorreoSoporte).HasColumnName("correo_soporte").HasMaxLength(150);
            e.Property(x => x.SitioWeb).HasColumnName("sitio_web").HasMaxLength(250);

            // Redes Sociales
            e.Property(x => x.FacebookUrl).HasColumnName("facebook_url").HasMaxLength(250);
            e.Property(x => x.InstagramUrl).HasColumnName("instagram_url").HasMaxLength(250);
            e.Property(x => x.LinkedinUrl).HasColumnName("linkedin_url").HasMaxLength(250);

            // Dirección Física
            e.Property(x => x.Calle).HasColumnName("calle").HasMaxLength(150);
            e.Property(x => x.NumeroExterior).HasColumnName("numero_exterior").HasMaxLength(20);
            e.Property(x => x.NumeroInterior).HasColumnName("numero_interior").HasMaxLength(20);
            e.Property(x => x.Colonia).HasColumnName("colonia").HasMaxLength(100);
            e.Property(x => x.CodigoPostal).HasColumnName("codigo_postal").HasMaxLength(15);
            e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(100);
            e.Property(x => x.Estado_o_Provincia).HasColumnName("estado_o_provincia").HasMaxLength(100);
            e.Property(x => x.Pais).HasColumnName("pais").HasMaxLength(100);

            // Configuración de Operación
            e.Property(x => x.MonedaPorDefecto).HasColumnName("moneda_por_defecto").HasMaxLength(5);
            e.Property(x => x.SimboloMoneda).HasColumnName("simbolo_moneda").HasMaxLength(5);
            e.Property(x => x.PorcentajeImpuesto).HasColumnName("porcentaje_impuesto").HasPrecision(18, 2);
            e.Property(x => x.HorarioAtencion).HasColumnName("horario_atencion").HasMaxLength(100);
        });

        // Catálogo de categorías de gastos
        b.Entity<CatalogoCategoriaGasto>(e =>
        {
            e.ToTable("catalogocategoriasgasto");
            e.HasKey(x => x.IdCategoriaGasto);

            e.Property(x => x.IdCategoriaGasto)
                .HasColumnName("idcategoriagasto")
                .ValueGeneratedOnAdd();

            e.Property(x => x.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.Descripcion)
                .HasColumnName("descripcion")
                .HasMaxLength(250);

            e.Property(x => x.RequiereComprobante)
                .HasColumnName("requierecomprobante")
                .HasDefaultValue(false);

            e.Property(x => x.Estatus)
                .HasColumnName("estatus")
                .HasDefaultValue(1);

            e.Property(x => x.FechaAlta)
                .HasColumnName("fechaalta")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.FechaModificacion)
                .HasColumnName("fechamodificacion")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.HasIndex(x => x.Nombre)
                .HasDatabaseName("UX_CategoriaGasto_Nombre")
                .IsUnique()
                .HasMethod("btree")
                .HasFilter(null);
        });

        // Cuentas de gastos
        b.Entity<CuentaGasto>(e =>
        {
            e.ToTable("cuentasgasto");
            e.HasKey(x => x.IdCuentaGasto);

            e.Property(x => x.IdCuentaGasto)
                .HasColumnName("idcuentagasto")
                .ValueGeneratedOnAdd();

            e.Property(x => x.Nombre)
                .HasColumnName("nombre")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.TipoCuenta)
                .HasColumnName("tipocuenta")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.Moneda)
                .HasColumnName("moneda")
                .HasMaxLength(5)
                .HasDefaultValue("MXN")
                .IsRequired();

            e.Property(x => x.SaldoInicial)
                .HasColumnName("saldoinicial")
                .HasPrecision(14, 2)
                .HasDefaultValue(0);

            e.Property(x => x.Estatus)
                .HasColumnName("estatus")
                .HasDefaultValue(1);

            e.Property(x => x.Observaciones)
                .HasColumnName("observaciones")
                .HasMaxLength(250);

            e.Property(x => x.FechaAlta)
                .HasColumnName("fechaalta")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.FechaModificacion)
                .HasColumnName("fechamodificacion")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.HasIndex(x => x.Nombre)
                .IsUnique();
        });

        // Gastos
        b.Entity<Gasto>(e =>
        {
            e.ToTable("gastos");
            e.HasKey(x => x.IdGasto);

            e.Property(x => x.IdGasto)
                .HasColumnName("idgasto")
                .ValueGeneratedOnAdd();

            e.Property(x => x.IdCategoriaGasto)
                .HasColumnName("idcategoriagasto")
                .IsRequired();

            e.Property(x => x.FolioGasto)
                .HasColumnName("foliogasto")
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.FechaGasto)
                .HasColumnName("fechagasto")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.Concepto)
                .HasColumnName("concepto")
                .HasMaxLength(250)
                .IsRequired();

            e.Property(x => x.Descripcion)
                .HasColumnName("descripcion")
                .HasColumnType("text");

            e.Property(x => x.Proveedor)
                .HasColumnName("proveedor")
                .HasMaxLength(200);

            e.Property(x => x.RFCProveedor)
                .HasColumnName("rfcproveedor")
                .HasMaxLength(20);

            e.Property(x => x.Referencia)
                .HasColumnName("referencia")
                .HasMaxLength(100);

            e.Property(x => x.Importe)
                .HasColumnName("importe")
                .HasPrecision(14, 2)
                .IsRequired();

            e.Property(x => x.Estatus)
                .HasColumnName("estatus")
                .HasMaxLength(20)
                .HasDefaultValue("PENDIENTE")
                .IsRequired();

            e.Property(x => x.IdZona)
                .HasColumnName("idzona");

            e.Property(x => x.IdCobrador)
                .HasColumnName("idcobrador");

            e.Property(x => x.IdVendedor)
                .HasColumnName("idvendedor");

            e.Property(x => x.IdCuentaPrevista)
                .HasColumnName("idcuentaprevista");

            e.Property(x => x.Observaciones)
                .HasColumnName("observaciones")
                .HasColumnType("text");

            e.Property(x => x.IdUsuarioRegistro)
                .HasColumnName("idusuarioregistro")
                .IsRequired();

            e.Property(x => x.IdUsuarioAutoriza)
                .HasColumnName("idusuarioautoriza");

            e.Property(x => x.FechaAutorizacion)
                .HasColumnName("fechaautorizacion")
                .HasColumnType("timestamp with time zone");

            e.Property(x => x.IdUsuarioCancelacion)
                .HasColumnName("idusuariocancelacion");

            e.Property(x => x.FechaCancelacion)
                .HasColumnName("fechacancelacion")
                .HasColumnType("timestamp with time zone");

            e.Property(x => x.MotivoCancelacion)
                .HasColumnName("motivocancelacion")
                .HasMaxLength(500);

            e.Property(x => x.FechaAlta)
                .HasColumnName("fechaalta")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.FechaModificacion)
                .HasColumnName("fechamodificacion")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.HasOne(x => x.Categoria)
                .WithMany(x => x.Gastos)
                .HasForeignKey(x => x.IdCategoriaGasto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Zona)
                .WithMany()
                .HasForeignKey(x => x.IdZona)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Cobrador)
                .WithMany()
                .HasForeignKey(x => x.IdCobrador)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Vendedor)
                .WithMany()
                .HasForeignKey(x => x.IdVendedor)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CuentaPrevista)
                .WithMany()
                .HasForeignKey(x => x.IdCuentaPrevista)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.FolioGasto)
                .IsUnique();

            e.HasIndex(x => x.FechaGasto)
                .HasDatabaseName("IX_Gastos_Fecha")
                .IsDescending();

            e.HasIndex(x => x.Estatus)
                .HasDatabaseName("IX_Gastos_Estatus");

            e.HasIndex(x => new { x.IdCategoriaGasto, x.FechaGasto })
                .HasDatabaseName("IX_Gastos_Categoria_Fecha")
                .IsDescending(false, true);

            e.HasIndex(x => new { x.IdZona, x.FechaGasto })
                .HasDatabaseName("IX_Gastos_Zona_Fecha")
                .IsDescending(false, true);

            e.HasIndex(x => new { x.IdCobrador, x.FechaGasto })
                .HasDatabaseName("IX_Gastos_Cobrador_Fecha")
                .IsDescending(false, true);

            e.HasIndex(x => new { x.IdVendedor, x.FechaGasto })
                .HasDatabaseName("IX_Gastos_Vendedor_Fecha")
                .IsDescending(false, true);
        });

        // Pagos de gastos
        b.Entity<GastoPago>(e =>
        {
            e.ToTable("gastospagos");
            e.HasKey(x => x.IdPagoGasto);

            e.Property(x => x.IdPagoGasto)
                .HasColumnName("idpagogasto")
                .ValueGeneratedOnAdd();

            e.Property(x => x.IdGasto)
                .HasColumnName("idgasto")
                .IsRequired();

            e.Property(x => x.IdCuentaGasto)
                .HasColumnName("idcuentagasto")
                .IsRequired();

            e.Property(x => x.FechaPago)
                .HasColumnName("fechapago")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.Importe)
                .HasColumnName("importe")
                .HasPrecision(14, 2)
                .IsRequired();

            e.Property(x => x.FormaPago)
                .HasColumnName("formapago")
                .HasMaxLength(20)
                .IsRequired();

            e.Property(x => x.Referencia)
                .HasColumnName("referencia")
                .HasMaxLength(100);

            e.Property(x => x.Observaciones)
                .HasColumnName("observaciones")
                .HasColumnType("text");

            e.Property(x => x.IdUsuarioRegistro)
                .HasColumnName("idusuarioregistro")
                .IsRequired();

            e.Property(x => x.FechaAlta)
                .HasColumnName("fechaalta")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.HasOne(x => x.Gasto)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdGasto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Cuenta)
                .WithMany(x => x.Pagos)
                .HasForeignKey(x => x.IdCuentaGasto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.IdGasto, x.FechaPago })
                .HasDatabaseName("IX_GastosPagos_Gasto_Fecha")
                .IsDescending(false, true);
        });

        // Movimientos de cuentas de gastos
        b.Entity<MovimientoCuentaGasto>(e =>
        {
            e.ToTable("movimientoscuentagasto");
            e.HasKey(x => x.IdMovimientoCuenta);

            e.Property(x => x.IdMovimientoCuenta)
                .HasColumnName("idmovimientocuenta")
                .ValueGeneratedOnAdd();

            e.Property(x => x.IdCuentaGasto)
                .HasColumnName("idcuentagasto")
                .IsRequired();

            e.Property(x => x.IdPagoGasto)
                .HasColumnName("idpagogasto");

            e.Property(x => x.TipoMovimiento)
                .HasColumnName("tipomovimiento")
                .HasMaxLength(10)
                .IsRequired();

            e.Property(x => x.Importe)
                .HasColumnName("importe")
                .HasPrecision(14, 2)
                .IsRequired();

            e.Property(x => x.FechaMovimiento)
                .HasColumnName("fechamovimiento")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(x => x.Concepto)
                .HasColumnName("concepto")
                .HasMaxLength(250)
                .IsRequired();

            e.Property(x => x.Referencia)
                .HasColumnName("referencia")
                .HasMaxLength(100);

            e.Property(x => x.Observaciones)
                .HasColumnName("observaciones")
                .HasColumnType("text");

            e.Property(x => x.IdUsuarioRegistro)
                .HasColumnName("idusuarioregistro")
                .IsRequired();

            e.HasOne(x => x.Cuenta)
                .WithMany(x => x.Movimientos)
                .HasForeignKey(x => x.IdCuentaGasto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Pago)
                .WithOne(x => x.MovimientoCuenta)
                .HasForeignKey<MovimientoCuentaGasto>(x => x.IdPagoGasto)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.IdPagoGasto)
                .IsUnique();

            e.HasIndex(x => new { x.IdCuentaGasto, x.FechaMovimiento })
                .HasDatabaseName("IX_MovimientosCuentaGasto_Cuenta_Fecha")
                .IsDescending(false, true);
        });

        // Comprobantes de gastos
        b.Entity<GastoComprobante>(e =>
        {
            e.ToTable("gastoscomprobantes");
            e.HasKey(x => x.IdComprobante);

            e.Property(x => x.IdComprobante)
                .HasColumnName("idcomprobante")
                .ValueGeneratedOnAdd();

            e.Property(x => x.IdGasto)
                .HasColumnName("idgasto")
                .IsRequired();

            e.Property(x => x.TipoComprobante)
                .HasColumnName("tipocomprobante")
                .HasMaxLength(30)
                .HasDefaultValue("TICKET")
                .IsRequired();

            e.Property(x => x.NombreArchivo)
                .HasColumnName("nombrearchivo")
                .HasMaxLength(250);

            e.Property(x => x.MimeType)
                .HasColumnName("mimetype")
                .HasMaxLength(100);

            e.Property(x => x.ArchivoBytes)
                .HasColumnName("archivobytes")
                .HasColumnType("bytea");

            e.Property(x => x.ArchivoUrl)
                .HasColumnName("archivourl")
                .HasMaxLength(1000);

            e.Property(x => x.Observaciones)
                .HasColumnName("observaciones")
                .HasMaxLength(250);

            e.Property(x => x.IdUsuarioRegistro)
                .HasColumnName("idusuarioregistro")
                .IsRequired();

            e.Property(x => x.FechaRegistro)
                .HasColumnName("fecharegistro")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.HasOne(x => x.Gasto)
                .WithMany(x => x.Comprobantes)
                .HasForeignKey(x => x.IdGasto)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.IdGasto)
                .HasDatabaseName("IX_GastosComprobantes_Gasto");
        });

        b.Entity<CatalogoComision>(e =>
        {
            e.ToTable("catalogocomisiones");
            e.HasKey(x => x.IdCatalogoComision);
            e.Property(x => x.IdCatalogoComision).HasColumnName("idcatalogocomision").ValueGeneratedOnAdd();
            e.Property(x => x.NombreEsquema).HasColumnName("nombreesquema").IsRequired();
            e.Property(x => x.TipoCalculo).HasColumnName("tipocalculo").IsRequired();

            e.Property(x => x.ComisionContado)
                .HasColumnName("comisioncontado")
                .HasColumnType("numeric(14,2)")
                .HasDefaultValue(0.00m);

            e.Property(x => x.ComisionCredito)
                .HasColumnName("comisioncredito")
                .HasColumnType("numeric(14,2)")
                .HasDefaultValue(0.00m);

            e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
            e.Property(x => x.FechaRegistro).HasColumnName("fecharegistro").IsRequired();
        });

        b.Entity<ConfiguracionComision>(e =>
        {
            e.ToTable("configuracioncomisiones");
            e.HasKey(x => x.IdConfiguracion);
            e.Property(x => x.IdConfiguracion).HasColumnName("idconfiguracion").ValueGeneratedOnAdd();
            e.Property(x => x.IdProducto).HasColumnName("idproducto").IsRequired();
            e.Property(x => x.IdCatalogoComision).HasColumnName("idcatalogocomision").IsRequired();
            e.Property(x => x.FechaAsignacion).HasColumnName("fechaasignacion").IsRequired();
            e.Property(x => x.IdUsuarioRegistro).HasColumnName("idusuarioregistro");

            // --- RELACIONES (LLAVES FORÁNEAS) ---
            e.HasOne(x => x.Producto)
                .WithOne() // Asumiendo relación 1 a 1 como en tu BD UNIQUE (IdProducto)
                .HasForeignKey<ConfiguracionComision>(x => x.IdProducto)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogoComision)
                .WithMany(c => c.ConfiguracionesComisiones)
                .HasForeignKey(x => x.IdCatalogoComision)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UsuarioRegistro)
                .WithMany()
                .HasForeignKey(x => x.IdUsuarioRegistro)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ComisionPorPagar>(e =>
        {
            e.ToTable("comisionesporpagar");
            e.HasKey(x => x.IdComision);
            e.Property(x => x.IdComision).HasColumnName("idcomision").ValueGeneratedOnAdd();
            e.Property(x => x.IdVenta).HasColumnName("idventa").IsRequired();
            e.Property(x => x.IdDetalle).HasColumnName("iddetalle").IsRequired();
            e.Property(x => x.IdVendedor).HasColumnName("idvendedor").IsRequired();

            e.Property(x => x.MontoComision)
                .HasColumnName("montocomision")
                .HasColumnType("numeric(14,2)")
                .IsRequired();

            e.Property(x => x.EstatusComision).HasColumnName("estatuscomision").HasDefaultValue("PENDIENTE")
                .IsRequired();
            e.Property(x => x.FechaCalculo).HasColumnName("fechacalculo").IsRequired();
            e.Property(x => x.FechaPago).HasColumnName("fechapago");
            e.Property(x => x.IdUsuarioPago).HasColumnName("idusuariopago");

            // --- RELACIONES (LLAVES FORÁNEAS) ---
            e.HasOne(x => x.Venta)
                .WithMany()
                .HasForeignKey(x => x.IdVenta)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.DetalleVenta)
                .WithMany()
                .HasForeignKey(x => x.IdDetalle)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Vendedor)
                .WithMany()
                .HasForeignKey(x => x.IdVendedor)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UsuarioPago)
                .WithMany()
                .HasForeignKey(x => x.IdUsuarioPago)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}