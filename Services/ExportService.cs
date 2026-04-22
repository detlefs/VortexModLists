using ClosedXML.Excel;
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
            sb.AppendLine($"## {group.Key}")
              .AppendLine()
              .AppendLine("| modName | ID | Version | Homepage | Status |")
              .AppendLine("|---|---|---|---|---|");

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
        var groups = mods
            .GroupBy(m => m.Game, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook();

        foreach (var group in groups)
        {
            var sheetName = SanitizeSheetName(group.Key);
            var ws = workbook.Worksheets.Add(sheetName);

            string[] headers = { "modName", "ID", "Version", "Homepage", "Status" };
            for (var i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
            }

            var rows = group
                .OrderBy(m => m.ModName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < rows.Count; i++)
            {
                var mod = rows[i];
                var row = i + 2;

                ws.Cell(row, 1).Value = mod.ModName;
                ws.Cell(row, 2).Value = mod.Id;
                ws.Cell(row, 3).Value = mod.Version;
                ws.Cell(row, 5).Value = mod.IsActive ? statusActive : statusInactive;

                if (!string.IsNullOrWhiteSpace(mod.Homepage))
                {
                    var cell = ws.Cell(row, 4);
                    cell.Value = "Link";
                    cell.SetHyperlink(new XLHyperlink(mod.Homepage));
                }
            }

            ws.Columns().AdjustToContents();
        }

        workbook.SaveAs(filePath);
    }

    private static string Csv(string value)
    {
        var sanitized = value.Replace("\"", "\"\"");
        return $"\"{sanitized}\"";
    }

    private static string EscapeMarkdown(string value)
    {
        return value.Replace("|", "\\|").Replace("\r", string.Empty).Replace("\n", " ").Trim();
    }

    private static string SanitizeSheetName(string name)
    {
        // Excel sheet names: max 31 chars, no \ / ? * [ ]
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        if (sanitized.Length > 31)
        {
            sanitized = sanitized[..31];
        }
        return string.IsNullOrWhiteSpace(sanitized) ? "Mods" : sanitized;
    }
}
