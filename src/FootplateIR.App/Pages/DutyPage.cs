using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Night duty lists of patrolmen / keymen: who is on duty at a KM, with call and WhatsApp buttons.</summary>
public class DutyPage : ContentPage
{
    private readonly DatePicker _night = new() { Format = "dd.MM.yyyy" };
    private readonly Entry _km = Ui.Entry("", "KM (48/17) or station code (BUDI)");
    private readonly VerticalStackLayout _here = new() { Spacing = 4 };
    private readonly VerticalStackLayout _lists = new() { Spacing = 4 };
    private AppData D => DataService.Current.Data;

    public DutyPage()
    {
        Title = "Staff on duty";
        var now = DateTime.Now;
        _night.Date = (now.Hour < 12 ? now.AddDays(-1) : now).Date;
        _night.DateSelected += (_, _) => Refresh();
        _km.Completed += (_, _) => ShowHere();
        Content = Ui.Page(
            Ui.Title("Patrolmen & keymen on duty"),
            Ui.Card(Ui.Stack(
                Ui.Field("Night of (patrolmen) / date (keymen)", _night),
                Ui.Field("Where is the train?", Ui.Row(_km, Ui.Small("Show", () => { ShowHere(); return Task.CompletedTask; }, Ui.Primary, Colors.White))),
                _here), Ui.Soft),
            Ui.Wrap(
                Ui.Button("Import tonight's list (paste / photo / PDF)", () => AppShell.GoAsync<ImportPage>()),
                Ui.Button("Type a list", NewRoster, false),
                Ui.Button("Share / print the lists", ShareLists, false)),
            _lists);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private DateOnly Night => DateOnly.FromDateTime(Ui.DateOf(_night));

    private List<DutyRoster> ForDate() =>
        D.Rosters.Where(r => r.IsNight ? r.Date == Night : r.Date == Night || r.Date == Night.AddDays(1))
            .OrderBy(r => r.IsNight ? 0 : 1).ThenBy(r => r.Beats.Select(b => b.FromKm).DefaultIfEmpty(0).Min()).ToList();

    /// <summary>Name, mobile and Call / WhatsApp buttons.</summary>
    public static View PersonRow(DutyPerson p, string message)
    {
        var name = Ui.Text($"{p.Display}   {p.MobileDisplay}{(p.MobileLooksRight ? "" : "  (check number)")}" + (p.Note.Length > 0 ? $"  - {p.Note}" : ""), 13,
            p.MobileLooksRight ? null : Ui.Warn);
        if (p.Mobile.Length == 0) return name;
        return Ui.Row(name,
            Ui.Small("Call", () => Dialer.CallAsync(p.Mobile), Ui.Ok, Colors.White),
            Ui.Small("WhatsApp", () => Dialer.WhatsAppAsync(p.Mobile, message)));
    }

    private void ShowHere()
    {
        _here.Children.Clear();
        var km = Patrol.KmOf(_km.Text ?? "", D.Masters.Chainage);
        if (km is null)
        {
            if ((_km.Text ?? "").Trim().Length > 0) _here.Children.Add(Ui.Text("Not understood - write a KM like 48/17 or 48, or a station code of the section.", 13, Ui.Warn));
            return;
        }
        var found = Patrol.OnDutyAt(D.Rosters, km.Value, Night, Night.AddDays(1));
        found.AddRange(Patrol.OnDutyAt(D.Rosters.Where(r => !r.IsNight), km.Value, Night, Night).Where(x => found.All(f => f.Beat != x.Beat)));
        if (found.Count == 0) _here.Children.Add(Ui.Muted($"No saved list covers KM {Patrol.KmText(km.Value)} for this date."));
        foreach (var (roster, beat) in found)
        {
            _here.Children.Add(Ui.Text($"{roster.Category} - beat {beat.No} ({beat.Range})", 14, Ui.Primary, true));
            foreach (var p in beat.Persons) _here.Children.Add(PersonRow(p, ""));
        }
        // Chart position now.
        var chart = D.Charts.FirstOrDefault(c => c.AppliesTo(Night));
        var beatOnChart = chart?.Beats.FirstOrDefault(b => b.Contains(km.Value));
        if (chart is not null && beatOnChart is not null)
        {
            var nowNm = Patrol.NightMinutes(TimeOnly.FromDateTime(DateTime.Now));
            var pos = Patrol.PositionAt(chart, beatOnChart, nowNm);
            var later = Patrol.PositionAt(chart, beatOnChart, nowNm + 5);
            _here.Children.Add(Ui.Muted(pos is double p
                ? $"{chart.Name}: {beatOnChart.Patrolmen} ({beatOnChart.Range}) should be near KM {Patrol.KmText(p)} now" +
                  (later is double l && Math.Abs(l - p) > 0.01 ? $", walking towards KM {(l > p ? Patrol.KmText(beatOnChart.ToKm) : Patrol.KmText(beatOnChart.FromKm))}." : " (turning point).")
                : $"{chart.Name}: outside patrolling hours now."));
        }
    }

    private void Refresh()
    {
        _lists.Children.Clear();
        var list = ForDate();
        if (list.Count == 0)
        {
            _lists.Children.Add(Ui.Muted("No duty list saved for this date. Paste the WhatsApp list or import the PDF / photo sent by the section."));
            var dates = D.Rosters.Select(r => r.Date).Distinct().OrderByDescending(d => d).Take(8).ToList();
            if (dates.Count > 0) _lists.Children.Add(Ui.Muted("Saved lists: " + string.Join(", ", dates.Select(Fmt.Date))));
        }
        foreach (var r in list)
        {
            var roster = r;
            var s = Ui.Stack(Ui.Text(r.Title, 15, Ui.Primary, true));
            foreach (var b in r.Beats.OrderBy(b => b.FromKm))
            {
                s.Children.Add(Ui.Text($"({b.No})  {b.Range}", 13, null, true));
                foreach (var p in b.Persons) s.Children.Add(PersonRow(p, ""));
            }
            s.Children.Add(Ui.Wrap(
                Ui.Small("Edit", () => Navigation.PushAsync(new RosterEditorPage(roster))),
                Ui.Small("Share", () => FileService.ShareTextAsync(Export.RosterText(roster), roster.Title)),
                Ui.Small("Delete", async () =>
                {
                    if (!await Ui.Confirm("Delete", "Delete this duty list?")) return;
                    D.Rosters.Remove(roster);
                    await DataService.Current.SaveAsync();
                    Refresh();
                })));
            _lists.Children.Add(Ui.Card(s));
        }
        ShowHere();
    }

    private async Task NewRoster()
    {
        var r = new DutyRoster { Date = Night, Category = "Cold Weather Patrolman", Source = "Typed" };
        await Navigation.PushAsync(new RosterEditorPage(r, isNew: true));
    }

    private async Task ShareLists()
    {
        var list = ForDate();
        if (list.Count == 0) { await Ui.Alert("Duty lists", "No list for this date."); return; }
        await ExportService.ExportAsync("Duty lists", $"Duty_list_{Fmt.Date(Night)}", f => f == ExportFormat.Text
            ? System.Text.Encoding.UTF8.GetBytes(string.Join("\n\n", list.Select(Export.RosterText)))
            : Export.Document(ReportLayout.Roster(list, D), f, "Duty list"),
            ExportFormat.Pdf, ExportFormat.Word, ExportFormat.Excel, ExportFormat.Html, ExportFormat.Text);
    }
}

/// <summary>Edit a duty list: date, category and the beats as text ("(1) km 77/0-81/0" then "Name - mobile" lines).</summary>
public class RosterEditorPage : ContentPage
{
    private readonly DutyRoster _r;
    private readonly bool _isNew;
    private readonly DatePicker _date = new() { Format = "dd.MM.yyyy" };
    private readonly Picker _cat;
    private readonly Entry _area = Ui.Entry("", "e.g. SSE/P.Way/MLGH, KM 77/0 - 117/0");
    private readonly Editor _text = Ui.Editor("", "(1) km 77/0-81/0\nRamraj Meena - 7372969217\nHome Guard Manish - 9636083245\n(2) km 81/0-85/0\n...", 260);

    public RosterEditorPage(DutyRoster r, bool isNew = false)
    {
        _r = r;
        _isNew = isNew;
        Title = isNew ? "New duty list" : "Edit duty list";
        _cat = Ui.Picker(DataService.Current.Data.Masters.DutyCategories, r.Category);
        _date.Date = r.Date.ToDateTime(TimeOnly.MinValue);
        _area.Text = r.Area;
        _text.Text = string.Join("\n", r.Beats.OrderBy(b => b.FromKm).SelectMany(b =>
            new[] { $"({b.No}) km {Patrol.KmText(b.FromKm)}-{Patrol.KmText(b.ToKm)}" }
                .Concat(b.Persons.Select(p => $"{(p.HomeGuard ? "Home Guard " : "")}{p.Name} - {p.Mobile}"))));
        Content = Ui.Page(
            Ui.Cols(Ui.Field("Night of / date", _date), Ui.Field("Staff", _cat)),
            Ui.Field("Area / unit", _area),
            Ui.Muted("One beat per heading line \"(n) km from-to\", then one person per line \"Name - mobile\". Write \"Home Guard\" before the name for home guards."),
            _text,
            Ui.Wrap(Ui.Button("Save", Save), Ui.Button("Cancel", () => Navigation.PopAsync(), false)));
    }

    private async Task Save()
    {
        var parsed = Patrol.ParseRosters("cwp\n" + (_text.Text ?? ""), "Typed");
        var beats = parsed.SelectMany(p => p.Beats).ToList();
        if (beats.Count == 0) { await Ui.Alert("Duty list", "No beat found - start each beat with a line like \"(1) km 77/0-81/0\"."); return; }
        _r.Date = DateOnly.FromDateTime(Ui.DateOf(_date));
        _r.Category = Ui.Value(_cat);
        _r.Area = (_area.Text ?? "").Trim();
        _r.Beats = beats;
        _r.ModifiedUtc = DateTime.UtcNow;
        var d = DataService.Current.Data;
        if (_isNew || !d.Rosters.Contains(_r)) d.Rosters.Add(_r);
        await DataService.Current.SaveAsync();
        await Navigation.PopAsync();
    }
}
