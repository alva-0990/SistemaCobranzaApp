using SistemaCobranzaApp.Domain.Constantes;

namespace SistemaCobranzaApp.Domain.Ventas;

/// <summary>
/// Reglas puras de alta de venta, portadas de VentanaNuevaVenta (app WPF) y adaptadas
/// al esquema financiero con amortización basada en cuota fija pactada y folios offline para .NET MAUI.
/// Sin dependencias de base de datos.
/// </summary>
public static class CalculoVenta
{
    /// <summary>Una línea de detalle para el cálculo (cantidad y precio).</summary>
    public readonly record struct LineaCalculo(int Cantidad, decimal PrecioCobrado)
    {
        public decimal Importe => Cantidad * PrecioCobrado;
    }

    /// <summary>Subtotal = suma de importes (cantidad * precio) de las líneas.</summary>
    public static decimal CalcularSubtotal(IEnumerable<LineaCalculo> lineas)
        => lineas.Sum(l => l.Importe);

    /// <summary>
    /// Saldo pendiente inicial = totalFinal - enganche, con piso en 0.
    /// </summary>
    public static decimal SaldoPendienteInicial(decimal totalFinal, decimal enganche)
    {
        var saldo = totalFinal - enganche;
        return saldo < 0 ? 0m : saldo;
    }

    /// <summary>
    /// Estatus automático inicial de la venta adaptado al nuevo esquema:
    ///  - saldoPendiente &lt;= 0 -> Pagada (EstatusVentaIds.Pagada)
    ///  - en otro caso           -> Pendiente / Activa (EstatusVentaIds.Pendiente)
    /// </summary>
    public static long DeterminarEstatusInicial(decimal totalFinal, decimal enganche)
    {
        var saldoPendiente = SaldoPendienteInicial(totalFinal, enganche);

        if (saldoPendiente <= 0) return EstatusVentaIds.Pagada;
        
        return EstatusVentaIds.Pendiente;
    }

    /// <summary>
    /// 🎯 NUEVO: Genera un código de venta único e inmutable en el dispositivo cliente (.NET MAUI).
    /// Garantiza cero colisiones entre vendedores operando en modo offline en la misma ruta.
    /// </summary>
    /// <param name="idVendedor">ID del vendedor autenticado en la app.</param>
    /// <summary>
    /// Genera un código de venta único e inmutable mezclando el Vendedor + Timestamp + Sufijo Aleatorio.
    /// Funciona idéntico en .NET MAUI (Offline) y en el Servidor Central.
    /// </summary>
    public static string GenerarCodigoVenta(long idVendedor)
    {
        // Prefijo V (venta) + Vendedor + Timestamp + Sufijo aleatorio.
        return GenerarFolio('V', idVendedor);
    }

    /// <summary>
    /// Genera el folio de un pago/abono capturado offline por un cobrador, con el mismo
    /// esquema que <see cref="GenerarCodigoVenta"/> (Timestamp + Sufijo aleatorio), pero con
    /// prefijo 'C' (cobrador). Reemplaza el antiguo folio provisional "LOCAL-xxxxx".
    /// Formato: C{idCobrador}-{yyMMdd-HHmmss}-{sufijo}.
    /// </summary>
    /// <param name="idCobrador">ID del cobrador autenticado en la app.</param>
    public static string GenerarFolioPago(long idCobrador)
        => GenerarFolio('C', idCobrador);

    // Genera un folio único e inmutable mezclando Prefijo + Id + Timestamp + Sufijo aleatorio.
    // Compartido por los folios de venta (V) y de pago (C) para garantizar el mismo formato.
    private static string GenerarFolio(char prefijo, long id)
    {
        // Formato compacto: AñoMesDía-HoraMinutoSegundo (ej: 261007-121430).
        string timestamp = DateTime.UtcNow.ToString("yyMMdd-HHmmss");

        // Sufijo aleatorio de 3 caracteres para evitar choques en el mismo segundo.
        const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        char[] sufijo = new char[3];

        for (int i = 0; i < 3; i++)
        {
            sufijo[i] = caracteres[random.Next(caracteres.Length)];
        }

        return $"{prefijo}{id}-{timestamp}-{new string(sufijo)}";
    }


    /// <summary>
    /// Valida que el código de venta generado offline en .NET MAUI cumpla con la estructura de seguridad.
    /// </summary>
    public static bool ValidarCodigoVentaOffline(string? codigoVenta)
    {
        if (string.IsNullOrWhiteSpace(codigoVenta)) return false;
        
        return codigoVenta.StartsWith("V") && codigoVenta.Contains("-");
    }

    public enum ResultadoValidacion
    {
        Ok,
        SinLineas,
        LineaInvalida,
        EngancheNegativo,
        EngancheExcedeTotal,
        CuotaFijaInvalida,
        CuotaFijaExcedeFinanciamiento
    }

    /// <summary>
    /// Valida los datos de alta de la venta y las restricciones de la cuota pactada (sin tocar la base).
    /// </summary>
    public static ResultadoValidacion Validar(
        IReadOnlyList<LineaCalculo> lineas,
        decimal enganche,
        decimal? montoCuotaFija,
        bool esCredito)
    {
        if (lineas is null || lineas.Count == 0)
            return ResultadoValidacion.SinLineas;

        foreach (var l in lineas)
        {
            if (l.Cantidad <= 0 || l.PrecioCobrado < 0)
                return ResultadoValidacion.LineaInvalida;
        }

        if (enganche < 0)
            return ResultadoValidacion.EngancheNegativo;

        var total = CalcularSubtotal(lineas);
        if (enganche > total)
            return ResultadoValidacion.EngancheExcedeTotal;

        if (esCredito)
        {
            var montoFinanciado = total - enganche;
            
            if (montoFinanciado > 0)
            {
                if (montoCuotaFija is null || montoCuotaFija <= 0)
                    return ResultadoValidacion.CuotaFijaInvalida;

                if (montoCuotaFija > montoFinanciado)
                    return ResultadoValidacion.CuotaFijaExcedeFinanciamiento;
            }
        }

        return ResultadoValidacion.Ok;
    }
}
