using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Entry / editing of one footplate inspection report, in the order of the inspection note.</summary>
public class ReportEditorPage : ContentPage
{
    private readonly InspectionReport _r;
    private readonly bool _isNew;
    private AppData D => DataService.Current.Data;

    // Particulars
    private readonly DatePicker _date = new() { Format = "dd.MM.yyyy" };
    private readonly Picker _kind;
    private readonly Dictionary<string, CheckBox> _purposes = new();
    private readonly Entry _otherPurpose = Ui.Entry("", "Other purpose");
    private readonly Entry _from = Ui.Entry("", "e.g. BUDI");
    private readonly Entry _to = Ui.Entry("", "e.g. CNA");
    private readonly Entry _train = Ui.Entry("", "e.g. 01910", Keyboard.Numeric);
    private readonly Entry _trainName = Ui.Entry("", "optional");
    private readonly Entry _dep = Ui.Entry("", "0027", Keyboard.Numeric);
    private readonly Entry _arr = Ui.Entry("", "0244", Keyboard.Numeric);
    private readonly Label _duration = Ui.Muted("");
    private readonly Entry _section = Ui.Entry();
    private readonly Entry _name = Ui.Entry();
    private readonly Entry _desig = Ui.Entry();
    private readonly Entry _mobile = Ui.Entry("", "", Keyboard.Telephone);

    // Loco and crew
    private readonly Entry _locoNo = Ui.Entry("", "e.g. 22228", Keyboard.Numeric);
    private readonly Entry _locoType = Ui.Entry("", "WAP4");
    private readonly Picker _cab = Ui.Picker(new[] { "", "Cab1", "Cab2" });
    private readonly Entry _shed = Ui.Entry("", "e.g. TKD");
    private readonly Entry _lp = Ui.Entry("", "Loco Pilot");
    private readonly Entry _lpHq = Ui.Entry("", "HQ");
    private readonly Picker _alpDesig = Ui.Picker(new[] { "Sr. ALP", "ALP" });
    private readonly Entry _alp = Ui.Entry("", "Assistant Loco Pilot");
    private readonly Entry _alpHq = Ui.Entry("", "HQ");
    private readonly Picker _tmDesig = Ui.Picker(new[] { "TM", "Sr. TM", "Guard" });
    private readonly Entry _tm = Ui.Entry("", "Train Manager");
    private readonly Entry _tmHq = Ui.Entry("", "HQ");

    // Observations
    private readonly Editor _staffNote = Ui.Editor("", "En-route Gate Keepers, Stationary Watchmen and Monsoon Patrolmen were checked as under:", 50);
    private readonly VerticalStackLayout _sightings = new() { Spacing = 4 };
    private readonly Dictionary<string, CheckBox> _std = new();
    private readonly Editor _otherObs = Ui.Editor("", "Other observations - one per line");
    private readonly Entry _visibility = Ui.Entry();
    private readonly Entry _weather = Ui.Entry("", "e.g. Light rain from BUDI to GQL'W'");
    private readonly Editor _shortcomings = Ui.Editor("", "One shortcoming per line, e.g. GKs at LC 66 & 29 not alert.");
    private readonly Editor _locations = Ui.Editor("", "KM 35/10-15, 36/05-06, 61/12(SEJ), JNRI S3 to S4 ...");
    private readonly Editor _remarks = Ui.Editor("", "Remarks (optional)", 60);
    private readonly Editor _action = Ui.Editor("", "Action taken / advised (optional)", 50);

    public ReportEditorPage(InspectionReport report, bool isNew = false)
    {
        _isNew = isNew;
        _r = isNew ? report : report.Clone();
        Title = isNew ? "New footplate report" : $"{Fmt.Date(report.Date)} {report.FromStation}-{report.ToStation}";
        _kind = Ui.Picker(D.Masters.Kinds, _r.Kind);

        var purposeFlow = Ui.Flow();
        foreach (var p in D.Masters.Purposes.Concat(_r.Purposes).Distinct())
        {
            var cb = new CheckBox { IsChecked = _r.Purposes.Contains(p), Color = Ui.Primary };
            _purposes[p] = cb;
            purposeFlow.Children.Add(Ui.Check(cb, p));
        }

        var stdStack = Ui.Stack();
        foreach (var o in D.Masters.StandardObservations)
        {
            var cb = new CheckBox { IsChecked = _r.Observations.Contains(o), Color = Ui.Primary, VerticalOptions = LayoutOptions.Start };
            _std[o] = cb;
            var l = Ui.Text(o, 13);
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => cb.IsChecked = !cb.IsChecked;
            l.GestureRecognizers.Add(tap);
            stdStack.Children.Add(Ui.Row(cb, l));
        }

        Fill();
        _train.Unfocused += async (_, _) => await FillFromTimetable(force: false);
        _dep.TextChanged += (_, _) => ShowDuration();
        _arr.TextChanged += (_, _) => ShowDuration();
        _from.TextChanged += (_, _) => ShowDuration();
        _to.TextChanged += (_, _) => ShowDuration();

        var m = D.Masters;
        Content = Ui.Page(
            Ui.Card(Ui.Stack(
                Ui.Heading("1. Inspection particulars"),
                Ui.Cols(Ui.Field("Date of inspection", _date), Ui.Field("Inspection", _kind)),
                Ui.Field("Purpose", Ui.Stack(purposeFlow, _otherPurpose)),
                Ui.Cols(Ui.Field("From station (code)", _from), Ui.Field("To station (code)", _to)),
                Ui.Cols(Ui.Field("Train No.", _train), Ui.Field("Train name", _trainName)),
                Ui.Cols(Ui.Field("Departure (hhmm)", _dep), Ui.Field("Arrival (hhmm)", _arr)),
                _duration,
                Ui.Wrap(
                    Ui.Small("Times from timetable", () => FillFromTimetable(force: true)),
                    Ui.Small("NTES (live running)", () => NtesPage.OpenAsync(Navigation, (_train.Text ?? "").Trim())),
                    Ui.Small("Trains passing (timetable)", () => Navigation.PushAsync(new TimetablePage()))),
                Ui.Cols(Ui.Field("Inspected by", _name), Ui.Field("Designation", _desig)),
                Ui.Cols(Ui.Field("Mobile", _mobile), Ui.Field("Section", _section)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("2. Loco and crew on board"),
                Ui.Cols(Ui.Field("Loco No.", _locoNo), Ui.Field("Loco type", Ui.Suggest(_locoType, () => m.LocoTypes, "Loco type"))),
                Ui.Cols(Ui.Field("Cab", _cab), Ui.Field("Shed", Ui.Suggest(_shed, () => m.Sheds, "Shed"))),
                Ui.Field("Loco Pilot (LP)", Ui.Row(Ui.Suggest(_lp, () => m.LocoPilots, "Loco Pilot"), _lpHq)),
                Ui.Field("Assistant Loco Pilot", Ui.Row(_alpDesig, Ui.Suggest(_alp, () => m.AssistantLocoPilots, "ALP"), _alpHq)),
                Ui.Field("Train Manager", Ui.Row(_tmDesig, Ui.Suggest(_tm, () => m.TrainManagers, "Train Manager"), _tmHq)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("3. Field staff seen en-route"),
                Ui.Field("Lead-in sentence", _staffNote),
                _sightings,
                Ui.Wrap(
                    Ui.Small("+ Add field staff", AddSighting),
                    Ui.Small("Patrolling chart check", ChartCheck),
                    Ui.Small("Deficient staff → shortcomings", DeficientToShortcomings)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("4. Observations"),
                Ui.Muted("Tick the standard observations that apply (edit the list under Profile, lists & settings)."),
                stdStack,
                Ui.Field("Other observations", _otherObs),
                Ui.Cols(Ui.Field("Visibility", Ui.Suggest(_visibility, () => m.Visibility, "Visibility")), Ui.Field("Weather", _weather)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("5. Shortcomings / deficiencies"),
                _shortcomings,
                Ui.Heading("6. Locations to be attended for better running"),
                Ui.Muted("Separate by commas or new lines. Write the nature in brackets: 61/12(SEJ), 63/03(Low joint). Compliance is recorded under 'Locations to be attended'."),
                _locations,
                Ui.Heading("7. Remarks"),
                _remarks,
                Ui.Small("Suggest remarks", SuggestRemarks),
                Ui.Field("Action taken / advised", _action))),
            Ui.Wrap(
                Ui.Button("Save", Save),
                Ui.Button("Export / print (PDF, Word, Excel ...)", ExportReport, false),
                Ui.Button("Share as WhatsApp text", ShareText, false),
                Ui.Button("Copy text", CopyText, false),
                Ui.Button("Duplicate for return journey", Duplicate, false),
                Ui.Button("Delete", Delete, false)));
        RefreshSightings();
        ShowDuration();
    }

    private void Fill()
    {
        _date.Date = _r.Date.ToDateTime(TimeOnly.MinValue);
        _from.Text = _r.FromStation;
        _to.Text = _r.ToStation;
        _train.Text = _r.TrainNo;
        _trainName.Text = _r.TrainName;
        _dep.Text = _r.Departure is TimeOnly d ? d.ToString("HHmm", Ui.Inv) : "";
        _arr.Text = _r.Arrival is TimeOnly a ? a.ToString("HHmm", Ui.Inv) : "";
        _section.Text = _r.Section;
        _name.Text = _r.InspectorName;
        _desig.Text = _r.Designation;
        _mobile.Text = _r.Mobile;
        _locoNo.Text = _r.LocoNo;
        _locoType.Text = _r.LocoType;
        if (!_cab.Items.Contains(_r.Cab)) _cab.Items.Add(_r.Cab);
        _cab.SelectedItem = _r.Cab;
        _shed.Text = _r.Shed;
        _lp.Text = _r.LocoPilot;
        _lpHq.Text = _r.LocoPilotHq;
        _alpDesig.SelectedItem = _r.AlpDesignation == "ALP" ? "ALP" : "Sr. ALP";
        _alp.Text = _r.AssistantLocoPilot;
        _alpHq.Text = _r.AlpHq;
        _tmDesig.SelectedItem = _tmDesig.Items.Contains(_r.TmDesignation) ? _r.TmDesignation : "TM";
        _tm.Text = _r.TrainManager;
        _tmHq.Text = _r.TmHq;
        _staffNote.Text = _r.FieldStaffNote;
        _otherObs.Text = string.Join("\n", _r.Observations.Where(o => !D.Masters.StandardObservations.Contains(o)));
        _visibility.Text = _r.Visibility;
        _weather.Text = _r.Weather;
        _shortcomings.Text = string.Join("\n", _r.Shortcomings);
        _locations.Text = string.Join(", ", _r.Locations.Select(l => l.Location + (l.Feature.Length > 0 ? $"({l.Feature})" : "")));
        _remarks.Text = _r.Remarks;
        _action.Text = _r.ActionTaken;
        _otherPurpose.Text = string.Join(", ", _r.Purposes.Where(p => !_purposes.ContainsKey(p)));
    }

    /// <summary>Copies the screen into the report.</summary>
    private void Collect()
    {
        _r.Date = DateOnly.FromDateTime(Ui.DateOf(_date));
        _r.Kind = Ui.Value(_kind);
        _r.Purposes = _purposes.Where(p => p.Value.IsChecked).Select(p => p.Key)
            .Concat((_otherPurpose.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct().ToList();
        _r.FromStation = (_from.Text ?? "").Trim().ToUpperInvariant();
        _r.ToStation = (_to.Text ?? "").Trim().ToUpperInvariant();
        _r.TrainNo = (_train.Text ?? "").Trim();
        _r.TrainName = (_trainName.Text ?? "").Trim();
        _r.Departure = Fmt.ParseTime(_dep.Text);
        _r.Arrival = Fmt.ParseTime(_arr.Text);
        _r.Section = (_section.Text ?? "").Trim();
        _r.InspectorName = (_name.Text ?? "").Trim();
        _r.Designation = (_desig.Text ?? "").Trim();
        _r.Mobile = (_mobile.Text ?? "").Trim();
        _r.LocoNo = (_locoNo.Text ?? "").Trim();
        _r.LocoType = (_locoType.Text ?? "").Trim().ToUpperInvariant().Replace("-", "");
        _r.Cab = Ui.Value(_cab);
        _r.Shed = (_shed.Text ?? "").Trim().ToUpperInvariant();
        _r.LocoPilot = Fmt.CleanName(_lp.Text);
        _r.LocoPilotHq = (_lpHq.Text ?? "").Trim();
        _r.AlpDesignation = Ui.Value(_alpDesig);
        _r.AssistantLocoPilot = Fmt.CleanName(_alp.Text);
        _r.AlpHq = (_alpHq.Text ?? "").Trim();
        _r.TmDesignation = Ui.Value(_tmDesig);
        _r.TrainManager = Fmt.CleanName(_tm.Text);
        _r.TmHq = (_tmHq.Text ?? "").Trim();
        _r.FieldStaffNote = (_staffNote.Text ?? "").Trim();
        _r.Observations = _std.Where(s => s.Value.IsChecked).Select(s => s.Key).Concat(Ui.Lines(_otherObs.Text)).ToList();
        _r.Visibility = (_visibility.Text ?? "").Trim();
        _r.Weather = (_weather.Text ?? "").Trim();
        _r.Shortcomings = Ui.Lines(_shortcomings.Text).Select(s => Fmt.EndWith(System.Text.RegularExpressions.Regex.Replace(s, @"^\d{1,2}\s*[.)]\s*", ""), '.')).ToList();
        // Keep compliance already recorded for locations written the same way.
        var old = _r.Locations;
        _r.Locations = Locations.ParseList(_locations.Text ?? "").Select(l =>
        {
            var same = old.FirstOrDefault(o => o.Location == l.Location && o.Feature == l.Feature);
            return same ?? l;
        }).ToList();
        _r.Remarks = (_remarks.Text ?? "").Trim();
        _r.ActionTaken = (_action.Text ?? "").Trim();
    }

    private void ShowDuration()
    {
        var d = Fmt.ParseTime(_dep.Text);
        var a = Fmt.ParseTime(_arr.Text);
        var t = new InspectionReport { Departure = d, Arrival = a, FromStation = (_from.Text ?? "").Trim().ToUpperInvariant(), ToStation = (_to.Text ?? "").Trim().ToUpperInvariant() };
        var km = ReportLayout.TrainKm(t, D.Masters.Chainage);
        var names = $"{Stations.NameOf(t.FromStation, D.Masters) ?? ""} → {Stations.NameOf(t.ToStation, D.Masters) ?? ""}".Trim(' ', '→');
        _duration.Text = (t.Duration is TimeSpan ts ? $"On the footplate {Fmt.Duration(ts)}" : "") +
                         (km is double k ? $"   •   about {k:0.0} km" : "") + (names.Length > 0 ? $"   •   {names}" : "");
    }

    /// <summary>Scheduled departure / arrival of the train (and its name) from the timetable kept in the app.</summary>
    private async Task FillFromTimetable(bool force)
    {
        var t = Timetable.Find(D.Trains, _train.Text);
        if (t is null)
        {
            if (force) await Ui.Alert("Timetable", $"Train {(_train.Text ?? "").Trim()} is not in the timetable. Load the timings under 'Train timings', or look it up on NTES.");
            return;
        }
        var from = (_from.Text ?? "").Trim().ToUpperInvariant();
        var to = (_to.Text ?? "").Trim().ToUpperInvariant();
        if ((_trainName.Text ?? "").Length == 0 && t.Name.Length > 0) _trainName.Text = t.Name;
        if (from.Length == 0 || to.Length == 0)
        {
            if (force) await Ui.Alert("Timetable", "Enter the From and To stations first.");
            return;
        }
        var (dep, arr) = Timetable.TimesFor(t, from, to);
        if (force || (_dep.Text ?? "").Length == 0) if (dep is TimeOnly d) _dep.Text = d.ToString("HHmm", Ui.Inv);
        if (force || (_arr.Text ?? "").Length == 0) if (arr is TimeOnly a) _arr.Text = a.ToString("HHmm", Ui.Inv);
        if (force && (dep is null || arr is null))
            await Ui.Alert("Timetable", $"The schedule of {t.TrainNo} does not have {(dep is null ? from : to)}. Correct it under 'Train timings'.");
        else if (force)
            await Ui.Alert("Timetable", "Scheduled times filled in - change them to the actual times of your run.");
    }

    private void RefreshSightings()
    {
        _sightings.Children.Clear();
        if (_r.Sightings.Count == 0) _sightings.Children.Add(Ui.Muted("No field staff recorded yet."));
        for (int i = 0; i < _r.Sightings.Count; i++)
        {
            var s = _r.Sightings[i];
            var label = Ui.Text($"{Fmt.Roman(i + 1)}. {ReportText.SightingText(s)}", 13, s.IsDeficient ? Ui.Warn : null);
            var edit = Ui.Small("Edit", async () =>
            {
                Collect();
                var page = new SightingEditorPage(s, _r);
                await Navigation.PushModalAsync(new NavigationPage(page));
                var result = await page.Result;
                if (result == SightingEditorPage.Outcome.Deleted) _r.Sightings.Remove(s);
                RefreshSightings();
            });
            _sightings.Children.Add(Ui.Row(label, edit));
        }
    }

    private async Task AddSighting()
    {
        Collect();
        var s = new StaffSighting { Category = _r.Sightings.LastOrDefault()?.Category ?? "Monsoon Patrolman" };
        var page = new SightingEditorPage(s, _r);
        await Navigation.PushModalAsync(new NavigationPage(page));
        if (await page.Result == SightingEditorPage.Outcome.Saved)
        {
            _r.Sightings.Add(s);
            if (_staffNote.Text is null or "") _staffNote.Text = Defaults.FieldStaffNote;
        }
        RefreshSightings();
    }

    private async Task DeficientToShortcomings()
    {
        Collect();
        var lines = ReportText.DeficientStaff(_r).Where(l => !_r.Shortcomings.Contains(l)).ToList();
        if (lines.Count == 0) { await Ui.Alert("Shortcomings", "No field staff is marked not alert / sleeping / absent / without jacket."); return; }
        _shortcomings.Text = string.Join("\n", _r.Shortcomings.Concat(lines));
    }

    private async Task ChartCheck()
    {
        Collect();
        await Navigation.PushAsync(new ChartCheckPage(_r, lines =>
        {
            Collect();
            _shortcomings.Text = string.Join("\n", _r.Shortcomings.Concat(lines.Where(l => !_r.Shortcomings.Contains(l))));
        }));
    }

    private Task SuggestRemarks()
    {
        Collect();
        _remarks.Text = ReportText.SuggestRemarks(_r);
        return Task.CompletedTask;
    }

    private async Task<bool> SaveCore(bool askWarnings = true)
    {
        Collect();
        if (askWarnings)
        {
            var w = ReportText.Check(_r);
            if (w.Count > 0 && !await Ui.Confirm("Please check", string.Join("\n", w.Select(x => "• " + x)) + "\n\nSave anyway?", "Save", "Go back"))
                return false;
        }
        await DataService.Current.SaveReportAsync(_r);
        return true;
    }

    private async Task Save()
    {
        if (await SaveCore()) await Navigation.PopAsync();
    }

    private async Task ExportReport()
    {
        if (!await SaveCore(askWarnings: false)) return;
        await ExportService.ExportAsync("Export inspection note", Path.GetFileNameWithoutExtension(Export.FileName(_r, ExportFormat.Pdf)),
            f => Export.Report(_r, D, f));
    }

    private async Task ShareText()
    {
        Collect();
        await FileService.ShareTextAsync(ReportText.ToMessage(_r, D.Profile), "Footplate inspection report");
    }

    private async Task CopyText()
    {
        Collect();
        await Clipboard.Default.SetTextAsync(ReportText.ToMessage(_r, D.Profile));
        await Ui.Alert("Copied", "The report is copied in the WhatsApp format - paste it in the group.");
    }

    private async Task Duplicate()
    {
        if (!await SaveCore(askWarnings: false)) return;
        var copy = _r.Clone();
        copy.Id = Guid.NewGuid();
        copy.CreatedUtc = copy.ModifiedUtc = DateTime.UtcNow;
        (copy.FromStation, copy.ToStation) = (_r.ToStation, _r.FromStation);
        copy.TrainNo = copy.TrainName = "";
        copy.Departure = copy.Arrival = null;
        copy.LocoNo = copy.LocoType = copy.Cab = copy.Shed = "";
        copy.LocoPilot = copy.LocoPilotHq = copy.AssistantLocoPilot = copy.AlpHq = copy.TrainManager = copy.TmHq = "";
        copy.Sightings.Clear();
        copy.Shortcomings.Clear();
        copy.Locations.Clear();
        copy.Remarks = copy.ActionTaken = copy.Weather = "";
        copy.Source = "Typed";
        await Navigation.PushAsync(new ReportEditorPage(copy, isNew: true));
    }

    private async Task Delete()
    {
        if (_isNew) { await Navigation.PopAsync(); return; }
        if (!await Ui.Confirm("Delete", "Delete this report?")) return;
        var existing = D.Reports.FirstOrDefault(x => x.Id == _r.Id);
        if (existing is not null) Store.Delete(D, existing);
        await DataService.Current.SaveAsync();
        await Navigation.PopAsync();
    }
}
