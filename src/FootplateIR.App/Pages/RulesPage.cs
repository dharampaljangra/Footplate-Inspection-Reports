using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Footplate rules (IRPWM 2024 up to ACS 16, WCR G&amp;SR 2016) and the monthly inspection schedule check.</summary>
public class RulesPage : ContentPage
{
    private readonly Picker _role;
    private readonly MonthSelector _month = new(DateTime.Today.Year, DateTime.Today.Month);
    private readonly VerticalStackLayout _schedule = new() { Spacing = 4 };
    private readonly Entry _search = Ui.Entry("", "Search: patrolman, gateman, jacket, 1004, SR 4.42 ...");
    private readonly Picker _book = Ui.Picker(new[] { "All books", Rules.Irpwm, Rules.Gsr });
    private readonly VerticalStackLayout _rules = new() { Spacing = 4 };
    private AppData D => DataService.Current.Data;

    public RulesPage()
    {
        Title = "Rules & schedule";
        _role = Ui.Picker(Rules.Roles, D.Settings.Role);
        _role.SelectedIndexChanged += async (_, _) =>
        {
            D.Settings.Role = Ui.Value(_role);
            await DataService.Current.SaveAsync();
            ShowSchedule();
        };
        _month.Changed += (_, _) => ShowSchedule();
        _search.TextChanged += (_, _) => ShowRules();
        _book.SelectedIndexChanged += (_, _) => ShowRules();
        Content = Ui.Page(
            Ui.Title("Footplate rules & inspection schedule"),
            Ui.Card(Ui.Stack(
                Ui.Heading("Inspection schedule check"),
                Ui.Muted($"From the saved reports, against {Rules.Irpwm} Table-1A / 1B and Para 1004(9)(e)."),
                Ui.Cols(Ui.Field("Schedule of", _role), Ui.Field("Month", _month)),
                _schedule,
                Ui.Small("Export / print the schedule", ExportSchedule)), Ui.Soft),
            Ui.Card(Ui.Stack(
                Ui.Heading("Rules and instructions"),
                Ui.Cols(_search, _book),
                _rules)));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ShowSchedule();
        ShowRules();
    }

    private void ShowSchedule()
    {
        _schedule.Children.Clear();
        if (_month.Month == 0) { _schedule.Children.Add(Ui.Muted("Choose a month.")); return; }
        foreach (var s in Rules.Schedule(D.Reports, Ui.Value(_role), _month.Year, _month.Month))
        {
            var color = s.Status switch { "Done" => Ui.Ok, "Not done" => Ui.Accent, "Due" => Ui.Warn, _ => Colors.DimGray };
            var st = Ui.Stack(
                Ui.Text($"{s.Item}  -  {s.Period}", 14, Ui.Primary, true),
                Ui.Text($"{s.Required}   ({s.Ref})", 12),
                Ui.Text(s.Status + (s.Done.Count > 0 ? ": " + string.Join(", ", s.Done.Select(Fmt.Date)) : ""), 13, color, true));
            if (s.Note.Length > 0) st.Children.Add(Ui.Muted(s.Note));
            st.Spacing = 1;
            _schedule.Children.Add(Ui.Card(st, s.Status == "Not done" ? Ui.WarnBack : null));
        }
    }

    private void ShowRules()
    {
        _rules.Children.Clear();
        var book = Ui.Value(_book);
        var list = Rules.Search(_search.Text, book == "All books" ? null : book).ToList();
        if (list.Count == 0) _rules.Children.Add(Ui.Muted("Nothing found."));
        foreach (var r in list)
        {
            var st = Ui.Stack(
                Ui.Text($"{r.Ref}  -  {r.Title}", 14, Ui.Primary, true),
                Ui.Muted($"{r.Book}  |  {r.Topic}"),
                Ui.Text(r.Text, 13));
            st.Spacing = 2;
            _rules.Children.Add(Ui.Card(st));
        }
    }

    private async Task ExportSchedule()
    {
        if (_month.Month == 0) { await Ui.Alert("Schedule", "Choose a month."); return; }
        var role = Ui.Value(_role);
        await ExportService.ExportAsync("Inspection schedule", $"Inspection_schedule_{_month.Period}",
            f => Export.Document(Rules.ScheduleDocument(D, role, _month.Year, _month.Month), f, "Schedule"),
            ExportFormat.Pdf, ExportFormat.Word, ExportFormat.Excel, ExportFormat.Html);
    }
}
