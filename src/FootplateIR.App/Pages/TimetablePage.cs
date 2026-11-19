using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Train timings kept in the app (works offline) and NTES for live running.</summary>
public class TimetablePage : ContentPage
{
    private readonly Entry _train = Ui.Entry("", "Train No. e.g. 01910", Keyboard.Numeric);
    private readonly VerticalStackLayout _schedule = new() { Spacing = 2 };
    private readonly Entry _place = Ui.Entry("BUDI", "Station code or KM (48/17)");
    private readonly Entry _from = Ui.Entry("22:00", "from", Keyboard.Numeric);
    private readonly Entry _to = Ui.Entry("07:00", "to", Keyboard.Numeric);
    private readonly VerticalStackLayout _passing = new() { Spacing = 2 };
    private readonly Label _count = Ui.Muted("");
    private AppData D => DataService.Current.Data;

    public TimetablePage()
    {
        Title = "Train timings";
        _train.Completed += (_, _) => ShowTrain();
        _place.Completed += (_, _) => ShowPassing();
        Content = Ui.Page(
            Ui.Title("Train timings"),
            Ui.Card(Ui.Stack(
                Ui.Text("Timings are kept in the app and work without internet. Load them once from the working time table (Excel / PDF / photo) or from the schedule text copied from NTES; for today's live running open NTES below.", 13),
                _count,
                Ui.Wrap(
                    Ui.Button("Open NTES (live running)", () => NtesPage.OpenAsync(Navigation, (_train.Text ?? "").Trim())),
                    Ui.Button("Load timings (paste / Excel / PDF / photo)", () => AppShell.GoAsync<ImportPage>(), false),
                    Ui.Button("Type / correct a schedule", () => Navigation.PushAsync(new ScheduleEditorPage(Timetable.Find(D.Trains, _train.Text))), false))), Ui.Soft),
            Ui.Card(Ui.Stack(
                Ui.Heading("Schedule of a train"),
                Ui.Row(_train, Ui.Small("Show", () => { ShowTrain(); return Task.CompletedTask; }, Ui.Primary, Colors.White)),
                _schedule)),
            Ui.Card(Ui.Stack(
                Ui.Heading("Trains passing a station / KM"),
                Ui.Cols(Ui.Field("Station or KM", _place), Ui.Field("From", _from), Ui.Field("To", _to)),
                Ui.Wrap(
                    Ui.Small("Show trains", () => { ShowPassing(); return Task.CompletedTask; }, Ui.Primary, Colors.White),
                    Ui.Small("Tonight (22:00 - 07:00)", () => { _from.Text = "22:00"; _to.Text = "07:00"; ShowPassing(); return Task.CompletedTask; }),
                    Ui.Small("Next 2 hours", () =>
                    {
                        var now = DateTime.Now;
                        _from.Text = now.ToString("HH:mm", Ui.Inv);
                        _to.Text = now.AddHours(2).ToString("HH:mm", Ui.Inv);
                        ShowPassing();
                        return Task.CompletedTask;
                    })),
                _passing)));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _count.Text = D.Trains.Count == 0
            ? "No timings loaded yet."
            : $"{D.Trains.Count} trains in the timetable: " + string.Join(", ", D.Trains.Take(40).Select(t => t.TrainNo)) + (D.Trains.Count > 40 ? " ..." : "");
        ShowPassing();
    }

    private void ShowTrain()
    {
        _schedule.Children.Clear();
        var no = (_train.Text ?? "").Trim();
        if (no.Length == 0) return;
        var t = Timetable.Find(D.Trains, no);
        if (t is null)
        {
            _schedule.Children.Add(Ui.Text($"Train {no} is not in the timetable. Open NTES for its schedule / running, copy the schedule and load it here.", 13, Ui.Warn));
            return;
        }
        _schedule.Children.Add(Ui.Text(t.Title + (t.Days.Length > 0 ? $"  ({t.Days})" : ""), 15, Ui.Primary, true));
        foreach (var s in t.Stops)
        {
            var km = Patrol.KmOf(s.Code, D.Masters.Chainage);
            var name = Stations.NameOf(s.Code, D.Masters) ?? "";
            var arr = s.Arr is null ? "Starts" : Fmt.Time(s.Arr);
            var dep = s.Dep is null ? "Ends" : Fmt.Time(s.Dep);
            _schedule.Children.Add(Ui.Text($"{s.Code,-5}  {arr,-6} {dep,-6}  {name}" + (km is double k ? $"  (KM {Patrol.KmText(k)})" : ""), 13,
                km is null ? Colors.DimGray : null));
        }
        _schedule.Children.Add(Ui.Wrap(
            Ui.Small("Live running on NTES", () => NtesPage.OpenAsync(Navigation, t.TrainNo)),
            Ui.Small("Correct this schedule", () => Navigation.PushAsync(new ScheduleEditorPage(t))),
            Ui.Small("Share", () => FileService.ShareTextAsync(Timetable.ToText(t), "Train " + t.TrainNo))));
    }

    private void ShowPassing()
    {
        _passing.Children.Clear();
        if (D.Trains.Count == 0) return;
        var from = Fmt.ParseTime(_from.Text) ?? new TimeOnly(22, 0);
        var to = Fmt.ParseTime(_to.Text) ?? new TimeOnly(7, 0);
        var list = Timetable.Passing(D.Trains, _place.Text ?? "", from, to, D.Masters.Chainage);
        if (list.Count == 0) { _passing.Children.Add(Ui.Muted("No train in the timetable passes here in this time.")); return; }
        foreach (var p in list)
        {
            var pass = p;
            var row = Ui.Row(
                Ui.Text($"{Fmt.Time(p.Time)}   {p.Train.Title}   {(p.TowardsHigherKm ? "→ COR side" : "→ Kota side")}   {p.Detail}", 13, p.Stops ? Ui.Primary : null, p.Stops),
                Ui.Small("Schedule", () => { _train.Text = pass.Train.TrainNo; ShowTrain(); return Task.CompletedTask; }));
            _passing.Children.Add(row);
        }
    }
}

/// <summary>Type or correct one schedule: first line "train no name", then "CODE arrival departure" per line.</summary>
public class ScheduleEditorPage : ContentPage
{
    private readonly Editor _text = Ui.Editor("", "01910 PRYJ-PUNE SPL\nBUDI 00:25 00:27\nSNAR 00:41 00:41\n...\nCNA 02:44 02:49", 300);

    public ScheduleEditorPage(TrainSchedule? t)
    {
        Title = t is null ? "New schedule" : "Schedule " + t.TrainNo;
        if (t is not null) _text.Text = Timetable.ToText(t);
        Content = Ui.Page(
            Ui.Muted("One stop per line: station code, arrival, departure (write Source / Destination for the first / last station). Only the stations you need (e.g. Kota - COR) are enough."),
            _text,
            Ui.Wrap(
                Ui.Button("Save", Save),
                Ui.Button("Delete this train", async () =>
                {
                    if (t is null || !await Ui.Confirm("Delete", $"Remove train {t.TrainNo} from the timetable?")) return;
                    DataService.Current.Data.Trains.Remove(t);
                    await DataService.Current.SaveAsync();
                    await Navigation.PopAsync();
                }, false),
                Ui.Button("Cancel", () => Navigation.PopAsync(), false)));
    }

    private async Task Save()
    {
        var d = DataService.Current.Data;
        var list = Timetable.Parse(_text.Text ?? "", d.Masters, "Typed");
        if (list.Count == 0) { await Ui.Alert("Schedule", "Not understood: the first line needs the 5-digit train number, then at least two stops like \"BUDI 00:25 00:27\"."); return; }
        foreach (var t in list) t.ModifiedUtc = DateTime.UtcNow;
        Timetable.Merge(d.Trains, list);
        await DataService.Current.SaveAsync();
        await Navigation.PopAsync();
    }
}

/// <summary>NTES opened inside the app; the train number is copied so it can be pasted in the search box.</summary>
public class NtesPage : ContentPage
{
    public static async Task OpenAsync(INavigation nav, string trainNo)
    {
        if (trainNo.Length > 0) await Clipboard.Default.SetTextAsync(trainNo);
        await nav.PushAsync(new NtesPage(trainNo));
    }

    public NtesPage(string trainNo)
    {
        Title = "NTES";
        var web = new WebView { Source = Timetable.NtesUrl, VerticalOptions = LayoutOptions.Fill };
        var bar = Ui.Row(
            Ui.Text(trainNo.Length > 0 ? $"Train {trainNo} is copied - paste it in the NTES search box." : "National Train Enquiry System", 12, Colors.White),
            Ui.Small("Back", () => { if (web.CanGoBack) web.GoBack(); return Task.CompletedTask; }),
            Ui.Small("Open in browser", () => Launcher.Default.OpenAsync(new Uri(Timetable.NtesUrl))));
        var top = new Border { Content = bar, BackgroundColor = Ui.Primary, Padding = new Thickness(8, 4), StrokeThickness = 0 };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(top, 0, 0);
        grid.Add(web, 0, 1);
        Content = grid;
    }
}
