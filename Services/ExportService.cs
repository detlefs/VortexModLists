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

        WriteEntry(archive, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        WriteEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        WriteEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Mods\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");

        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
        WriteRow(sheet, new[] { "Game", "modName", "ID", "Version", "Homepage", "Status" });

        foreach (var mod in rows)
        {
            WriteRow(sheet, new[]
            {
                mod.Game,
                mod.ModName,
                mod.Id,
                mod.Version,
                mod.Homepage,
                mod.IsActive ? statusActive : statusInactive
            });
        }

        sheet.Append("</sheetData></worksheet>");
        WriteEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
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
