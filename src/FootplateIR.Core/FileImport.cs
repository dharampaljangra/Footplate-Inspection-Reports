using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using Wp = DocumentFormat.OpenXml.Wordprocessing;

namespace FootplateIR.Core;

/// <summary>What was found in a file / text to import (shown as a preview before anything is added).</summary>
public sealed class ImportResult
{
    public string Source { get; set; } = "";
    /// <summary>Text read from the file (editable before reading reports from it, e.g. after OCR).</summary>
    public string Text { get; set; } = "";
    public List<InspectionReport> Reports { get; } = new();
    public List<DutyRoster> Rosters { get; } = new();
    public List<TrainSchedule> Trains { get; } = new();
    /// <summary>True when the reports came from data embedded by this app (exact copy).</summary>
    public bool Exact { get; set; }
    public AppData? Backup { get; set; }
    public List<string> Messages { get; } = new();

    public string Summary =>
        Backup is not null ? $"Backup: {Backup.Reports.Count} reports, {Backup.Rosters.Count} duty lists."
        : $"{Reports.Count} inspection report(s), {Rosters.Count} duty list(s), {Trains.Count} train schedule(s) found" + (Exact ? " (exact copy saved by this app)." : ".");
}

/// <summary>Reads reports and duty lists from text, Word, PDF, Excel, HTML and backup files.</summary>
public static class FileImport
{
    public static readonly string[] Extensions = { ".txt", ".csv", ".docx", ".pdf", ".xlsx", ".xlsm", ".html", ".htm", ".json", ".fir" };

    public static ImportResult Read(string path, AppData data)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var res = new ImportResult { Source = ext switch
        {
            ".docx" => "Word", ".pdf" => "PDF", ".xlsx" or ".xlsm" => "Excel", ".html" or ".htm" => "HTML", ".json" or ".fir" => "Backup",
            _ => "Text"
        } };
        string? embedded = null;
        switch (ext)
        {
            case ".json" or ".fir":
                var json = File.ReadAllText(path, Encoding.UTF8);
                try
                {
                    res.Backup = JsonSerializer.Deserialize(json, FirJsonContext.Default.AppData);
                    if (res.Backup is not null && (res.Backup.Reports.Count > 0 || res.Backup.Rosters.Count > 0)) return res;
                }
                catch (JsonException) { }
                res.Backup = null;
                embedded = json;
                break;
            case ".docx":
                res.Text = ReadDocx(path, out embedded);
                break;
            case ".pdf":
                res.Text = ReadPdf(path, out embedded);
                if (res.Text.Trim().Length == 0 && embedded is null)
                    res.Messages.Add("No text in this PDF - it is a scanned (photo) PDF. Take a photo / screenshot of the page and use 'Import from photo' to read it.");
                break;
            case ".xlsx" or ".xlsm":
                res.Text = ReadExcel(path, out embedded);
                if (embedded is null)
                    try { res.Trains.AddRange(Timetable.ReadExcel(path, data.Masters)); } catch (Exception ex) { res.Messages.Add("Timetable sheet not read: " + ex.Message); }
                break;
            case ".html" or ".htm":
                var html = File.ReadAllText(path, Encoding.UTF8);
                var m = Regex.Match(html, DataPacking.Marker + "[A-Za-z0-9+/=]+");
                if (m.Success) embedded = DataPacking.Unpack(m.Value);
                res.Text = HtmlToText(html);
                break;
            default:
                res.Text = File.ReadAllText(path, Encoding.UTF8);
                break;
        }
        if (embedded is not null)
        {
            try
            {
                var list = JsonSerializer.Deserialize(embedded, FirJsonContext.Default.ListInspectionReport);
                if (list is not null && list.Count > 0)
                {
                    res.Reports.AddRange(list);
                    res.Exact = true;
                    return res;
                }
            }
            catch (JsonException) { }
        }
        Analyze(res, data);
        return res;
    }

    /// <summary>Reads reports and duty lists from <see cref="ImportResult.Text"/>.</summary>
    public static void Analyze(ImportResult res, AppData data)
    {
        res.Reports.Clear();
        res.Rosters.Clear();
        var excelTrains = res.Trains.Where(t => t.Source == "Excel").ToList();
        res.Trains.Clear();
        res.Trains.AddRange(excelTrains);
        var text = res.Text ?? "";
        var parsed = ReportParser.Parse(text, data.Profile, data.Masters, res.Source);
        res.Reports.AddRange(parsed.Reports);
        // Duty lists: the whole text when it is not a report, or the messages of a chat that are not reports.
        var rest = res.Reports.Count == 0 ? text : string.Join("\n", ReportParser.SplitMessages(text).Where(c => !ReportParser.LooksLikeReport(c)));
        if (Patrol.LooksLikeRoster(rest)) res.Rosters.AddRange(Patrol.ParseRosters(rest, res.Source));
        if (res.Trains.Count == 0 && Timetable.LooksLikeTimetable(rest)) res.Trains.AddRange(Timetable.Parse(rest, data.Masters, res.Source));
        foreach (var t in res.Trains)
            res.Messages.Add($"Train {t.Title}: {t.Stops.Count} stops, {t.Route}");
        if (res.Reports.Count == 0 && res.Rosters.Count == 0 && res.Trains.Count == 0)
            res.Messages.Add("No inspection report or duty list was recognised. Check the text below (correct any OCR mistakes) and read it again.");
        else
            res.Messages.AddRange(parsed.Reports.Count > 0 ? parsed.Messages : new List<string>());
        foreach (var r in res.Rosters)
            res.Messages.Add($"{r.Title}: {r.Beats.Count} beats, {r.Beats.Sum(b => b.Persons.Count)} persons" +
                             (r.Beats.SelectMany(b => b.Persons).Any(p => !p.MobileLooksRight) ? " (check mobile numbers marked !)" : ""));
    }

    private static string ReadDocx(string path, out string? embedded)
    {
        embedded = null;
        var sb = new StringBuilder();
        using var wd = WordprocessingDocument.Open(path, false);
        var main = wd.MainDocumentPart;
        if (main is null) return "";
        foreach (var cx in main.CustomXmlParts)
        {
            using var r = new StreamReader(cx.GetStream());
            var x = r.ReadToEnd();
            if (x.Contains(DataPacking.Marker)) embedded = DataPacking.Unpack(Regex.Replace(x, "<[^>]+>", ""));
        }
        var body = main.Document?.Body;
        if (body is null) return "";
        foreach (var el in body.ChildElements)
        {
            if (el is Wp.Paragraph p) sb.AppendLine(ParaText(p));
            else if (el is Wp.Table t)
                foreach (var row in t.Elements<Wp.TableRow>())
                    foreach (var cell in row.Elements<Wp.TableCell>())
                    {
                        var ct = string.Join("\n", cell.Elements<Wp.Paragraph>().Select(ParaText)).Trim();
                        if (ct.Length > 0) sb.AppendLine(ct);
                    }
        }
        return sb.ToString();
    }

    private static string ParaText(Wp.Paragraph p)
    {
        var sb = new StringBuilder();
        foreach (var el in p.Descendants())
        {
            if (el is Wp.Text t) sb.Append(t.Text);
            else if (el is Wp.Break) sb.Append('\n');
            else if (el is Wp.TabChar) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static string ReadPdf(string path, out string? embedded)
    {
        embedded = null;
        var bytes = File.ReadAllBytes(path);
        var raw = Encoding.Latin1.GetString(bytes);
        var b = raw.IndexOf("%FIRDATA-BEGIN", StringComparison.Ordinal);
        var e = raw.IndexOf("%FIRDATA-END", StringComparison.Ordinal);
        if (b >= 0 && e > b)
        {
            var packed = string.Concat(raw[(b + 14)..e].Split('\n').Select(l => l.Trim().TrimStart('%')));
            embedded = DataPacking.Unpack(packed);
        }
        var sb = new StringBuilder();
        try
        {
            using var doc = PdfDocument.Open(bytes);
            foreach (var page in doc.GetPages())
            {
                // Words in the order they were drawn; a new line whenever the baseline changes or the text goes back left.
                double? lastY = null, lastRight = null;
                var line = new StringBuilder();
                foreach (var w in page.GetWords())
                {
                    var y = w.BoundingBox.Bottom;
                    bool newLine = lastY is double ly && (Math.Abs(y - ly) > 2.0 || (lastRight is double lr && w.BoundingBox.Left < lr - 2));
                    if (newLine)
                    {
                        sb.AppendLine(line.ToString().Trim());
                        line.Clear();
                    }
                    else if (line.Length > 0)
                    {
                        // A wide gap between words on one line = separate cells.
                        if (lastRight is double lr2 && w.BoundingBox.Left - lr2 > 12) { sb.AppendLine(line.ToString().Trim()); line.Clear(); }
                        else line.Append(' ');
                    }
                    line.Append(w.Text);
                    lastY = y;
                    lastRight = w.BoundingBox.Right;
                }
                if (line.Length > 0) sb.AppendLine(line.ToString().Trim());
            }
        }
        catch (Exception ex) when (embedded is not null)
        {
            _ = ex;
        }
        return sb.ToString();
    }

    private static string ReadExcel(string path, out string? embedded)
    {
        embedded = null;
        var sb = new StringBuilder();
        using var wb = new XLWorkbook(path);
        if (wb.Worksheets.TryGetWorksheet("_firdata", out var ds))
            embedded = DataPacking.Unpack(string.Concat(ds.CellsUsed().OrderBy(c => c.Address.RowNumber).Select(c => c.GetString())));
        foreach (var ws in wb.Worksheets.Where(w => w.Name != "_firdata"))
        {
            foreach (var row in ws.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetFormattedString().Trim()).Where(s => s.Length > 0).Distinct().ToList();
                if (cells.Count == 0) continue;
                bool labelPairs = (cells.Count == 2 || cells.Count == 4) &&
                                  cells.Where((_, i) => i % 2 == 0).All(c => c.Length <= 35 && !Regex.IsMatch(c, @"\d{3}") && !c.EndsWith('.'));
                if (labelPairs)
                    for (int i = 0; i < cells.Count; i += 2) sb.AppendLine(cells[i].TrimEnd(':') + ": " + cells[i + 1]);
                else sb.AppendLine(string.Join(" ", cells));
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string HtmlToText(string html)
    {
        var s = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<br\s*/?>|</(p|div|tr|h\d|li|td|th)>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        return WebUtility.HtmlDecode(s);
    }
}
