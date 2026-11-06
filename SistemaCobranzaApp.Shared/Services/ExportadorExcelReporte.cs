using System.Globalization;
using System.IO.Compression;
using System.Xml;
using MudBlazor.Utilities;
using SistemaCobranzaApp.Shared.Theme;

namespace SistemaCobranzaApp.Shared.Services;

public sealed class ReporteExcelInfo
{
    public string Empresa { get; set; } = "";
    public string Titulo { get; set; } = "";
    public Dictionary<string, string> Filtros { get; set; } = new();
    public List<ReporteExcelKpi> Kpis { get; set; } = new();
    public DateTime FechaGeneracion { get; set; } = DateTime.Now;
}

public sealed class ReporteExcelKpi
{
    public string Nombre { get; set; } = "";
    public string Valor { get; set; } = "";
}

/// <summary>
/// Reporte con formato (empresa, título, filtros, KPIs, tabla con filtros, totales y pie).
/// Sin dependencias externas: genera el XLSX directamente (compatible con Web/WASM y MAUI).
/// </summary>
public static partial class ExportadorExcel
{
    private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    // Índices de estilo (cellXfs) definidos en EstilosXml.
    private const int EstDefecto = 0, EstEmpresa = 1, EstTitulo = 2, EstNegrita = 3, EstKpi = 4,
        EstCabecera = 5, EstMoneda = 6, EstTotalEtiqueta = 7, EstTotalMoneda = 8, EstPie = 9;

    private readonly record struct Celda(object? Valor, int Estilo, string? Formula = null);

    public static byte[] Crear(ReporteExcelInfo info, string[] columnas, IEnumerable<object?[]> filas,
        int[]? columnasMoneda = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(columnas);
        ArgumentNullException.ThrowIfNull(filas);

        var monedas = new HashSet<int>(columnasMoneda ?? []);
        var celdas = new SortedDictionary<int, SortedDictionary<int, Celda>>();
        var uniones = new List<string>();
        // Ancho total del reporte: la tabla o los bloques de KPIs (2 columnas + 1 de separación cada uno), lo que sea mayor.
        int ultimaCol = Math.Max(Math.Max(1, columnas.Length), info.Kpis.Count * 3 - 1);

        void Poner(int f, int c, object? v, int estilo, string? formula = null)
        {
            if (!celdas.TryGetValue(f, out var fila)) celdas[f] = fila = new();
            fila[c] = new Celda(v, estilo, formula);
        }

        void Unir(int f1, int c1, int f2, int c2, int estilo)
        {
            for (var f = f1; f <= f2; f++)
                for (var c = c1; c <= c2; c++)
                    if (!(celdas.TryGetValue(f, out var fi) && fi.ContainsKey(c))) Poner(f, c, null, estilo);
            if (f1 != f2 || c1 != c2) uniones.Add($"{Columna(c1)}{f1}:{Columna(c2)}{f2}");
        }

        var r = 1;

        // Empresa
        Poner(r, 1, info.Empresa, EstEmpresa);
        Unir(r, 1, r, ultimaCol, EstEmpresa);
        r += 2;

        // Título
        Poner(r, 1, info.Titulo, EstTitulo);
        Unir(r, 1, r, ultimaCol, EstTitulo);
        r += 2;

        // Fecha
        Poner(r, 1, $"Fecha generación: {info.FechaGeneracion:dd/MM/yyyy HH:mm}", EstDefecto);
        r += 2;

        // Filtros
        foreach (var (clave, valor) in info.Filtros)
        {
            Poner(r, 1, clave, EstNegrita);
            Poner(r, 2, valor, EstDefecto);
            r++;
        }
        if (info.Filtros.Count > 0) r++;

        // KPIs (bloques de 2 columnas x 3 filas, separados por una columna)
        if (info.Kpis.Count > 0)
        {
            var colKpi = 1;
            foreach (var kpi in info.Kpis)
            {
                Poner(r, colKpi, $"{kpi.Nombre}\n{kpi.Valor}", EstKpi);
                Unir(r, colKpi, r + 2, colKpi + 1, EstKpi);
                colKpi += 3;
            }
            r += 4;
        }

        // Cabecera de tabla
        var filaCabecera = r;
        for (var i = 0; i < columnas.Length; i++) Poner(filaCabecera, i + 1, columnas[i], EstCabecera);
        r++;

        // Datos
        var inicioDatos = r;
        var sumas = new Dictionary<int, decimal>();
        var anchos = new double[ultimaCol];
        for (var i = 0; i < ultimaCol; i++) anchos[i] = i < columnas.Length ? (columnas[i]?.Length ?? 0) + 4 : 14;

        foreach (var registro in filas)
        {
            if (registro.Length != columnas.Length)
                throw new ArgumentException("El número de valores no coincide con las columnas.");
            if (r >= 1_048_000) throw new InvalidOperationException("El reporte excede el máximo de filas de Excel.");

            for (var i = 0; i < registro.Length; i++)
            {
                var v = registro[i];
                var esNumero = v is byte or short or int or long or float or double or decimal;
                var moneda = esNumero && monedas.Contains(i + 1);
                Poner(r, i + 1, v, moneda ? EstMoneda : EstDefecto);
                if (moneda) sumas[i + 1] = sumas.GetValueOrDefault(i + 1) + Convert.ToDecimal(v, CultureInfo.InvariantCulture);

                var largo = esNumero ? 14 : (Convert.ToString(v, CultureInfo.InvariantCulture)?.Length ?? 0) + 2;
                if (largo > anchos[i]) anchos[i] = largo;
            }
            r++;
        }
        var finDatos = r - 1;
        var hayDatos = finDatos >= inicioDatos;

        // Totales
        r++;
        if (monedas.Count > 0)
        {
            Poner(r, 1, "TOTALES", EstTotalEtiqueta);
            if (hayDatos)
            {
                foreach (var col in monedas.Where(c => c >= 1 && c <= columnas.Length))
                {
                    var l = Columna(col);
                    Poner(r, col, sumas.GetValueOrDefault(col), EstTotalMoneda, $"SUM({l}{inicioDatos}:{l}{finDatos})");
                }
            }
            r++;
        }

        // Pie
        r += 2;
        Poner(r, 1, "Reporte generado por "+info.Empresa, EstPie);

        // ---- Escritura del paquete ----
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Texto(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Texto(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Texto(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="{NsMain}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Reporte" sheetId="1" r:id="rId1"/></sheets>
                  <calcPr fullCalcOnLoad="1"/>
                </workbook>
                """);
            Texto(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            Texto(zip, "xl/styles.xml", EstilosXml());

            using var salida = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml = XmlWriter.Create(salida, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", NsMain);

            // Congela todo lo anterior a los datos (cabecera incluida).
            xml.WriteStartElement("sheetViews", NsMain);
            xml.WriteStartElement("sheetView", NsMain); xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", NsMain);
            xml.WriteAttributeString("ySplit", filaCabecera.ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("topLeftCell", $"A{filaCabecera + 1}");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();

            xml.WriteStartElement("cols", NsMain);
            for (var i = 0; i < ultimaCol; i++)
            {
                xml.WriteStartElement("col", NsMain);
                xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("width", Math.Clamp(anchos[i], 10, 60).ToString("0.##", CultureInfo.InvariantCulture));
                xml.WriteAttributeString("customWidth", "1");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("sheetData", NsMain);
            foreach (var (nFila, fila) in celdas)
            {
                xml.WriteStartElement("row", NsMain);
                xml.WriteAttributeString("r", nFila.ToString(CultureInfo.InvariantCulture));
                foreach (var (nCol, celda) in fila) EscribirCelda(xml, nFila, nCol, celda);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            if (hayDatos)
            {
                xml.WriteStartElement("autoFilter", NsMain);
                xml.WriteAttributeString("ref", $"A{filaCabecera}:{Columna(columnas.Length)}{finDatos}");
                xml.WriteEndElement();
            }

            if (uniones.Count > 0)
            {
                xml.WriteStartElement("mergeCells", NsMain);
                xml.WriteAttributeString("count", uniones.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var u in uniones)
                {
                    xml.WriteStartElement("mergeCell", NsMain);
                    xml.WriteAttributeString("ref", u);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }

            xml.WriteEndElement(); xml.WriteEndDocument();
        }
        return memoria.ToArray();
    }

    private static void EscribirCelda(XmlWriter xml, int fila, int col, Celda celda)
    {
        xml.WriteStartElement("c", NsMain);
        xml.WriteAttributeString("r", Columna(col) + fila.ToString(CultureInfo.InvariantCulture));
        if (celda.Estilo != 0) xml.WriteAttributeString("s", celda.Estilo.ToString(CultureInfo.InvariantCulture));

        var valor = celda.Valor;
        if (valor is byte or short or int or long or float or double or decimal)
        {
            if (celda.Formula is not null) xml.WriteElementString("f", NsMain, celda.Formula);
            xml.WriteElementString("v", NsMain, Convert.ToString(valor, CultureInfo.InvariantCulture));
        }
        else if (valor is not null)
        {
            xml.WriteAttributeString("t", "inlineStr");
            xml.WriteStartElement("is", NsMain); xml.WriteStartElement("t", NsMain);
            xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            var texto = Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
            texto = new string(texto.Where(XmlConvert.IsXmlChar).ToArray());
            xml.WriteString(texto.Length > 32767 ? texto[..32767] : texto);
            xml.WriteEndElement(); xml.WriteEndElement();
        }
        xml.WriteEndElement();
    }

    // Colores tomados de AppTheme (paleta del sistema): si cambia el color institucional,
    // el Excel lo adopta automáticamente. Primario = título/cabecera; tintes claros = KPIs y totales.
    private static string ArgbPrimario() => Argb(AppTheme.Theme.PaletteLight.Primary, 0);
    private static string ArgbKpi() => Argb(AppTheme.Theme.PaletteLight.Primary, 0.85);
    private static string ArgbTotales() => Argb(AppTheme.Theme.PaletteLight.Secondary, 0.8);

    // Aclara el color mezclándolo con blanco (0 = original, 1 = blanco) y lo devuelve como AARRGGBB.
    private static string Argb(MudColor color, double aclarar)
    {
        static int Mezcla(int canal, double f) => (int)Math.Round(canal + (255 - canal) * f);
        return $"FF{Mezcla(color.R, aclarar):X2}{Mezcla(color.G, aclarar):X2}{Mezcla(color.B, aclarar):X2}";
    }

    private static string EstilosXml() => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1"><numFmt numFmtId="164" formatCode="&quot;$&quot;#,##0.00"/></numFmts>
          <fonts count="6">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="18"/><name val="Calibri"/></font>
            <font><b/><sz val="14"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><i/><sz val="9"/><color rgb="FF7F7F7F"/><name val="Calibri"/></font>
          </fonts>
          <fills count="5">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="{{ArgbPrimario()}}"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="{{ArgbKpi()}}"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="{{ArgbTotales()}}"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left style="thin"><color auto="1"/></left><right style="thin"><color auto="1"/></right><top style="thin"><color auto="1"/></top><bottom style="thin"><color auto="1"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="10">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="3" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf numFmtId="0" fontId="4" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
            <xf numFmtId="0" fontId="3" fillId="4" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
            <xf numFmtId="164" fontId="3" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/>
            <xf numFmtId="0" fontId="5" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
