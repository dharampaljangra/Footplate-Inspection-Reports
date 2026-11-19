using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Brings reports and duty lists in from WhatsApp text, photos (OCR), Word, PDF, Excel, HTML and backups.</summary>
public class ImportPage : ContentPage
{
    private readonly Editor _text = Ui.Editor("", "Paste one or more WhatsApp reports or duty lists here (a whole exported chat is fine) ...", 160);
    private readonly VerticalStackLayout _results = new() { Spacing = 4 };
    private ImportResult? _last;
    private readonly Dictionary<InspectionReport, CheckBox> _pickReports = new();
    private readonly Dictionary<DutyRoster, CheckBox> _pickRosters = new();
    private readonly Dictionary<TrainSchedule, CheckBox> _pickTrains = new();

    public ImportPage()
    {
        Title = "Import";
        Content = Ui.Page(
            Ui.Title("Import reports and duty lists"),
            Ui.Card(Ui.Stack(
                Ui.Text("Reports written on WhatsApp, typed notes in Word / PDF / Excel, photos of a printed note, night duty lists of patrolmen / keymen, and train timings (working time table in Excel, or schedules copied from NTES) are all read here. A preview is shown before anything is added; reports already saved (same date, train and route) are skipped.", 13),
                Ui.Muted("Files saved by this app (PDF, Word, Excel, HTML) carry an exact copy of the report and come back unchanged.")), Ui.Soft),
            Ui.Wrap(
                Ui.Button("Paste from clipboard", Paste),
                Ui.Button("Take photo (camera)", () => Photo(true), false),
                Ui.Button("Choose photo / screenshot", () => Photo(false), false),
                Ui.Button("Choose file (Word, PDF, Excel, text, backup)", PickFile, false)),
            _text,
            Ui.Wrap(Ui.Button("Read the text above", ReadText), Ui.Button("Clear", () => { _text.Text = ""; _results.Children.Clear(); return Task.CompletedTask; }, false)),
            _results);
    }

    private async Task Paste()
    {
        var t = await Clipboard.Default.GetTextAsync();
        if (string.IsNullOrWhiteSpace(t)) { await Ui.Alert("Clipboard", "The clipboard is empty. Copy the WhatsApp message(s) first (long-press > Copy)."); return; }
        _text.Text = t;
        await ReadText();
    }

    private async Task Photo(bool camera)
    {
        var path = await OcrService.GetPhotoAsync(camera);
        if (path is null) return;
        string text;
        try { text = await OcrService.RecognizeAsync(path); }
        catch (Exception ex) { await Ui.Alert("Reading the photo", ex.Message); return; }
        if (text.Trim().Length == 0) { await Ui.Alert("Reading the photo", "No text found. Hold the phone straight over the page in good light and try again."); return; }
        _text.Text = text;
        await ReadText("Photo");
        await Ui.Alert("Photo read", "Check the text read from the photo (OCR can mistake 0/O, 1/l, 5/S) - correct it in the box and tap 'Read the text above' again if needed.");
    }

    private async Task PickFile()
    {
        var f = await FileService.PickAsync("Choose a report / duty list file",
            new[] { ".txt", ".docx", ".pdf", ".xlsx", ".xlsm", ".csv", ".html", ".htm", ".json", ".fir", ".jpg", ".jpeg", ".png" },
            new[] { "text/plain", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/pdf",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "text/csv", "text/html", "application/json", "application/octet-stream", "image/*" });
        if (f is null) return;
        var path = await FileService.ToCacheAsync(f);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            try { _text.Text = await OcrService.RecognizeAsync(path); }
            catch (Exception ex) { await Ui.Alert("Reading the photo", ex.Message); return; }
            await ReadText("Photo");
            return;
        }
        var data = DataService.Current.Data;
        var res = await Task.Run(() => FileImport.Read(path, data));
        if (res.Backup is not null)
        {
            if (!await Ui.Confirm("Backup file", res.Summary + "\n\nAdd its reports and duty lists to the ones on this device (newer edits win)?")) return;
            var s = Store.Merge(data, res.Backup);
            await DataService.Current.SaveAsync();
            await Ui.Alert("Backup merged", s.ToString());
            return;
        }
        _text.Text = res.Text;
        Show(res);
    }

    private async Task ReadText() => await ReadText("Text");

    private async Task ReadText(string source)
    {
        var res = new ImportResult { Source = source == "Text" && (_text.Text ?? "").Contains('[') ? "WhatsApp" : source, Text = _text.Text ?? "" };
        await Task.Run(() => FileImport.Analyze(res, DataService.Current.Data));
        Show(res);
    }

    private void Show(ImportResult res)
    {
        _last = res;
        _pickReports.Clear();
        _pickRosters.Clear();
        _pickTrains.Clear();
        _results.Children.Clear();
        _results.Children.Add(Ui.Heading(res.Summary));
        foreach (var m in res.Messages.Where(m => res.Reports.Count + res.Rosters.Count + res.Trains.Count == 0 || m.StartsWith("No ")))
            _results.Children.Add(Ui.Text(m, 13, Ui.Warn));
        var data = DataService.Current.Data;
        foreach (var r in res.Reports)
        {
            bool dup = data.Reports.Any(x => x.Id == r.Id || x.Date == r.Date && x.TrainNo == r.TrainNo && x.FromStation == r.FromStation && x.ToStation == r.ToStation && r.TrainNo.Length > 0);
            var cb = new CheckBox { IsChecked = !dup, Color = Ui.Primary };
            _pickReports[r] = cb;
            var report = r;
            var s = Ui.Stack(
                Ui.Text($"{Fmt.Date(r.Date)}  {r.Kind} {ReportLayout.Purposes(r)}", 14, Ui.Primary, true),
                Ui.Text($"{r.FromStation} {Fmt.Time(r.Departure)} → {r.ToStation} {Fmt.Time(r.Arrival)} by {r.TrainNo}, loco {r.LocoLine}; LP {r.LocoPilot}", 13),
                Ui.Muted($"{r.Sightings.Count} field staff entries, {r.Observations.Count} observations, {r.Shortcomings.Count} shortcomings, {r.Locations.Count} locations" + (dup ? "  -  already saved" : "")),
                Ui.Small("Open / correct before adding", async () =>
                {
                    await Navigation.PushAsync(new ReportEditorPage(report, isNew: true));
                    cb.IsChecked = false;
                }));
            s.Spacing = 2;
            _results.Children.Add(Ui.Card(Ui.Row(cb, s), dup ? Ui.Soft : null));
        }
        foreach (var r in res.Rosters)
        {
            var cb = new CheckBox { IsChecked = true, Color = Ui.Primary };
            _pickRosters[r] = cb;
            var s = Ui.Stack(Ui.Text(r.Title, 14, Ui.Primary, true));
            foreach (var b in r.Beats)
                s.Children.Add(Ui.Text($"({b.No}) {b.Range}: " + string.Join(", ", b.Persons.Select(p => $"{p.Display} {p.Mobile}{(p.MobileLooksRight ? "" : " (!)")}")), 12));
            s.Spacing = 1;
            _results.Children.Add(Ui.Card(Ui.Row(cb, s)));
        }
        foreach (var t in res.Trains)
        {
            var cb = new CheckBox { IsChecked = true, Color = Ui.Primary };
            _pickTrains[t] = cb;
            var known = Timetable.Find(data.Trains, t.TrainNo) is not null;
            var s = Ui.Stack(
                Ui.Text($"Train {t.Title}  ({t.Route}, {t.Stops.Count} stops)" + (known ? "  - replaces the saved schedule" : ""), 14, Ui.Primary, true),
                Ui.Text(string.Join("  ", t.Stops.Take(14).Select(x => $"{x.Code} {Fmt.Time(x.Time)}")) + (t.Stops.Count > 14 ? " ..." : ""), 12));
            s.Spacing = 1;
            _results.Children.Add(Ui.Card(Ui.Row(cb, s)));
        }
        if (res.Reports.Count + res.Rosters.Count + res.Trains.Count > 0)
            _results.Children.Add(Ui.Button("Add the ticked items", AddSelected));
    }

    private async Task AddSelected()
    {
        if (_last is null) return;
        var data = DataService.Current.Data;
        var reports = _pickReports.Where(p => p.Value.IsChecked).Select(p => p.Key).ToList();
        foreach (var r in reports) if (r.Source.Length == 0) r.Source = _last.Source;
        var (added, skipped) = Store.AddReports(data, reports);
        var (nr, nb) = Patrol.Merge(data.Rosters, _pickRosters.Where(p => p.Value.IsChecked).Select(p => p.Key));
        var (ta, tu) = Timetable.Merge(data.Trains, _pickTrains.Where(p => p.Value.IsChecked).Select(p => p.Key));
        await DataService.Current.SaveAsync();
        await Ui.Alert("Added", $"{added} report(s) added" + (skipped > 0 ? $", {skipped} already saved" : "") +
                                (nb > 0 ? $"; duty lists: {nb} beats saved ({nr} new list(s))" : "") +
                                (ta + tu > 0 ? $"; timetable: {ta} train(s) added, {tu} updated" : "") + ".");
        _results.Children.Clear();
        _text.Text = "";
    }
}
