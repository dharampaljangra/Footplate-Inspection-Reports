using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Locations reported for better running, grouped by spot (repeats show up), with compliance.</summary>
public class LocationsPage : ContentPage
{
    private readonly MonthSelector _sel = new(DateTime.Today.Year, 0);
    private readonly Switch _pendingOnly = new() { IsToggled = true, OnColor = Ui.Primary };
    private readonly Switch _allTime = new() { IsToggled = true, OnColor = Ui.Primary };
    private readonly Label _summary = Ui.Muted("");
    private readonly VerticalStackLayout _list = new() { Spacing = 4 };
    private AppData D => DataService.Current.Data;

    public LocationsPage()
    {
        Title = "Locations to be attended";
        _sel.Changed += (_, _) => { _allTime.IsToggled = false; Refresh(); };
        _pendingOnly.Toggled += (_, _) => Refresh();
        _allTime.Toggled += (_, _) => Refresh();
        Content = Ui.Page(
            Ui.Title("Locations to be attended for better running"),
            Ui.Card(Ui.Stack(
                Ui.Row(Ui.Text("Pending only", 14), _pendingOnly, Ui.Text("All dates", 14), _allTime),
                _sel,
                _summary,
                Ui.Wrap(Ui.Small("Export list (PDF, Word, Excel ...)", ExportList))), Ui.Soft),
            _list);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private string Period => _allTime.IsToggled ? "All dates" : _sel.Period;

    private List<InspectionReport> Reports() => D.Reports.Where(r => _allTime.IsToggled || _sel.Includes(r.Date)).ToList();

    private void Refresh()
    {
        _list.Children.Clear();
        var groups = ReportLayout.LocationGroups(Reports()).Where(g => !_pendingOnly.IsToggled || g.Pending > 0).ToList();
        _summary.Text = $"{groups.Count} spots; {groups.Count(g => g.Items.Count > 1 && g.Pending > 0)} reported more than once and still pending (shown first in red).";
        foreach (var g in groups.OrderByDescending(g => g.Pending > 0 && g.Items.Count > 1).ThenBy(g => Locations.SortValue(g.Items[0].Loc.Location)))
        {
            var group = g;
            var locs = string.Join(", ", g.Items.Select(x => ReportLayout.KmDisplay(x.Loc.Location)).Distinct());
            var features = string.Join("; ", g.Items.Select(x => x.Loc.Feature).Where(f => f.Length > 0).Distinct());
            var s = Ui.Stack(
                Ui.Text(locs + (features.Length > 0 ? $"  ({features})" : ""), 15, Ui.Primary, true),
                Ui.Text($"Reported {g.Items.Count} time(s): " + string.Join(", ", g.Items.Select(x => $"{Fmt.Date(x.Report.Date)} ({x.Report.TrainNo})")), 13),
                Ui.Text(g.Pending > 0 ? "Pending" : "Attended" + (g.Items.Select(x => x.Loc.AttendedOn).Max() is DateOnly d ? " on " + Fmt.Date(d) : "") +
                        (g.Items.Select(x => x.Loc.ActionTaken).FirstOrDefault(a => a.Length > 0) is string a ? " - " + a : ""), 13, g.Pending > 0 ? Ui.Warn : Ui.Ok, true),
                Ui.Wrap(
                    Ui.Small("Mark attended", () => Mark(group, LocationStatus.Attended)),
                    Ui.Small("Under watch", () => Mark(group, LocationStatus.Watch)),
                    Ui.Small("Pending", () => Mark(group, LocationStatus.Pending))));
            s.Spacing = 2;
            _list.Children.Add(Ui.Card(s, g.Pending > 0 && g.Items.Count > 1 ? Ui.WarnBack : null));
        }
    }

    private async Task Mark(ReportLayout.LocationGroup g, string status)
    {
        DateOnly? on = null;
        var action = "";
        if (status == LocationStatus.Attended)
        {
            var d = await Ui.Prompt("Attended on", "Date (dd.mm.yyyy)", Fmt.Date(DateOnly.FromDateTime(DateTime.Today)));
            if (d is null) return;
            on = Fmt.ParseDate(d) ?? DateOnly.FromDateTime(DateTime.Today);
            action = await Ui.Prompt("Action taken", "e.g. Packing done by MSP / SEJ attended / joint rectified", "") ?? "";
        }
        foreach (var (report, loc) in g.Items)
        {
            loc.Status = status;
            loc.AttendedOn = on;
            if (status == LocationStatus.Attended) loc.ActionTaken = action.Trim();
            report.ModifiedUtc = DateTime.UtcNow;
        }
        await DataService.Current.SaveAsync();
        Refresh();
    }

    private async Task ExportList()
    {
        var reports = Reports();
        await ExportService.ExportAsync("Locations to be attended", $"Locations_to_be_attended_{Period}",
            f => f == ExportFormat.Excel ? XlsxRenderer.Workbook(reports, D, Period, includeNotes: false)
                : Export.Document(ReportLayout.LocationRegister(reports, D, Period, _pendingOnly.IsToggled), f, "Locations"),
            ExportFormat.Pdf, ExportFormat.Word, ExportFormat.Excel, ExportFormat.Html);
    }
}
