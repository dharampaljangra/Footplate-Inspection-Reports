using System.Text;

namespace FootplateIR.Core;

public enum ExportFormat { Pdf, Word, Excel, Html, Text, Csv }

/// <summary>Writes reports, registers and duty lists to files in the chosen format.</summary>
public static class Export
{
    public static string Extension(ExportFormat f) => f switch
    {
        ExportFormat.Pdf => ".pdf", ExportFormat.Word => ".docx", ExportFormat.Excel => ".xlsx",
        ExportFormat.Html => ".html", ExportFormat.Csv => ".csv", _ => ".txt"
    };

    public static string Mime(ExportFormat f) => f switch
    {
        ExportFormat.Pdf => "application/pdf",
        ExportFormat.Word => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ExportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ExportFormat.Html => "text/html",
        ExportFormat.Csv => "text/csv",
        _ => "text/plain"
    };

    public static string SafeFile(string s)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\'' }).ToHashSet();
        var n = new string(s.Select(c => bad.Contains(c) ? '_' : c).ToArray());
        return n.Replace(' ', '_');
    }

    public static string FileName(InspectionReport r, ExportFormat f) =>
        SafeFile($"{(r.Kind.Contains("Night") ? "Night" : r.Kind.Contains("Day") ? "Day" : "")}_Footplate_Inspection_{r.FromStation}-{r.ToStation}_{Fmt.Date(r.Date)}").Trim('_') + Extension(f);

    public static byte[] Document(DocModel doc, ExportFormat f, string sheetName = "Report") => f switch
    {
        ExportFormat.Pdf => PdfRenderer.Render(doc),
        ExportFormat.Word => DocxRenderer.Render(doc),
        ExportFormat.Excel => XlsxRenderer.Render(doc, sheetName),
        _ => Encoding.UTF8.GetBytes(HtmlRenderer.Render(doc))
    };

    public static byte[] Report(InspectionReport r, AppData data, ExportFormat f) => f switch
    {
        ExportFormat.Text => Encoding.UTF8.GetBytes(ReportText.ToMessage(r, data.Profile)),
        ExportFormat.Csv => Encoding.UTF8.GetBytes(Csv(new[] { r }, data)),
        _ => Document(ReportLayout.Report(r, data), f, $"{r.Date:dd.MM.yy} {r.TrainNo}")
    };

    /// <summary>Many reports: a register (Excel workbook / CSV) or all notes one after the other (PDF / Word / HTML / text).</summary>
    public static byte[] Reports(IList<InspectionReport> reports, AppData data, ExportFormat f, string period) => f switch
    {
        ExportFormat.Excel => XlsxRenderer.Workbook(reports, data, period),
        ExportFormat.Csv => Encoding.UTF8.GetBytes(Csv(reports, data)),
        ExportFormat.Text => Encoding.UTF8.GetBytes(string.Join(Environment.NewLine + "------------------------------" + Environment.NewLine,
            reports.OrderBy(r => r.Date).Select(r => ReportText.ToMessage(r, data.Profile)))),
        _ => Document(ReportLayout.Reports(reports.OrderBy(r => r.Date).ThenBy(r => r.Departure).ToList(), data, "Footplate Inspection Notes - " + period), f)
    };

    public static byte[] Register(IList<InspectionReport> reports, AppData data, ExportFormat f, string period) => f switch
    {
        ExportFormat.Excel => XlsxRenderer.Workbook(reports, data, period, includeNotes: false),
        ExportFormat.Csv or ExportFormat.Text => Encoding.UTF8.GetBytes(Csv(reports, data)),
        _ => Document(ReportLayout.Register(reports, data, period), f, "Register")
    };

    public static string Csv(IEnumerable<InspectionReport> reports, AppData data)
    {
        static string Q(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        var sb = new StringBuilder();
        sb.AppendLine("Date,Inspection,Purpose,Train No,From,To,Departure,Arrival,Loco,LP,ALP,TM,Visibility,Weather,Field staff,Observations,Shortcomings,Locations");
        foreach (var r in reports.OrderBy(r => r.Date))
            sb.AppendLine(string.Join(",", new[]
            {
                Fmt.Date(r.Date), r.Kind, string.Join("+", r.Purposes), r.TrainNo, r.FromStation, r.ToStation, Fmt.Time(r.Departure), Fmt.Time(r.Arrival),
                r.LocoLine, r.LocoPilot, r.AssistantLocoPilot, r.TrainManager, r.Visibility, r.Weather,
                string.Join(" | ", r.Sightings.Select(ReportText.SightingText)), string.Join(" | ", r.Observations),
                string.Join(" | ", r.Shortcomings), Locations.Join(r.Locations)
            }.Select(Q)));
        return sb.ToString();
    }

    public static string RosterText(DutyRoster r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(r.Title);
        foreach (var b in r.Beats.OrderBy(b => b.FromKm))
        {
            sb.AppendLine($"({b.No}) {b.Range}");
            foreach (var p in b.Persons) sb.AppendLine($"   {p.Display} - {p.Mobile}{(p.Note.Length > 0 ? " (" + p.Note + ")" : "")}");
        }
        return sb.ToString();
    }
}
