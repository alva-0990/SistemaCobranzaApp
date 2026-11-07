using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SistemaCobranzaApp.Domain.Entidades;
using SistemaCobranzaApp.Infrastructure.Persistencia;
using SistemaCobranzaApp.Infrastructure.Reportes;
using SistemaCobranzaApp.Shared.Dtos;

namespace SistemaCobranzaApp.Infrastructure.Whatsapp;

/// <summary>
/// Recordatorios de cobranza por WhatsApp (envío MANUAL confirmado por el usuario).
///
/// Flujo:
///   1. <see cref="ListarCandidatosAsync"/> toma los clientes en atraso (reutiliza el reporte
///      "Clientes sin pago reciente"), determina la ETAPA según los días de atraso y arma el
///      mensaje con la plantilla parametrizada (tabla parametrossistema). El usuario revisa.
///   2. <see cref="EnviarAsync"/> encola en la cola de WhatsApp SOLO los clientes que el usuario
///      seleccionó. El worker existente los drena y envía. Nada se envía sin confirmación.
///
/// Las plantillas y umbrales de etapa viven en parámetros (editables en caliente):
///   RECORDATORIO_ETAPA1_DIAS_HASTA, RECORDATORIO_ETAPA2_DIAS_HASTA,
///   RECORDATORIO_MSG_ETAPA1/2/3, WHATSAPP_NOTIFICAR_RECORDATORIO.
/// </summary>
public sealed class ServicioRecordatoriosWhatsApp
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");
    private const string NegocioPorDefecto = "Sistema de Cobranza";

    // Valores por defecto si el parámetro no existe.
    private const int Etapa1PorDefecto = 7;
    private const int Etapa2PorDefecto = 15;

    private readonly CobranzaDbContext _db;
    private readonly ServicioReportes _reportes;
    private readonly SenalColaWhatsApp _senal;

    public ServicioRecordatoriosWhatsApp(CobranzaDbContext db, ServicioReportes reportes, SenalColaWhatsApp senal)
    {
        _db = db;
        _reportes = reportes;
        _senal = senal;
    }

    public async Task<RecordatorioCandidatosResponse> ListarCandidatosAsync(
        RecordatorioCobranzaFiltro f, CancellationToken ct = default)
    {
        var dias = Math.Max(1, f.DiasAtrasoMinimo);
        // Reutiliza el reporte de clientes sin pago reciente (agrupado por cliente, con saldo y días).
        var sinPago = await _reportes.ClientesSinPagoAsync(
            new ReporteSinPagoFiltro(f.IdZona, f.IdRuta, null, dias), ct);

        var cfg = await LeerConfiguracionAsync(ct);
        var empresa = await ObtenerNombreNegocioAsync(ct);

        var candidatos = sinPago.Filas.Select(c =>
        {
            var (etapa, nombreEtapa, plantilla) = ResolverEtapa(c.DiasSinPago, cfg);
            var telefono = ServicioColaWhatsApp.NormalizarTelefono(c.TelefonoPrincipal);
            var mensaje = ArmarMensaje(plantilla, c.Cliente, empresa, c.SaldoPendiente, c.DiasSinPago);
            return new RecordatorioCandidatoDto(
                c.IdCliente, c.Cliente, c.TelefonoPrincipal, c.Zona, c.Ruta,
                c.SaldoPendiente, c.DiasSinPago, etapa, nombreEtapa, mensaje,
                Enviable: telefono is not null);
        })
        .OrderByDescending(x => x.DiasAtraso)
        .ThenByDescending(x => x.SaldoPendiente)
        .ToList();

        return new RecordatorioCandidatosResponse(
            candidatos, candidatos.Count, candidatos.Sum(x => x.SaldoPendiente));
    }

    public async Task<EnviarRecordatoriosResponse> EnviarAsync(
        EnviarRecordatoriosRequest req, RecordatorioCobranzaFiltro filtro, CancellationToken ct = default)
    {
        var cfg = await LeerConfiguracionAsync(ct);
        if (!cfg.EnvioActivo || !cfg.NotificarRecordatorio)
            return new EnviarRecordatoriosResponse(0, req.IdClientes.Count,
                "El envío de recordatorios por WhatsApp está desactivado en Parámetros.");

        if (req.IdClientes.Count == 0)
            return new EnviarRecordatoriosResponse(0, 0, "No se seleccionó ningún cliente.");

        // Se recalculan los candidatos del filtro y se cruzan con la selección, para no confiar
        // en datos del cliente (saldo/días/mensaje) y reflejar el estado actual al momento de enviar.
        var candidatos = (await ListarCandidatosAsync(filtro, ct)).Candidatos
            .Where(c => req.IdClientes.Contains(c.IdCliente))
            .ToList();

        var seleccionados = new HashSet<long>(req.IdClientes);
        int encolados = 0, omitidos = 0;

        foreach (var c in candidatos)
        {
            var telefono = ServicioColaWhatsApp.NormalizarTelefono(c.Telefono);
            if (telefono is null) { omitidos++; continue; }

            _db.ColaMensajesWhatsApp.Add(new ColaMensajeWhatsApp
            {
                Telefono = telefono,
                Mensaje = c.Mensaje,
                Plantilla = PlantillaWhatsApp.Recordatorio,
                Estado = EstadoMensajeWhatsApp.Pendiente,
                TipoOrigen = TipoOrigenWhatsApp.Recordatorio,
                FechaCreacion = DateTimeOffset.UtcNow
            });
            encolados++;
        }

        // Clientes seleccionados que ya no aparecen como candidatos (p. ej. pagaron) también se omiten.
        omitidos += seleccionados.Count - candidatos.Count;

        if (encolados > 0)
        {
            await _db.SaveChangesAsync(ct);
            _senal.Avisar();
        }

        return new EnviarRecordatoriosResponse(encolados, Math.Max(0, omitidos),
            encolados == 0 ? "No se encoló ningún recordatorio (sin teléfono utilizable o sin candidatos vigentes)." : null);
    }

    // ------------------------- Interno -------------------------

    private sealed record ConfigRecordatorio(
        bool EnvioActivo, bool NotificarRecordatorio,
        int Etapa1Hasta, int Etapa2Hasta,
        string MsgEtapa1, string MsgEtapa2, string MsgEtapa3);

    private async Task<ConfigRecordatorio> LeerConfiguracionAsync(CancellationToken ct)
    {
        var claves = new[]
        {
            ClavesParametrosWhatsApp.EnvioActivo,
            ClavesParametrosWhatsApp.NotificarRecordatorio,
            ClavesParametrosWhatsApp.RecordatorioEtapa1DiasHasta,
            ClavesParametrosWhatsApp.RecordatorioEtapa2DiasHasta,
            ClavesParametrosWhatsApp.RecordatorioMsgEtapa1,
            ClavesParametrosWhatsApp.RecordatorioMsgEtapa2,
            ClavesParametrosWhatsApp.RecordatorioMsgEtapa3
        };

        var filas = await _db.ParametrosSistema.AsNoTracking()
            .Where(p => claves.Contains(p.Clave))
            .Select(p => new { p.Clave, p.ValorTexto, p.ValorNumerico })
            .ToListAsync(ct);

        string? Texto(string clave) => filas.FirstOrDefault(f => f.Clave == clave)?.ValorTexto;
        decimal? Numero(string clave) => filas.FirstOrDefault(f => f.Clave == clave)?.ValorNumerico;
        static bool EsSi(string? v, bool d) => string.IsNullOrWhiteSpace(v) ? d : v.Trim().Equals("SI", StringComparison.OrdinalIgnoreCase);

        var e1 = (int)(Numero(ClavesParametrosWhatsApp.RecordatorioEtapa1DiasHasta) ?? Etapa1PorDefecto);
        var e2 = (int)(Numero(ClavesParametrosWhatsApp.RecordatorioEtapa2DiasHasta) ?? Etapa2PorDefecto);
        if (e1 < 1) e1 = Etapa1PorDefecto;
        if (e2 <= e1) e2 = e1 + 1;

        return new ConfigRecordatorio(
            EnvioActivo: EsSi(Texto(ClavesParametrosWhatsApp.EnvioActivo), true),
            NotificarRecordatorio: EsSi(Texto(ClavesParametrosWhatsApp.NotificarRecordatorio), true),
            Etapa1Hasta: e1,
            Etapa2Hasta: e2,
            MsgEtapa1: Texto(ClavesParametrosWhatsApp.RecordatorioMsgEtapa1) ?? MensajePorDefecto(1),
            MsgEtapa2: Texto(ClavesParametrosWhatsApp.RecordatorioMsgEtapa2) ?? MensajePorDefecto(2),
            MsgEtapa3: Texto(ClavesParametrosWhatsApp.RecordatorioMsgEtapa3) ?? MensajePorDefecto(3));
    }

    private static (int etapa, string nombre, string plantilla) ResolverEtapa(int dias, ConfigRecordatorio cfg)
    {
        if (dias <= cfg.Etapa1Hasta) return (1, "Preventiva", cfg.MsgEtapa1);
        if (dias <= cfg.Etapa2Hasta) return (2, "Seguimiento", cfg.MsgEtapa2);
        return (3, "Urgente", cfg.MsgEtapa3);
    }

    private static string ArmarMensaje(string plantilla, string cliente, string empresa, decimal saldo, int dias)
        => plantilla
            .Replace("{cliente}", cliente)
            .Replace("{empresa}", empresa)
            .Replace("{saldo}", saldo.ToString("C2", Mx))
            // Acepta {días} y {dias} por comodidad.
            .Replace("{días}", dias.ToString(CultureInfo.InvariantCulture))
            .Replace("{dias}", dias.ToString(CultureInfo.InvariantCulture));

    private async Task<string> ObtenerNombreNegocioAsync(CancellationToken ct)
    {
        var e = await _db.Empresas.AsNoTracking()
            .Select(x => new { x.NombreCorto, x.NombreComercial })
            .FirstOrDefaultAsync(ct);
        var nombre = !string.IsNullOrWhiteSpace(e?.NombreCorto) ? e!.NombreCorto : e?.NombreComercial;
        return string.IsNullOrWhiteSpace(nombre) ? NegocioPorDefecto : nombre.Trim();
    }

    private static string MensajePorDefecto(int etapa) => etapa switch
    {
        1 => "Hola *{cliente}*, le saludamos de *{empresa}*. Le recordamos su saldo pendiente de *{saldo}* ({días} días).",
        2 => "Hola *{cliente}*, su cuenta en *{empresa}* presenta un saldo de *{saldo}* con {días} días sin abonos.",
        _ => "Estimado(a) *{cliente}*, su cuenta en *{empresa}* registra {días} días de atraso con saldo vencido de *{saldo}*."
    };
}
