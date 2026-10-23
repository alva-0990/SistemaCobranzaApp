namespace SistemaCobranzaApp.Infrastructure.Gastos;

public class ResultadoOperacion<T>
{
    public bool Exito { get; init; }
    public string? Error { get; init; }
    public T? Datos { get; init; }
    public bool NoEncontrado { get; init; }

    public static ResultadoOperacion<T> Fallo(string error) =>
        new() { Exito = false, Error = error };

    public static ResultadoOperacion<T> NoExiste(string error) =>
        new() { Exito = false, Error = error, NoEncontrado = true };

    public static ResultadoOperacion<T> Ok(T datos) =>
        new() { Exito = true, Datos = datos };
}