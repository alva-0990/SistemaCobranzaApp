namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Filtros para consultar la cartera. Todos opcionales; se combinan (AND).
/// </summary>
public record CarteraFiltro(
    long? IdCobrador = null,
    long? IdZona = null,
    long? IdRuta = null,
    bool SoloConSaldo = true);

/// <summary>
/// Una venta con saldo, tal como la ve el cobrador en su cartera.
/// </summary>
public record CarteraCuentaDto(
    long IdVenta,
    long IdCliente,
    string Cliente,
    string? Ruta,
    long? IdZona,
    string? CodigoVenta,
    decimal TotalFinal,
    decimal SaldoPendiente,
    decimal? PagoRequerido,
    long IdEstatusVenta,
    string? Estatus,
    DateTimeOffset? FechaUltimoPago);

public record CarteraResponse(
    IReadOnlyList<CarteraCuentaDto> Cuentas,
    int Total,
    decimal SaldoTotal);
