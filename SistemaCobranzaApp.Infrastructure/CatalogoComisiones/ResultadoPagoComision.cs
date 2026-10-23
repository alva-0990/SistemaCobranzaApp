
namespace SistemaCobranzaApp.Infrastructure.CatalogoComisiones;

public class ResultadoPagoComision
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public int RegistrosProcesados { get; init; }

    public static ResultadoPagoComision Fallo(string error) => new() { Exito = false, Error = error };
    public static ResultadoPagoComision Ok(int cantidad) => new() { Exito = true, RegistrosProcesados = cantidad };
}
