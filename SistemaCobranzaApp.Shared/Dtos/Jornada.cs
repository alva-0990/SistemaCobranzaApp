namespace SistemaCobranzaApp.Shared.Dtos;

public record AsignacionJornadaRequest(long IdUsuario, long IdRuta, long? IdVendedor, long? IdCobrador);
public record RutaJornadaDto(long IdUsuario, long IdRuta, string Ruta, long IdZona, long? IdVendedor, long? IdCobrador);
public record JornadaDescargaDto(
    long IdUsuario, DateTimeOffset DescargadaEn, RutaJornadaDto Asignacion,
    List<ClienteDto> Clientes, List<VentaDto> Ventas, List<PagoSyncDto> Pagos,
    List<ProductoDto> Productos, List<ExistenciaZonaDto> Inventario,
    List<CondicionCreditoDto> Condiciones, List<CatalogoFrecuenciaPagoDto> Frecuencias,
    List<MesDto> Meses)
{
    public List<ClasificaionClienteDto> Clasificaciones { get; init; } = new();
    public bool EsZonaCompleta { get; init; }
    public List<RutaJornadaDto> Rutas { get; init; } = new();
}
public record ClienteJornadaRequest(Guid ClienteUuid, DateTimeOffset FechaCaptura, CrearClienteRequest Cliente);
public record VentaJornadaRequest(Guid VentaUuid, DateTimeOffset FechaCaptura, CrearVentaRequest Venta)
{
    public long? IdVentaVieja { get; init; }
    public Guid? VentaViejaUuid { get; init; }
    public decimal? SaldoArrastre { get; init; }
}
public record PagoJornadaRequest(long IdRuta, RegistrarPagoRequest Pago, Guid? VentaLocalUuid = null);
public record ResultadoOperacionJornada(bool Aplicado, bool Duplicado, string? Error,
    VentaDto? Venta = null, RegistrarPagoResponse? Pago = null, ClienteDto? Cliente = null);

public class OperacionJornadaLocal
{
    public long? IdVentaVieja { get; set; }
    public Guid? VentaViejaUuid { get; set; }
    public decimal SaldoArrastre { get; set; }
    public long IdLocal { get; set; }
    public CrearClienteRequest? Cliente { get; set; }
    public ClienteDto? ClienteCentral { get; set; }
    public ClienteDto? ClienteLocal { get; set; }
    public TicketVentaDto? TicketVenta { get; set; }
    public TicketPagoDto? TicketPago { get; set; }
    public Guid Uuid { get; set; } = Guid.NewGuid();
    public DateTimeOffset FechaCaptura { get; set; } = DateTimeOffset.UtcNow;
    public CrearVentaRequest? Venta { get; set; }
    public RegistrarPagoRequest? Pago { get; set; }
    public Guid? VentaLocalUuid { get; set; }
    public bool Aplicada { get; set; }
    public string? Error { get; set; }
    public VentaDto? VentaCentral { get; set; }
    public RegistrarPagoResponse? PagoCentral { get; set; }
}

public class JornadaLocal
{
    public JornadaDescargaDto? Descarga { get; set; }
    public List<OperacionJornadaLocal> Operaciones { get; set; } = new();
}
