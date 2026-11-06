using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaCobranzaApp.Domain.Comun;
using SistemaCobranzaApp.Shared.Dtos;
using SistemaCobranzaApp.Shared.Theme;

namespace SistemaCobranzaApp.Infrastructure.Cobranza;

/// <summary>
/// Genera el PDF del estado de cuenta integral a partir de <see cref="EstadoCuentaIntegralDto"/>.
/// Los colores salen de la paleta del sistema (AppTheme): si cambia el color institucional, el PDF lo adopta.
/// </summary>
public static class GeneradorPdfEstadoCuenta
{
    private static readonly CultureInfo Mx = new("es-MX");

    // Paleta del documento derivada del tema del sistema.
    private static readonly string Primario = Hex(AppTheme.Theme.PaletteLight.Primary.R, AppTheme.Theme.PaletteLight.Primary.G, AppTheme.Theme.PaletteLight.Primary.B);
    // Acento = el primario oscurecido un 20 % (para títulos y bordes).
    private static readonly string Acento = Hex((byte)(AppTheme.Theme.PaletteLight.Primary.R * 0.8), (byte)(AppTheme.Theme.PaletteLight.Primary.G * 0.8), (byte)(AppTheme.Theme.PaletteLight.Primary.B * 0.8));
    private const string Gris = "#757575";
    private const string FondoSuave = "#ECEFF1";
    private const string Rojo = "#C62828";
    private const string Verde = "#2E7D32";
    private const string Ambar = "#F9A825";

    private static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
    private static string Dinero(decimal v) => v.ToString("C2", Mx);
    private static string Fecha(DateTimeOffset? d) => d is { } v ? v.EnZonaProyecto().DateTime.ToString("dd/MM/yyyy") : "—";

    public static byte[] Generar(EstadoCuentaIntegralDto e)
    {
        // Licencia Community de QuestPDF (gratuita para organizaciones con ingresos anuales menores a 1 M USD).
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor("#333333"));

            page.Header().PaddingBottom(6).BorderBottom(0.5f).BorderColor(FondoSuave)
                .Text("ESTADO DE CUENTA INTEGRAL DEL CLIENTE").FontSize(8).FontColor(Primario);

            page.Content().Column(col =>
            {
                col.Spacing(8);
                Portada(col, e);
                DatosCliente(col, e);
                Dashboard(col, e);
                ResumenCreditos(col, e);
                foreach (var d in e.Activos) DetalleActivo(col, d);
                Cerrados(col, "Historial de Créditos Liquidados", "Fecha Liquidación", e.Liquidados,
                    "No se registran créditos liquidados en el histórico del cliente.");
                Cerrados(col, "Créditos Cancelados", "Fecha Cancelación", e.Cancelados,
                    "No se registran créditos cancelados en el histórico del cliente.");
                Cierre(col, e);
            });

            page.Footer().Row(r =>
            {
                r.RelativeItem().Text($"Documento generado por {e.Empresa.TrimEnd('.')}.").FontSize(8).FontColor(Gris);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Gris));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Titulo(ColumnDescriptor col, string texto)
        => col.Item().PaddingTop(8).BorderLeft(3).BorderColor(Primario).PaddingLeft(6)
            .Text(texto).FontSize(12).SemiBold().FontColor(Acento);

    private static IContainer Celda(IContainer c, bool cabecera = false, string? fondo = null)
        => cabecera
            ? c.Background(Primario).PaddingVertical(4).PaddingHorizontal(5)
            : c.Background(fondo ?? "#FFFFFF").BorderBottom(0.5f).BorderColor(FondoSuave).PaddingVertical(4).PaddingHorizontal(5);

    private static void Portada(ColumnDescriptor col, EstadoCuentaIntegralDto e)
    {
        col.Item().AlignCenter().Column(c =>
        {
            c.Item().AlignCenter().Text(e.Empresa).FontSize(14).SemiBold().FontColor(Acento);
            c.Item().AlignCenter().Text("ESTADO DE CUENTA INTEGRAL DEL CLIENTE").FontSize(18).SemiBold().FontColor(Primario);
            c.Item().AlignCenter().Text($"Fecha de Emisión: {Fecha(e.FechaEmision)}").FontSize(9).FontColor(Gris);
        });
    }

    private static void DatosCliente(ColumnDescriptor col, EstadoCuentaIntegralDto e)
    {
        Titulo(col, "Datos del Cliente");
        var c = e.Cliente;
        string Dato(string etiqueta, string? valor) => $"{etiqueta}: {(string.IsNullOrWhiteSpace(valor) ? "—" : valor)}";

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); });
            void Fila(params (string et, string? v)[] celdas)
            {
                foreach (var (et, v) in celdas)
                    t.Cell().Element(x => Celda(x)).Text(txt =>
                    {
                        txt.Span(et + ": ").SemiBold();
                        txt.Span(string.IsNullOrWhiteSpace(v) ? "—" : v);
                    });
            }
            Fila(("ID Cliente", c.IdCliente.ToString()), ("Nombre", c.Nombre), ("Clasificación", c.Clasificacion));
            Fila(("Teléfonos", c.Telefonos), ("Correo", c.Correo), ("Estatus", c.Activo ? "Activo" : "Inactivo"));
            t.Cell().ColumnSpan(2).Element(x => Celda(x)).Text(txt => { txt.Span("Dirección: ").SemiBold(); txt.Span(string.IsNullOrWhiteSpace(c.Direccion) ? "—" : c.Direccion); });
            t.Cell().Element(x => Celda(x)).Text(txt => { txt.Span("Ruta / Zona: ").SemiBold(); txt.Span($"{c.Ruta ?? "—"} / {c.Zona ?? "—"}"); });
            t.Cell().Element(x => Celda(x)).Text(txt => { txt.Span("Vendedor Asignado: ").SemiBold(); txt.Span(string.IsNullOrWhiteSpace(c.Vendedor) ? "—" : c.Vendedor); });
            t.Cell().ColumnSpan(2).Element(x => Celda(x)).Text(txt => { txt.Span("Referencia: ").SemiBold(); txt.Span(string.IsNullOrWhiteSpace(c.Referencia) ? "—" : c.Referencia); });
        });

        var colorRiesgo = e.PerfilRiesgo switch { "Alto" => Rojo, "Medio" => Ambar, _ => Verde };
        col.Item().Row(r =>
        {
            r.AutoItem().PaddingRight(6).Text("Perfil de Riesgo:").FontSize(11).FontColor(Acento);
            r.AutoItem().Background(colorRiesgo).PaddingVertical(2).PaddingHorizontal(10)
                .Text(e.PerfilRiesgo).FontColor("#FFFFFF").SemiBold();
        });
    }

    private static void Dashboard(ColumnDescriptor col, EstadoCuentaIntegralDto e)
    {
        Titulo(col, "Dashboard Financiero");
        var r = e.Resumen;

        void Fila(params (string titulo, string valor, string? color)[] kpis)
            => col.Item().Row(row =>
            {
                row.Spacing(4);
                foreach (var (titulo, valor, color) in kpis)
                    row.RelativeItem().Background(FondoSuave).Padding(8).Column(c =>
                    {
                        c.Item().AlignCenter().Text(titulo).FontSize(8).FontColor(Gris);
                        c.Item().AlignCenter().PaddingTop(3).Text(valor).FontSize(11).Bold().FontColor(color ?? Primario);
                    });
            });

        // Fila 1: cómo se integra lo vendido (subtotal − descuentos = total) y lo cobrado (enganches + abonos).
        Fila(("Subtotal Productos", Dinero(r.HistoricoSubtotal), null),
             ("Descuentos", Dinero(r.HistoricoDescuentos), Ambar),
             ("Histórico Vendido", Dinero(r.HistoricoVendido), null),
             ("Enganches", Dinero(r.HistoricoEnganches), null),
             ("Abonos", Dinero(r.HistoricoAbonos), null),
             ("Histórico Pagado", Dinero(r.HistoricoPagado), Verde));
        // Fila 2: bonificaciones (racha, plan a meses, acuerdo), traspasos por renovación, saldo y conteos.
        Fila(("Bonificaciones", Dinero(r.HistoricoBonificaciones), Ambar),
             ("Traspaso Renovación", Dinero(r.HistoricoTraspasos), null),
             ("Saldo Pendiente", Dinero(r.SaldoPendiente), r.SaldoPendiente > 0 ? Rojo : Verde),
             ("Créditos Activos", r.CreditosActivos.ToString(), null),
             ("Liquidados", r.Liquidados.ToString(), null),
             ("Cancelados", r.Cancelados.ToString(), null));

        // Cuadre: lo vendido menos lo cobrado y lo bonificado debe coincidir con el saldo.
        col.Item().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Gris));
            t.Span("Cuadre: ").SemiBold();
            t.Span($"Vendido {Dinero(r.HistoricoVendido)} − Enganches {Dinero(r.HistoricoEnganches)} − Abonos {Dinero(r.HistoricoAbonos)} " +
                   $"− Bonificaciones {Dinero(r.HistoricoBonificaciones)} − Traspasos {Dinero(r.HistoricoTraspasos)} = Saldo {Dinero(r.SaldoPendiente)}");
        });
    }

    private static string ColorEstado(string estado, decimal saldo)
        => saldo <= 0 ? Verde : Ambar;

    private static void ResumenCreditos(ColumnDescriptor col, EstadoCuentaIntegralDto e)
    {
        Titulo(col, "Resumen Consolidado de Créditos");
        if (e.Creditos.Count == 0)
        {
            col.Item().Text("El cliente no tiene créditos registrados.").Italic().FontColor(Gris);
            return;
        }
        col.Item().DefaultTextStyle(x => x.FontSize(7.5f)).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.1f); cd.RelativeColumn(1.15f); cd.RelativeColumn(1.35f); cd.RelativeColumn(1.05f);
                cd.RelativeColumn(1.1f); cd.RelativeColumn(1.05f); cd.RelativeColumn(1.05f); cd.RelativeColumn(1.05f);
                cd.RelativeColumn(1.1f); cd.RelativeColumn(1.15f);
            });
            foreach (var h in new[] { "Crédito", "Fecha Compra", "Estado", "Descuento", "Total Crédito", "Enganche", "Abonos", "Bonif./ Traspaso", "Saldo Pendiente", "Último Pago" })
                t.Cell().Element(x => Celda(x, true)).Text(h).FontColor("#FFFFFF").SemiBold();
            foreach (var c in e.Creditos)
            {
                t.Cell().Element(x => Celda(x)).Text(c.Codigo).SemiBold();
                t.Cell().Element(x => Celda(x)).Text(Fecha(c.FechaCompra));
                t.Cell().Element(x => Celda(x)).Text(c.Estado.ToUpperInvariant()).FontColor(ColorEstado(c.Estado, c.Saldo)).SemiBold();
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Descuento));
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Total));
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Enganche));
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Abonos));
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Bonificaciones + c.Traspaso));
                t.Cell().Element(x => Celda(x)).Text(Dinero(c.Saldo));
                t.Cell().Element(x => Celda(x)).Text(Fecha(c.FechaUltimoPago));
            }
        });
    }

    private static void DetalleActivo(ColumnDescriptor col, EstadoCuentaIntegralDetalleDto d)
    {
        col.Item().PageBreak();
        Titulo(col, "Detalle de Créditos Activos");
        var c = d.Credito;
        col.Item().Border(0.5f).BorderColor(FondoSuave).Background("#FAFAFA").Padding(10).Column(box =>
        {
            box.Spacing(5);
            box.Item().Text($"CRÉDITO {c.Codigo} (Fecha de Apertura: {Fecha(c.FechaCompra)})").SemiBold().FontColor(Primario).FontSize(10);
            box.Item().Text(t =>
            {
                t.Span("Subtotal: ").SemiBold(); t.Span($"{Dinero(c.Subtotal)}  |  ");
                t.Span("Descuento Global: ").SemiBold(); t.Span($"{Dinero(c.Descuento)}  |  ");
                t.Span("Total del Crédito: ").SemiBold(); t.Span(Dinero(c.Total));
            });
            box.Item().Text(t =>
            {
                t.Span("Enganche: ").SemiBold(); t.Span($"{Dinero(c.Enganche)}  |  ");
                t.Span("Abonos: ").SemiBold(); t.Span($"{Dinero(c.Abonos)}  |  ");
                t.Span("Total Pagado: ").SemiBold(); t.Span(Dinero(c.Pagado));
            });
            box.Item().Text(t =>
            {
                t.Span("Bonif. por Racha: ").SemiBold(); t.Span($"{Dinero(d.BonificacionRacha)}  |  ");
                t.Span("Liquidación por Plan: ").SemiBold(); t.Span($"{Dinero(d.LiquidacionPlan)}  |  ");
                t.Span("Liquidación por Acuerdo / Ajuste: ").SemiBold(); t.Span(Dinero(d.Condonacion));
            });
            box.Item().Text(t =>
            {
                t.Span("Saldo Insoluto Pendiente: ").SemiBold(); t.Span(Dinero(c.Saldo)).Bold().FontColor(Rojo);
                t.Span($"   (Total {Dinero(c.Total)} − Pagado {Dinero(c.Pagado)} − Bonificaciones {Dinero(c.Bonificaciones)})").FontSize(8).FontColor(Gris);
            });
            box.Item().Text(t => { t.Span("Avance de Pago: ").SemiBold(); t.Span($"{d.AvancePorcentaje:0.#}%"); });
            // Barra de avance
            box.Item().Height(8).Background(FondoSuave).Row(r =>
            {
                var llenado = (float)Math.Clamp(d.AvancePorcentaje, 0m, 100m);
                if (llenado > 0) r.RelativeItem(llenado).Background(Verde);
                if (llenado < 100) r.RelativeItem(100f - llenado);
            });

            if (d.Productos.Count > 0)
            {
                box.Item().PaddingTop(6).Text("Productos Adquiridos:").SemiBold().FontColor(Acento);
                box.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(1.2f); cd.RelativeColumn(3.2f); cd.RelativeColumn(0.9f); cd.RelativeColumn(1.3f); cd.RelativeColumn(1.3f); });
                    foreach (var h in new[] { "Código", "Descripción", "Cantidad", "Precio Unitario", "Total" })
                        t.Cell().Element(x => Celda(x, true)).Text(h).FontColor("#FFFFFF").SemiBold();
                    foreach (var p in d.Productos)
                    {
                        t.Cell().Element(x => Celda(x)).Text(p.Codigo);
                        t.Cell().Element(x => Celda(x)).Text(p.Descripcion);
                        t.Cell().Element(x => Celda(x)).Text(p.Cantidad.ToString());
                        t.Cell().Element(x => Celda(x)).Text(Dinero(p.PrecioUnitario));
                        t.Cell().Element(x => Celda(x)).Text(Dinero(p.Importe));
                    }
                });
            }

            // ---- Tabla de Amortización (Agenda de Cuotas) — DESACTIVADA en el estado de cuenta ----
            // Se conserva para reutilizarla en el comprobante de venta en PDF (envío al registrar la venta).
            // Los datos siguen llegando en EstadoCuentaIntegralDetalleDto.Cuotas; basta con descomentar este bloque.
            // if (d.Cuotas.Count > 0)
            // {
            //     box.Item().PaddingTop(6).Text("Tabla de Amortización (Agenda de Cuotas):").SemiBold().FontColor(Acento);
            //     box.Item().Table(t =>
            //     {
            //         t.ColumnsDefinition(cd => { cd.ConstantColumn(30); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.3f); });
            //         foreach (var h in new[] { "No.", "Fecha Cuota", "Monto Cuota", "Pagado", "Estatus", "Saldo Insoluto" })
            //             t.Cell().Element(x => Celda(x, true)).Text(h).FontColor("#FFFFFF").SemiBold();
            //         foreach (var q in d.Cuotas)
            //         {
            //             var pagada = string.Equals(q.Estatus, "PAGADA", StringComparison.OrdinalIgnoreCase);
            //             var fondo = pagada ? "#E8F5E9" : null;
            //             var colorEst = pagada ? Verde : string.Equals(q.Estatus, "VENCIDA", StringComparison.OrdinalIgnoreCase) ? Rojo : Ambar;
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(q.Numero.ToString());
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(Fecha(q.Vencimiento));
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(Dinero(q.MontoCuota));
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(Dinero(q.Pagado));
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(q.Estatus.ToUpperInvariant()).FontColor(colorEst).SemiBold();
            //             t.Cell().Element(x => Celda(x, false, fondo)).Text(Dinero(q.SaldoInsoluto));
            //         }
            //     });
            // }

            if (d.Pagos.Count > 0)
            {
                box.Item().PaddingTop(6).Text("Historial de Pagos del Crédito:").SemiBold().FontColor(Acento);
                box.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.RelativeColumn(1.1f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.5f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.2f); cd.RelativeColumn(1.5f); });
                    foreach (var h in new[] { "Folio", "Fecha", "Movimiento", "Monto", "Saldo Después", "Cobrador" })
                        t.Cell().Element(x => Celda(x, true)).Text(h).FontColor("#FFFFFF").SemiBold();
                    foreach (var p in d.Pagos)
                    {
                        t.Cell().Element(x => Celda(x)).Text(p.Folio ?? "—");
                        t.Cell().Element(x => Celda(x)).Text(Fecha(p.Fecha));
                        t.Cell().Element(x => Celda(x)).Text(p.Movimiento ?? "—");
                        t.Cell().Element(x => Celda(x)).Text(Dinero(p.Monto));
                        t.Cell().Element(x => Celda(x)).Text(Dinero(p.SaldoActual));
                        t.Cell().Element(x => Celda(x)).Text(p.Cobrador ?? "—");
                    }
                });
            }
        });
    }

    private static void Cerrados(ColumnDescriptor col, string titulo, string etiquetaCierre,
        IReadOnlyList<EstadoCuentaIntegralCerradoDto> filas, string vacio)
    {
        Titulo(col, titulo);
        if (filas.Count == 0)
        {
            col.Item().Text(vacio).Italic().FontColor(Gris);
            return;
        }
        col.Item().DefaultTextStyle(x => x.FontSize(8)).Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(1.2f); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn();
                cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn(); cd.RelativeColumn();
            });
            foreach (var h in new[] { "Crédito", "Fecha Compra", etiquetaCierre, "Descuento", "Total Crédito", "Enganche", "Total Pagado", "Bonif.", "Traspaso Renov." })
                t.Cell().Element(x => Celda(x, true)).Text(h).FontColor("#FFFFFF").SemiBold();
            foreach (var f in filas)
            {
                t.Cell().Element(x => Celda(x)).Text(f.Codigo);
                t.Cell().Element(x => Celda(x)).Text(Fecha(f.FechaCompra));
                t.Cell().Element(x => Celda(x)).Text(Fecha(f.FechaCierre));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Descuento));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Total));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Enganche));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Pagado));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Bonificaciones));
                t.Cell().Element(x => Celda(x)).Text(Dinero(f.Traspaso));
            }
        });
    }

    private static void Cierre(ColumnDescriptor col, EstadoCuentaIntegralDto e)
    {
        col.Item().ShowEntire().Column(c =>
        {
            c.Spacing(8);
            c.Item().Element(x => x).PaddingTop(8).BorderLeft(3).BorderColor(Primario).PaddingLeft(6)
                .Text("Resumen Final y Observaciones").FontSize(12).SemiBold().FontColor(Acento);
            c.Item().Border(0.5f).BorderColor(FondoSuave).Background("#FAFAFA").Padding(10).Text(e.Observaciones);
            c.Item().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Gris));
                t.Span("Operador Emisor: ").SemiBold(); t.Span(e.Operador);
                t.Span("   |   "); t.Span("Zona Operación: ").SemiBold(); t.Span(e.ZonaOperacion ?? "—");
            });

            // Firmas
            c.Item().PaddingTop(20).Row(r =>
            {
                void Firma(string linea1, string linea2)
                    => r.RelativeItem().Column(f =>
                    {
                        // Línea de firma de ancho fijo (independiente del largo del texto).
                        f.Item().PaddingHorizontal(25).Height(40).BorderBottom(0.8f).BorderColor("#333333");
                        f.Item().PaddingTop(4).AlignCenter().Text(linea1).FontSize(9).SemiBold().FontColor(Primario);
                        f.Item().AlignCenter().Text(linea2).FontSize(8).FontColor(Gris);
                    });
                Firma("Acepto de Conformidad", "Firma del Cliente o Representante");
                Firma(e.Operador, "Ejecutivo de Cobranza Autorizado");
            });

            // Huella de integridad
            c.Item().PaddingTop(14).Border(0.5f).BorderColor(Acento).Background("#FAFAFA").Padding(8).Column(s =>
            {
                s.Item().Text("Huella de Integridad del Documento (SHA-256):").FontSize(8).SemiBold().FontColor(Primario);
                s.Item().PaddingTop(3).Background(FondoSuave).Padding(5).Text(e.SelloIntegridad).FontSize(7.5f).LetterSpacing(0.05f).FontColor("#555555");
                s.Item().PaddingTop(3).Text($"Emitido el {e.FechaEmision.EnZonaProyecto():dd/MM/yyyy HH:mm}. La huella se calcula sobre las cifras principales del estado de cuenta.")
                    .FontSize(7.5f).Italic().FontColor(Gris);
            });
        });
    }
}
