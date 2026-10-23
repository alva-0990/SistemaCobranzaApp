namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Resultado del diálogo de venta. Transporta el request capturado y marca
/// si corresponde a una edición (de una venta existente) o a un alta nueva.
/// </summary>
public sealed record ResultadoDialogoVenta(
    CrearVentaRequest Request,
    bool EsEdicion,
    long IdVenta
);
