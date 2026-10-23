namespace SistemaCobranzaApp.Shared.Dtos;

/// <summary>
/// Catálogo central de códigos de respuesta de la API.
/// Mantén aquí todos los códigos para conservar la consistencia entre módulos.
/// </summary>
public static class CodigosApi
{
    public const string Exito = "200";
    public const string Creado = "201";
    public const string SolicitudInvalida = "400";
    public const string NoAutorizado = "401";
    public const string Prohibido = "403";
    public const string NoEncontrado = "404";
    public const string Conflicto = "409";
    public const string ErrorInterno = "500";
}
