using System.Globalization;
using System.Text;
using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Patrolling charts (beats, rounds, season) and the running KM of the stations; positions of all patrol pairs now.</summary>
public class ChartPage : ContentPage
{
    private readonly Picker _charts = new();
    private readonly Entry _name = Ui.Entry();
    private readonly Picker _cat;
    private readonly Entry _from = Ui.Entry("", "dd.mm.yyyy");
    private readonly Entry _to = Ui.Entry("", "dd.mm.yyyy");
    private readonly Entry _rounds = Ui.Entry("", "23:00-01:20, 01:50-04:10, 04:40-07:00");
    private readonly Editor _beats = Ui.Editor("", "", 220);
    private readonly Editor _chainage = Ui.Editor("", "", 200);
    private readonly VerticalStackLayout _now = new() { Spacing = 2 };
    private readonly Entry _mastQuery = Ui.Entry("", "Mast (40/14) or chainage (40.85)");
    private readonly VerticalStackLayout _mastResult = new() { Spacing = 2 };
    private readonly Editor _mastCsv = Ui.Editor("", "Mast,Km,Metre,Chainage,MastType,Line,Drawing\n80/1,80,40.5,80.0405,B-150,,80-85 LOP", 80);
    private AppData D => DataService.Current.Data;
    private PatrolChart? _cur;

    public ChartPage()
    {
        Title = "Patrolling chart";
        _cat = Ui.Picker(D.Masters.DutyCategories);
        _charts.SelectedIndexChanged += (_, _) => Load();
        Content = Ui.Page(
            Ui.Title("Patrolling chart & section KM"),
            Ui.Card(Ui.Stack(
                Ui.Field("Chart", _charts),
                Ui.Wrap(Ui.Small("New chart (copy of this)", NewChart), Ui.Small("Delete chart", DeleteChart)),
                Ui.Field("Name", _name),
                Ui.Cols(Ui.Field("Staff", _cat), Ui.Field("Rounds", _rounds)),
                Ui.Cols(Ui.Field("Valid from", _from), Ui.Field("Valid to", _to)),
                Ui.Muted("Beats - one per line: Unit; Patrolmen; From KM; To KM; L (first round starts at the From KM end) or R (starts at the To KM end)."),
                _beats,
                Ui.Button("Save chart", SaveChart))),
            Ui.Card(Ui.Stack(Ui.Heading("Where the patrol pairs should be now"), _now, Ui.Small("Refresh", () => { ShowNow(); return Task.CompletedTask; })), Ui.Soft),
            Ui.Card(Ui.Stack(
                Ui.Heading("OHE masts"),
                Ui.Muted($"Mast register from {OheMasts.Source}: {OheMasts.Coverage}" + (D.Masters.ExtraMasts.Count > 0 ? $" + {D.Masters.ExtraMasts.Count} added" : "") +
                         ". Locations written as KM/mast (40/14, 35/10-15) are placed at the mast's exact chainage."),
                Ui.Row(_mastQuery, Ui.Small("Find", () => { FindMast(); return Task.CompletedTask; }, Ui.Primary, Colors.White)),
                _mastResult,
                Ui.Muted("Add masts of other stretches (e.g. SMPA - COR) - paste the rows of the OHE layout plan register (same columns as above) or choose a CSV file."),
                _mastCsv,
                Ui.Wrap(Ui.Small("Add pasted masts", AddMasts), Ui.Small("Choose CSV file", PickMasts)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("Running KM of stations (chainage)"),
                Ui.Muted("One per line: CODE KM  (e.g. BUDI 32.202). Used to place the train along the section."),
                _chainage,
                Ui.Button("Save station KM", SaveChainage, false))));
        _chainage.Text = string.Join("\n", D.Masters.Chainage.OrderBy(c => c.Km).Select(c => $"{c.Code} {c.Km.ToString("0.###", CultureInfo.InvariantCulture)}"));
        Fill();
    }

    private void Fill()
    {
        _charts.Items.Clear();
        foreach (var c in D.Charts) _charts.Items.Add(c.Name);
        if (_charts.Items.Count > 0) _charts.SelectedIndex = 0;
        else Load();
    }

    private void Load()
    {
        _cur = _charts.SelectedIndex >= 0 && _charts.SelectedIndex < D.Charts.Count ? D.Charts[_charts.SelectedIndex] : null;
        if (_cur is null) { _beats.Text = ""; _name.Text = ""; _now.Children.Clear(); return; }
        _name.Text = _cur.Name;
        if (!_cat.Items.Contains(_cur.Category)) _cat.Items.Add(_cur.Category);
        _cat.SelectedItem = _cur.Category;
        _from.Text = _cur.ValidFrom is DateOnly f ? Fmt.Date(f) : "";
        _to.Text = _cur.ValidTo is DateOnly t ? Fmt.Date(t) : "";
        _rounds.Text = string.Join(", ", _cur.Rounds.Select(r => $"{r.Start:HH\\:mm}-{r.End:HH\\:mm}"));
        var sb = new StringBuilder();
        foreach (var b in _cur.Beats)
            sb.AppendLine($"{b.Unit}; {b.Patrolmen}; {b.FromKm.ToString("0.###", CultureInfo.InvariantCulture)}; {b.ToKm.ToString("0.###", CultureInfo.InvariantCulture)}; {(b.StartsAtFromEnd ? "L" : "R")}");
        _beats.Text = sb.ToString().TrimEnd();
        ShowNow();
    }

    private void ShowNow()
    {
        _now.Children.Clear();
        if (_cur is null) return;
        var now = DateTime.Now;
        var nm = Patrol.NightMinutes(TimeOnly.FromDateTime(now));
        var night = DateOnly.FromDateTime(now.Hour < 12 ? now.AddDays(-1) : now);
        if (!_cur.AppliesTo(night)) _now.Children.Add(Ui.Muted("This chart is not in force tonight (see Valid from / to)."));
        if (_cur.Beats.All(b => Patrol.PositionAt(_cur, b, nm) is null)) { _now.Children.Add(Ui.Muted($"{now:HH:mm} - outside patrolling hours.")); return; }
        foreach (var b in _cur.Beats)
        {
            var p = Patrol.PositionAt(_cur, b, nm);
            if (p is double km) _now.Children.Add(Ui.Text($"{b.Patrolmen} ({b.Unit}, {b.Range}): near KM {Patrol.KmText(km)}", 13));
        }
    }

    private void FindMast()
    {
        _mastResult.Children.Clear();
        var q = (_mastQuery.Text ?? "").Trim();
        if (q.Length == 0) return;
        if (OheMasts.Find(q) is OheMast m)
            _mastResult.Children.Add(Ui.Text(m.Display, 14, Ui.Primary, true));
        else if (OheMasts.Range(q) is { } r)
            _mastResult.Children.Add(Ui.Text(r.FromRegister ? $"{q}: KM {r.From:0.000} - {r.To:0.000}" : $"{q}: not in the mast register; about KM {r.From:0.00} - {r.To:0.00} (average span)", 14));
        double? km = double.TryParse(q.Replace("KM", "", StringComparison.OrdinalIgnoreCase).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var k) ? k
            : OheMasts.Range(q)?.From;
        if (km is double x)
        {
            _mastResult.Children.Add(Ui.Muted("Nearest masts:"));
            foreach (var n in OheMasts.Near(x, 5)) _mastResult.Children.Add(Ui.Text($"{n.Display}   ({(n.Chainage - x) * 1000:+0;-0;0} m)", 13));
            var st = D.Masters.Chainage.OrderBy(c => Math.Abs(c.Km - x)).FirstOrDefault();
            if (st is not null) _mastResult.Children.Add(Ui.Muted($"Nearest station: {st.Code} (KM {st.Km:0.000}, {(x - st.Km) * 1000:+0;-0;0} m)"));
        }
        if (_mastResult.Children.Count == 0) _mastResult.Children.Add(Ui.Muted("Not found."));
    }

    private async Task SaveChart()
    {
        if (_cur is null) return;
        var beats = new List<PatrolBeat>();
        foreach (var line in Ui.Lines(_beats.Text))
        {
            var p = line.Split(';', ',', '\t').Select(x => x.Trim()).ToArray();
            if (p.Length < 4 || !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ||
                !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            {
                await Ui.Alert("Beat not understood", line);
                return;
            }
            beats.Add(new PatrolBeat { Unit = p[0], Patrolmen = p[1], FromKm = a, ToKm = b, StartsAtFromEnd = p.Length < 5 || !p[4].StartsWith("R", StringComparison.OrdinalIgnoreCase) });
        }
        var rounds = new List<PatrolRound>();
        foreach (var part in (_rounds.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var se = part.Split('-', StringSplitOptions.TrimEntries);
            if (se.Length != 2 || Fmt.ParseTime(se[0]) is not TimeOnly s || Fmt.ParseTime(se[1]) is not TimeOnly e) { await Ui.Alert("Rounds", "Write rounds as 23:00-01:20, 01:50-04:10, 04:40-07:00"); return; }
            rounds.Add(new PatrolRound { Start = s, End = e });
        }
        _cur.Name = (_name.Text ?? "").Trim();
        _cur.Category = Ui.Value(_cat);
        _cur.ValidFrom = Fmt.ParseDate(_from.Text);
        _cur.ValidTo = Fmt.ParseDate(_to.Text);
        _cur.Rounds = rounds;
        _cur.Beats = beats;
        await DataService.Current.SaveAsync();
        var idx = _charts.SelectedIndex;
        Fill();
        _charts.SelectedIndex = idx;
        await Ui.Alert("Saved", $"{beats.Count} beats saved.");
    }

    private async Task NewChart()
    {
        var name = await Ui.Prompt("New chart", "Name (e.g. Monsoon Patrolling Chart 2027 (Kota - COR))");
        if (string.IsNullOrWhiteSpace(name)) return;
        var json = System.Text.Json.JsonSerializer.Serialize(_cur ?? Patrol.WinterChartKotaCor2026(), FirJsonContext.Default.PatrolChart);
        var copy = System.Text.Json.JsonSerializer.Deserialize(json, FirJsonContext.Default.PatrolChart)!;
        copy.Id = Guid.NewGuid();
        copy.Name = name.Trim();
        copy.ValidFrom = copy.ValidTo = null;
        D.Charts.Add(copy);
        await DataService.Current.SaveAsync();
        Fill();
        _charts.SelectedIndex = D.Charts.Count - 1;
    }

    private async Task DeleteChart()
    {
        if (_cur is null || !await Ui.Confirm("Delete", $"Delete {_cur.Name}?")) return;
        D.Charts.Remove(_cur);
        await DataService.Current.SaveAsync();
        Fill();
    }

    private void FindMast()
    {
        _mastResult.Children.Clear();
        var q = (_mastQuery.Text ?? "").Trim();
        if (q.Length == 0) return;
        if (OheMasts.Find(q) is OheMast m)
        {
            _mastResult.Children.Add(Ui.Text($"OHE {m.Mast}: chainage {m.Chainage:0.000} km" + (m.Type.Length > 0 ? $", mast type {m.Type}" : "") +
                                             (m.Drawing.Length > 0 ? $", LOP {m.Drawing}" : ""), 13, Ui.Primary, true));
            ShowNear(m.Chainage);
            return;
        }
        if (double.TryParse(q, NumberStyles.Float, CultureInfo.InvariantCulture, out var ch)) { ShowNear(ch); return; }
        _mastResult.Children.Add(Ui.Text("Not in the register.", 13, Ui.Warn));
    }

    private void ShowNear(double ch)
    {
        var near = OheMasts.All.OrderBy(x => Math.Abs(x.Chainage - ch)).Take(5).OrderBy(x => x.Chainage);
        _mastResult.Children.Add(Ui.Muted("Nearby: " + string.Join(",  ", near.Select(x => $"{x.Mast} ({x.Chainage:0.000})"))));
        var st = D.Masters.Chainage.OrderBy(x => Math.Abs(x.Km - ch)).FirstOrDefault();
        if (st is not null) _mastResult.Children.Add(Ui.Muted($"Nearest station {st.Code} ({Stations.NameOf(st.Code, D.Masters)}) at KM {st.Km:0.000}, {Math.Abs(st.Km - ch):0.00} km away."));
    }

    private async Task AddMasts()
    {
        var list = OheMasts.Parse(_mastCsv.Text ?? "");
        if (list.Count == 0) { await Ui.Alert("OHE masts", "No mast row understood. Columns: Mast, Km, Metre, Chainage, MastType, Line, Drawing."); return; }
        var extra = D.Masters.ExtraMasts;
        foreach (var m in list)
        {
            extra.RemoveAll(x => OheMasts.Normal(x.Mast) == m.Mast);
            extra.Add(m);
        }
        OheMasts.SetExtra(extra);
        await DataService.Current.SaveAsync();
        _mastCsv.Text = "";
        await Ui.Alert("OHE masts", $"{list.Count} masts added. The register now has {OheMasts.Count} masts.");
    }

    private async Task PickMasts()
    {
        var f = await FileService.PickAsync("OHE mast register (CSV)", new[] { ".csv", ".txt" }, new[] { "text/csv", "text/plain", "text/comma-separated-values" });
        if (f is null) return;
        var path = await FileService.ToCacheAsync(f);
        _mastCsv.Text = await File.ReadAllTextAsync(path);
        await AddMasts();
    }

    private async Task SaveChainage()
    {
        var list = new List<StationKm>();
        foreach (var line in Ui.Lines(_chainage.Text))
        {
            var p = line.Split(new[] { ' ', '\t', ';', ',', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2 || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var km)) { await Ui.Alert("Not understood", line); return; }
            list.Add(new StationKm { Code = p[0].ToUpperInvariant(), Km = km });
        }
        D.Masters.Chainage = list;
        await DataService.Current.SaveAsync();
        await Ui.Alert("Saved", $"{list.Count} stations saved.");
    }
}
