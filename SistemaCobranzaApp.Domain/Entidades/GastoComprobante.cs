namespace SistemaCobranzaApp.Domain.Entidades;

public class GastoComprobante
{
    public long IdComprobante { get; set; }

    public long IdGasto { get; set; }

    // TICKET, FACTURA, RECIBO, FOTOGRAFIA, OTRO
    public string TipoComprobante { get; set; } = "TICKET";

    public string? NombreArchivo { get; set; }
    public string? MimeType { get; set; }

    // Archivo almacenado directamente en PostgreSQL
    public byte[]? ArchivoBytes { get; set; }

    // Alternativa: ruta o URL de almacenamiento externo
    public string? ArchivoUrl { get; set; }

    public string? Observaciones { get; set; }

    public long IdUsuarioRegistro { get; set; }

    public DateTimeOffset FechaRegistro { get; set; }

    // Navegación
    public Gasto? Gasto { get; set; }
}