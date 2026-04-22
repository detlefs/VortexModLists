using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using VortexModLists.Models;

namespace VortexModLists.Services;

public static class ExportService
{
    public static string ToCsv(IEnumerable<ModEntry> mods, string statusActive, string statusInactive)
    {
        var rows = mods.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Game,modName,ID,Version,Homepage,Status");

        foreach (var mod in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(mod.Game),
                Csv(mod.ModName),
                Csv(mod.Id),
                Csv(mod.Version),
                Csv(mod.Homepage),
                Csv(mod.IsActive ? statusActive : statusInactive)));
        }

        return sb.ToString();
    }

    public static string ToMarkdown(IEnumerable<ModEntry> mods, string statusActive, string statusInactive)
    {
        var groups = mods
            .GroupBy(m => m.Game, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();

        foreach (var group in groups)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| modName | ID | Version | Homepage | Status |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var mod in group.OrderBy(m => m.ModName, StringComparer.OrdinalIgnoreCase))
            {
                var link = string.IsNullOrWhiteSpace(mod.Homepage) ? string.Empty : $"[Link]({EscapeMarkdown(mod.Homepage)})";
                sb.AppendLine($"| {EscapeMarkdown(mod.ModName)} | {EscapeMarkdown(mod.Id)} | {EscapeMarkdown(mod.Version)} | {link} | {(mod.IsActive ? statusActive : statusInactive)} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static void ToExcel(string filePath, IEnumerable<ModEntry> mods, string statusActive, string statusInactive)
    {
        var rows = mods.ToList();
        using var archive = ZipFile.Open(filePath, ZipArchiveMode.Create);

        WriteEntry(archive, "[Content_Types].xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>");

        WriteEntry(archive, "_rels/.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>");

        WriteEntry(archive, "xl/workbook.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"Mods\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

        WriteEntry(archive, "xl/_rels/workbook.xml.rels",
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>");

        // Collect hyperlinks: rowIndex (1-based, header = 1) → url
        // Homepage is column E (index 5, col letter "E")
        var hyperlinks = new List<(int row, string url)>();
        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
            "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheetData>");

        WriteRow(sheet, new[] { "Game", "modName", "ID", "Version", "Homepage", "Status" });

        for (var i = 0; i < rows.Count; i++)
        {
            var mod = rows[i];
            var rowIndex = i + 2; // 1-based, row 1 = header

            WriteRow(sheet, new[]
            {
                mod.Game,
                mod.ModName,
                mod.Id,
                mod.Version,
                string.IsNullOrWhiteSpace(mod.Homepage) ? string.Empty : "Link",
                mod.IsActive ? statusActive : statusInactive
            });

            if (!string.IsNullOrWhiteSpace(mod.Homepage))
            {
                hyperlinks.Add((rowIndex, mod.Homepage));
            }
        }

        sheet.Append("</sheetData>");

        // Write <hyperlinks> element referencing relationship IDs
        if (hyperlinks.Count > 0)
        {
            sheet.Append("<hyperlinks>");
            for (var i = 0; i < hyperlinks.Count; i++)
            {
                var (row, _) = hyperlinks[i];
                sheet.Append($"<hyperlink ref=\"E{row}\" r:id=\"hId{i + 1}\"/>");
            }
            sheet.Append("</hyperlinks>");
        }

        sheet.Append("</worksheet>");
        WriteEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());

        // Write sheet1.xml.rels with one external relationship per hyperlink
        var sheetRels = new StringBuilder();
        sheetRels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 0; i < hyperlinks.Count; i++)
        {
            var (_, url) = hyperlinks[i];
            sheetRels.Append(
                $"<Relationship Id=\"hId{i + 1}\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" " +
                $"Target=\"{SecurityElement.Escape(url)}\" TargetMode=\"External\"/>");
        }
        sheetRels.Append("</Relationships>");
        WriteEntry(archive, "xl/worksheets/_rels/sheet1.xml.rels", sheetRels.ToString());
    }

    private static string Csv(string value)
    {
        var sanitized = value.Replace("\"", "\"\"");
        return $"\"{sanitized}\"";
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteRow(StringBuilder sb, IReadOnlyList<string> values)
    {
        sb.Append("<row>");
        foreach (var value in values)
        {
            sb.Append("<c t=\"inlineStr\"><is><t>");
            sb.Append(SecurityElement.Escape(value));
            sb.Append("</t></is></c>");
        }
        sb.Append("</row>");
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|").Replace("\r", string.Empty).Replace("\n", " ").Trim();
    }
}
