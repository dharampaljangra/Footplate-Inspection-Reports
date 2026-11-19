using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Where the train should have met each patrol pair as per the patrolling chart, against what was recorded.</summary>
public class ChartCheckPage : ContentPage
{
    public ChartCheckPage(InspectionReport r, Action<List<string>> addShortcomings)
    {
        Title = "Patrolling chart check";
        var data = DataService.Current.Data;
        var night = Patrol.NightOf(r.Date, r.Departure);
        var charts = data.Charts.Where(c => c.Beats.Count > 0).ToList();
        var chart = charts.FirstOrDefault(c => c.AppliesTo(night)) ?? charts.FirstOrDefault();
        var stack = Ui.Stack(Ui.Title("Patrolling chart check"));
        if (chart is null)
        {
            stack.Children.Add(Ui.Text("No patrolling chart is saved. See 'Patrolling chart & section KM'."));
            Content = Ui.Page(stack);
            return;
        }
        if (!chart.AppliesTo(night))
            stack.Children.Add(Ui.Card(Ui.Text($"Note: {chart.Name} is for {Fmt.Date(chart.ValidFrom ?? DateOnly.MinValue)} to {Fmt.Date(chart.ValidTo ?? DateOnly.MaxValue)}; this report is of the night of {Patrol.NightText(night)}. Shown for reference only.", 13, Ui.Warn), Ui.WarnBack));
        var rows = Patrol.Check(r, chart, data.Masters.Chainage, data.Rosters, data.Settings.ChartToleranceKm, data.Trains);
        if (rows.Count == 0)
        {
            stack.Children.Add(Ui.Text("No meeting with a patrol pair: check the stations (they must be on the section KM list) and the departure / arrival times, or the train ran outside the patrolling hours."));
            Content = Ui.Page(stack);
            return;
        }
        int ok = rows.Count(x => x.Status == Patrol.AsPerChart), off = rows.Count(x => x.Status == Patrol.OffPosition), miss = rows.Count(x => x.Status == Patrol.NotNoticed);
        stack.Children.Add(Ui.Card(Ui.Stack(
            Ui.Text($"{chart.Name}", 14, Ui.Primary, true),
            Ui.Text($"{r.FromStation} {Fmt.Time(r.Departure)} → {r.ToStation} {Fmt.Time(r.Arrival)}: {rows.Count(x => x.ExpectedKm is not null)} patrol pairs should have been met. " +
                    $"{ok} as per chart, {off} off position, {miss} not recorded in the report.", 13),
            Ui.Muted(Patrol.UsesSchedule(r, data.Masters.Chainage, data.Trains)
                ? $"Meeting points follow the schedule of train {r.TrainNo} (halts included), fitted to the actual departure and arrival."
                : $"Meeting points assume an even speed between departure and arrival (train {r.TrainNo} is not in the timetable); a difference of about a kilometre is normal.")), Ui.Soft));
        foreach (var x in rows)
        {
            var color = x.Status switch { Patrol.AsPerChart => Ui.Ok, Patrol.OffPosition => Ui.Warn, _ => Colors.DimGray };
            var s = Ui.Stack(
                Ui.Text($"{x.Beat.Patrolmen}   {x.Beat.Range}   ({x.Beat.Unit})", 14, Ui.Primary, true),
                Ui.Text(x.Status, 13, color, true),
                Ui.Text(x.Detail, 13));
            foreach (var p in x.OnDuty) s.Children.Add(DutyPage.PersonRow(p, ""));
            s.Spacing = 2;
            stack.Children.Add(Ui.Card(s, x.Status is Patrol.OffPosition || x.Seen?.IsDeficient == true ? Ui.WarnBack : null));
        }
        stack.Children.Add(Ui.Wrap(
            Ui.Button("Add deviations to shortcomings", async () =>
            {
                var offRows = rows.Where(x => x.Status == Patrol.OffPosition).ToList();
                var missRows = rows.Where(x => x.Status == Patrol.NotNoticed).ToList();
                var pick = await Ui.Choose("Add to shortcomings", $"Off position only ({offRows.Count})", $"Off position and not met ({offRows.Count + missRows.Count})");
                if (pick is null) return;
                var lines = Patrol.Shortcomings(pick.StartsWith("Off position only") ? offRows : offRows.Concat(missRows));
                if (lines.Count == 0) { await Ui.Alert("Chart check", "No deviation to add."); return; }
                addShortcomings(lines);
                await Navigation.PopAsync();
            }),
            Ui.Button("Back", () => Navigation.PopAsync(), false)));
        Content = Ui.Page(stack);
    }
}
