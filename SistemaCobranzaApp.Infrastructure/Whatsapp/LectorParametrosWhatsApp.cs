using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SistemaCobranzaApp.Infrastructure.Persistencia;

namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Lee de parametrossistema la configuración de WhatsApp con una caché corta en memoria,
/// para no golpear la base en cada venta/pago pero reflejar los cambios de la pantalla de
/// Parámetros en pocos segundos. Se registra como SINGLETON (la caché es compartida).
///
/// Como el servicio es singleton y CobranzaDbContext es scoped, abre un scope propio para
/// consultar la base.
/// </summary>
public sealed class LectorParametrosWhatsApp
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _candado = new(1, 1);

    private ConfiguracionWhatsApp? _cache;
    private DateTimeOffset _expira = DateTimeOffset.MinValue;

    public LectorParametrosWhatsApp(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<ConfiguracionWhatsApp> ObtenerAsync(CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        if (_cache is not null && ahora < _expira)
            return _cache;

        await _candado.WaitAsync(ct);
        try
        {
            // Otro hilo pudo refrescar mientras esperábamos el candado.
            ahora = DateTimeOffset.UtcNow;
            if (_cache is not null && ahora < _expira)
                return _cache;

            var config = await LeerDeBaseAsync(ct);
            _cache = config;
            _expira = DateTimeOffset.UtcNow.Add(Vigencia);
            return config;
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>Invalida la caché para forzar una relectura en la próxima consulta.</summary>
    public void Invalidar() => _expira = DateTimeOffset.MinValue;

    private async Task<ConfiguracionWhatsApp> LeerDeBaseAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CobranzaDbContext>();

        var claves = new[]
        {
            ClavesParametrosWhatsApp.EnvioActivo,
            ClavesParametrosWhatsApp.NotificarVenta,
            ClavesParametrosWhatsApp.NotificarPago,
            ClavesParametrosWhatsApp.PlantillaVenta,
            ClavesParametrosWhatsApp.PlantillaPago,
            ClavesParametrosWhatsApp.PurgaActiva,
            ClavesParametrosWhatsApp.PurgaDias
        };

        var filas = await db.ParametrosSistema.AsNoTracking()
            .Where(p => claves.Contains(p.Clave))
            .Select(p => new { p.Clave, p.ValorTexto, p.ValorNumerico })
            .ToListAsync(ct);

        string? Texto(string clave) =>
            filas.FirstOrDefault(f => f.Clave == clave)?.ValorTexto;

        decimal? Numero(string clave) =>
            filas.FirstOrDefault(f => f.Clave == clave)?.ValorNumerico;

        // Interruptores: ausente o distinto de 'SI' => activo por defecto en los de envío
        // salvo que explícitamente digan 'NO'. Para ser predecibles: default true si la
        // clave no existe, y false solo cuando el valor es 'NO'.
        static bool EsSi(string? v, bool porDefecto)
        {
            if (string.IsNullOrWhiteSpace(v)) return porDefecto;
            return v.Trim().Equals("SI", StringComparison.OrdinalIgnoreCase);
        }

        var plantillaVenta = Texto(ClavesParametrosWhatsApp.PlantillaVenta);
        if (string.IsNullOrWhiteSpace(plantillaVenta))
            plantillaVenta = PlantillasPorDefecto.Venta;

        var plantillaPago = Texto(ClavesParametrosWhatsApp.PlantillaPago);
        if (string.IsNullOrWhiteSpace(plantillaPago))
            plantillaPago = PlantillasPorDefecto.Pago;

        var purgaDias = (int)(Numero(ClavesParametrosWhatsApp.PurgaDias) ?? 7m);
        if (purgaDias < 1) purgaDias = 1;

        return new ConfiguracionWhatsApp(
            EnvioActivo: EsSi(Texto(ClavesParametrosWhatsApp.EnvioActivo), porDefecto: true),
            NotificarVenta: EsSi(Texto(ClavesParametrosWhatsApp.NotificarVenta), porDefecto: true),
            NotificarPago: EsSi(Texto(ClavesParametrosWhatsApp.NotificarPago), porDefecto: true),
            PlantillaVenta: plantillaVenta!,
            PlantillaPago: plantillaPago!,
            PurgaActiva: EsSi(Texto(ClavesParametrosWhatsApp.PurgaActiva), porDefecto: true),
            PurgaDias: purgaDias);
    }
}

/// <summary>Textos por defecto cuando la plantilla de la tabla está vacía.</summary>
public static class PlantillasPorDefecto
{
    public const string Venta =
        "*{empresa}*\n*COMPROBANTE DE VENTA*\n\nEstimado(a) *{cliente}*, confirmamos el registro de su compra.\n\n*Código de venta:* {folio}\n*Fecha/Hora:* {fecha}\n\nAgradecemos su preferencia.\n\n_Mensaje automático. Si desea dejar de recibir notificaciones, responda con la palabra BAJA._";

    public const string Pago =
        "*{empresa}*\n*COMPROBANTE DE PAGO*\n\nEstimado(a) *{cliente}*, confirmamos el registro de su pago.\n\n*Folio:* {folio}\n*Fecha/Hora:* {fecha}\n*Saldo anterior:* {saldo_anterior}\n*Monto abonado:* {monto}\n*Saldo actual:* {saldo}\n\nAgradecemos su compromiso de mantener su cuenta al corriente.\n\n_Mensaje automático. Si desea dejar de recibir notificaciones, responda con la palabra BAJA._";
}
