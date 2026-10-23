using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace SistemaCobranzaApp.Shared.Services;

/// <summary>Libro XLSX con valores de texto y numéricos, sin ejecutar fórmulas provenientes de los datos.</summary>
public static class ExportadorExcel
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static byte[] Crear(string[] columnas, IEnumerable<object?[]> filas)
    {
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
                </Types>
                """);
            Texto(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Texto(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="utf-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Reporte" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Texto(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="utf-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            using var salida = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
            using var xml = XmlWriter.Create(salida, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false) });
            xml.WriteStartDocument();
            xml.WriteStartElement("worksheet", Ns);
            xml.WriteStartElement("sheetViews", Ns);
            xml.WriteStartElement("sheetView", Ns); xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", Ns); xml.WriteAttributeString("ySplit", "1");
            xml.WriteAttributeString("topLeftCell", "A2"); xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
            xml.WriteStartElement("sheetData", Ns);
            var numero = 0;
            void Fila(object?[] valores)
            {
                if (valores.Length != columnas.Length) throw new ArgumentException("El número de valores no coincide con las columnas.");
                if (++numero > 1_048_576) throw new InvalidOperationException("El reporte excede el máximo de filas de Excel.");
                xml.WriteStartElement("row", Ns); xml.WriteAttributeString("r", numero.ToString(CultureInfo.InvariantCulture));
                for (var i = 0; i < valores.Length; i++)
                {
                    xml.WriteStartElement("c", Ns); xml.WriteAttributeString("r", Columna(i + 1) + numero);
                    var valor = valores[i];
                    if (valor is byte or short or int or long or float or double or decimal)
                    {
                        xml.WriteElementString("v", Ns, Convert.ToString(valor, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        xml.WriteAttributeString("t", "inlineStr");
                        xml.WriteStartElement("is", Ns); xml.WriteStartElement("t", Ns);
                        xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                        var texto = Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "";
                        texto = new string(texto.Where(XmlConvert.IsXmlChar).ToArray());
                        xml.WriteString(texto.Length > 32767 ? texto[..32767] : texto);
                        xml.WriteEndElement(); xml.WriteEndElement();
                    }
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            Fila(columnas.Cast<object?>().ToArray());
            foreach (var fila in filas) Fila(fila);
            xml.WriteEndElement();
            xml.WriteStartElement("autoFilter", Ns); xml.WriteAttributeString("ref", $"A1:{Columna(columnas.Length)}{numero}"); xml.WriteEndElement();
            xml.WriteEndElement(); xml.WriteEndDocument();
        }
        return memoria.ToArray();
    }
    private static string Columna(int valor)
    {
        var nombre = "";
        while (valor > 0) { valor--; nombre = (char)('A' + valor % 26) + nombre; valor /= 26; }
        return nombre;
    }
    private static void Texto(ZipArchive zip, string nombre, string contenido)
    {
        using var escritor = new StreamWriter(zip.CreateEntry(nombre).Open(), new System.Text.UTF8Encoding(false));
        escritor.Write(contenido);
    }
}
