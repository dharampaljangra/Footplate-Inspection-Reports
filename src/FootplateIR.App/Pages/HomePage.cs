using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>All reports of a month / year, with search and the period exports.</summary>
public class HomePage : ContentPage
{
    private readonly MonthSelector _sel;
    private readonly Entry _search = Ui.Entry("", "Search train, station, KM, crew name ...");
    private readonly Label _summary = Ui.Muted("");
    private readonly VerticalStackLayout _list = new() { Spacing = 4 };
    private static bool _errorShown;

    public HomePage()
    {
        Title = "Footplate Inspection Reports";
        var latest = DataService.Current.Data.Reports.Select(r => r.Date).DefaultIfEmpty(DateOnly.FromDateTime(DateTime.Today)).Max();
        _sel = new MonthSelector(latest.Year, latest.Month);
        _sel.Changed += (_, _) => Refresh();
        _search.TextChanged += (_, _) => Refresh();
        Content = Ui.Page(
            Ui.Title("Footplate Inspection Reports"),
            Ui.Wrap(
                Ui.Button("+ New report", NewReport),
                Ui.Button("Import (WhatsApp text / photo / file)", () => AppShell.GoAsync<ImportPage>(), false),
                Ui.Button("Who is on duty here? (call)", () => AppShell.GoAsync<DutyPage>(), false)),
            Ui.Card(Ui.Stack(
                _sel,
                _search,
                _summary,
                Ui.Wrap(
                    Ui.Small("Export all notes of the period", ExportNotes),
                    Ui.Small("Register (one line per inspection)", ExportRegister),
                    Ui.Small("Pending locations", () => AppShell.GoAsync<LocationsPage>()))), Ui.Soft),
            _list);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
        var err = DataService.Current.LoadError;
        if (err is not null && !_errorShown)
        {
            _errorShown = true;
            _ = Ui.Alert("Data file", "The saved data could not be read (" + err + "). The previous copy was used; the unreadable file was kept aside.");
        }
    }

    private List<InspectionReport> Selected()
    {
        var q = (_search.Text ?? "").Trim();
        return DataService.Current.Data.Reports
            .Where(r => _sel.Includes(r.Date))
            .Where(r => q.Length == 0 || Matches(r, q))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Departure)
            .ToList();
    }

    private static bool Matches(InspectionReport r, string q)
    {
        bool Has(string s) => s.Contains(q, StringComparison.OrdinalIgnoreCase);
        return Has(r.TrainNo) || Has(r.FromStation) || Has(r.ToStation) || Has(r.LocoPilot) || Has(r.AssistantLocoPilot) || Has(r.TrainManager)
               || Has(r.LocoNo) || Has(r.Kind) || r.Locations.Any(l => Has(l.Location) || Has(l.Feature)) || r.Sightings.Any(s => Has(s.Location))
               || r.Shortcomings.Any(Has) || Has(Fmt.Date(r.Date));
    }

    private void Refresh()
    {
        var list = Selected();
        _list.Children.Clear();
        var nights = list.Count(r => r.Kind.Contains("Night", StringComparison.OrdinalIgnoreCase));
        var hours = list.Sum(r => r.Duration?.TotalHours ?? 0);
        _summary.Text = list.Count == 0
            ? $"No reports for {_sel.Period}. Tap '+ New report', or import your WhatsApp reports."
            : $"{_sel.Period}: {list.Count} inspections ({nights} night, {list.Count - nights} day), {hours:0.0} hours on the footplate, " +
              $"{list.Sum(r => r.Shortcomings.Count)} shortcomings, {list.Sum(r => r.Locations.Count)} locations.";
        foreach (var r in list)
        {
            var report = r;
            var head = new Label
            {
                Text = $"{Fmt.Date(r.Date)}   {r.FromStation} → {r.ToStation}   by {r.TrainNo}",
                FontAttributes = FontAttributes.Bold, TextColor = Ui.Primary, FontSize = 15
            };
            var s = Ui.Stack(head,
                Ui.Text($"{r.Kind} {ReportLayout.Purposes(r)}  •  {Fmt.Time(r.Departure)} - {Fmt.Time(r.Arrival)} ({Fmt.Duration(r.Duration)})  •  Loco {r.LocoLine}", 13),
                Ui.Muted($"LP {r.LocoPilot}  |  {r.Sightings.Sum(x => x.Count)} field staff seen" +
                         (r.Sightings.Any(x => x.IsDeficient) ? $" ({r.Sightings.Count(x => x.IsDeficient)} deficient)" : "") +
                         $"  |  {r.Shortcomings.Count} shortcomings  |  {r.Locations.Count} locations"));
            s.Spacing = 2;
            var card = Ui.Card(s, r.Sightings.Any(x => x.IsDeficient) || r.Shortcomings.Count > 0 ? Color.FromArgb("#FFF8F6") : null);
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await Navigation.PushAsync(new ReportEditorPage(report));
            card.GestureRecognizers.Add(tap);
            _list.Children.Add(card);
        }
    }

    private async Task NewReport()
    {
        var d = DataService.Current.Data;
        var r = Defaults.NewReport(d.Profile, d.Masters);
        // Night footplates mostly start after midnight: date of the morning.
        await Navigation.PushAsync(new ReportEditorPage(r, isNew: true));
    }

    private async Task ExportNotes()
    {
        var list = Selected();
        if (list.Count == 0) { await Ui.Alert("Export", "No reports in this period."); return; }
        var data = DataService.Current.Data;
        await ExportService.ExportAsync($"{list.Count} footplate inspection notes - {_sel.Period}", $"Footplate_Inspection_Notes_{_sel.Period}",
            f => Export.Reports(list, data, f, _sel.Period));
    }

    private async Task ExportRegister()
    {
        var list = Selected();
        if (list.Count == 0) { await Ui.Alert("Export", "No reports in this period."); return; }
        var data = DataService.Current.Data;
        await ExportService.ExportAsync($"Register of footplate inspections - {_sel.Period}", $"Footplate_Register_{_sel.Period}",
            f => Export.Register(list, data, f, _sel.Period), ExportFormat.Pdf, ExportFormat.Excel, ExportFormat.Word, ExportFormat.Html, ExportFormat.Csv);
    }
}
