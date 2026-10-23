namespace SistemaCobranzaApp.Domain.Entidades;

public class AsignacionJornada
{
    public long IdUsuario { get; set; }
    public long IdRuta { get; set; }
    public long? IdVendedor { get; set; }
    public long? IdCobrador { get; set; }
}
