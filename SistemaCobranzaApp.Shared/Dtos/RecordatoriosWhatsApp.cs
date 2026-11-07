namespace SistemaCobranzaApp.Shared.Dtos;

// ---- Recordatorios de cobranza por WhatsApp (envío manual confirmado por el usuario) ----

/// <summary>Filtro para listar candidatos a recordatorio (clientes en atraso con saldo).</summary>
public record RecordatorioCobranzaFiltro(
    long? IdZona = null,
    long? IdRuta = null,
    int DiasAtrasoMinimo = 1);

/// <summary>
/// Candidato a recibir un recordatorio: un cliente en atraso (datos agregados a nivel cliente).
/// Incluye el mensaje ya armado según la etapa, para que el usuario lo revise antes de enviar.
/// </summary>
public record RecordatorioCandidatoDto(
    long IdCliente,
    string Cliente,
    string? Telefono,
    string? Zona,
    string? Ruta,
    decimal SaldoPendiente,
    int DiasAtraso,
    int Etapa,            // 1, 2 o 3
    string EtapaNombre,   // "Preventiva" | "Seguimiento" | "Urgente"
    string Mensaje,       // texto ya armado (preview y lo que se enviaría)
    bool Enviable);       // false si no tiene teléfono utilizable o está dado de baja de WhatsApp

public record RecordatorioCandidatosResponse(
    IReadOnlyList<RecordatorioCandidatoDto> Candidatos,
    int Total,
    decimal SaldoTotal);

/// <summary>Solicitud de envío: lista de clientes seleccionados por el usuario.</summary>
public record EnviarRecordatoriosRequest(IReadOnlyList<long> IdClientes);

public record EnviarRecordatoriosResponse(
    int Encolados,
    int Omitidos,
    string? Detalle);
