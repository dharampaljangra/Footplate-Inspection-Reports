using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>One gate keeper / watchman / patrolman seen from the cab; shows who is on duty there with call buttons.</summary>
public class SightingEditorPage : ContentPage
{
    public enum Outcome { Cancelled, Saved, Deleted }

    private readonly StaffSighting _s;
    private readonly InspectionReport _r;
    private readonly TaskCompletionSource<Outcome> _tcs = new();
    public Task<Outcome> Result => _tcs.Task;

    private readonly Picker _cat;
    private readonly Entry _loc = Ui.Entry("", "KM 48/17, LC 66 or JNRI");
    private readonly Entry _count = Ui.Entry("1", "", Keyboard.Numeric);
    private readonly Picker _cond;
    private readonly Picker _jacket;
    private readonly Entry _remarks = Ui.Entry("", "optional");
    private readonly VerticalStackLayout _duty = new() { Spacing = 4 };

    public SightingEditorPage(StaffSighting s, InspectionReport r)
    {
        _s = s;
        _r = r;
        var m = DataService.Current.Data.Masters;
        Title = "Field staff";
        _cat = Ui.Picker(m.SightingCategories, s.Category);
        _cond = Ui.Picker(new[] { "" }.Concat(m.Conditions), s.Condition);
        _jacket = Ui.Picker(m.JacketOptions, s.Jacket);
        _loc.Text = s.Location;
        _count.Text = s.Count.ToString(Ui.Inv);
        _remarks.Text = s.Remarks;
        _loc.Unfocused += (_, _) => ShowDuty();
        Content = Ui.Page(
            Ui.Card(Ui.Stack(
                Ui.Field("Staff", _cat),
                Ui.Cols(Ui.Field("Location (KM 48/17, LC 66, station code)", _loc), Ui.Field("Nos.", _count)),
                Ui.Cols(Ui.Field("Found", _cond), Ui.Field("Reflective jacket", _jacket)),
                Ui.Field("Remarks", _remarks))),
            Ui.Card(Ui.Stack(Ui.Heading("On duty here"), _duty, Ui.Small("Check again", () => { ShowDuty(); return Task.CompletedTask; })), Ui.Soft),
            Ui.Wrap(
                Ui.Button("Save", Save),
                Ui.Button("Cancel", () => Close(Outcome.Cancelled), false),
                Ui.Button("Delete", () => Close(Outcome.Deleted), false)));
        ShowDuty();
    }

    private void ShowDuty()
    {
        _duty.Children.Clear();
        var data = DataService.Current.Data;
        var km = Patrol.KmOf(_loc.Text ?? "", data.Masters.Chainage);
        if (km is null) { _duty.Children.Add(Ui.Muted("Enter a KM (e.g. 48/17) or station code to see the patrolmen / keymen on duty and call them.")); return; }
        if (OheMasts.Find(_loc.Text ?? "") is OheMast mast) _duty.Children.Add(Ui.Muted(mast.Display));
        if (OheMasts.Range(_loc.Text ?? "") is { FromRegister: true } mastRange && Math.Abs(mastRange.To - mastRange.From) > 0.0005)
            _duty.Children.Add(Ui.Muted(OheMasts.Describe(_loc.Text ?? "")));
        var night = Patrol.NightOf(_r.Date, _r.Departure);
        var list = Patrol.OnDutyAt(data.Rosters, km.Value, night, _r.Date);
        if (list.Count == 0) _duty.Children.Add(Ui.Muted($"No duty list for the night of {Patrol.NightText(night)} covers KM {Patrol.KmText(km.Value)}. Import tonight's list under 'Patrolmen & keymen on duty'."));
        foreach (var (roster, beat) in list)
        {
            _duty.Children.Add(Ui.Text($"{roster.Category} - beat {beat.No} ({beat.Range})", 13, Ui.Primary, true));
            foreach (var p in beat.Persons) _duty.Children.Add(DutyPage.PersonRow(p, $"Footplate inspection {Fmt.Date(_r.Date)} by {_r.TrainNo}"));
        }
        // Where the chart places the patrol pair when the train passes.
        var chart = data.Charts.FirstOrDefault(c => c.AppliesTo(night));
        var beatOnChart = chart?.Beats.FirstOrDefault(b => b.Contains(km.Value));
        var time = Patrol.TimeAtKm(_r, data.Masters.Chainage, km.Value, data.Trains);
        if (chart is not null && beatOnChart is not null && time is TimeOnly t)
        {
            var pos = Patrol.PositionAt(chart, beatOnChart, Patrol.NightMinutes(t));
            _duty.Children.Add(Ui.Muted(pos is double p
                ? $"Train passed here about {Fmt.Time(t)} hrs; as per {chart.Name} {beatOnChart.Patrolmen} ({beatOnChart.Range}) should then be near KM {Patrol.KmText(p)}."
                : $"Train passed here about {Fmt.Time(t)} hrs - outside the patrolling hours of {chart.Name}."));
        }
    }

    private async Task Save()
    {
        var loc = (_loc.Text ?? "").Trim();
        loc = System.Text.RegularExpressions.Regex.Replace(loc, @"^KM\.?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (loc.Length == 0) { await Ui.Alert("Location", "Enter the location (KM, LC or station)."); return; }
        _s.Category = Ui.Value(_cat);
        _s.Location = loc;
        _s.Count = int.TryParse(_count.Text, out var n) && n > 0 ? n : 1;
        _s.Condition = Ui.Value(_cond);
        _s.Jacket = Ui.Value(_jacket);
        _s.Remarks = (_remarks.Text ?? "").Trim();
        await Close(Outcome.Saved);
    }

    private async Task Close(Outcome o)
    {
        if (o == Outcome.Deleted && !await Ui.Confirm("Delete", "Remove this entry?")) return;
        _tcs.TrySetResult(o);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(Outcome.Cancelled);
        return base.OnBackButtonPressed();
    }
}
