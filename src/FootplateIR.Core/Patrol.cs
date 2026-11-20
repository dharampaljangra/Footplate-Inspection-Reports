using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

// ============================================================================================
//  Patrolling charts (time - distance diagrams), night duty rosters with phone numbers,
//  and the check of field staff seen from the footplate against the chart.
// ============================================================================================

/// <summary>Running KM (chainage) of a station on the section, as printed on the patrolling chart.</summary>
public sealed class StationKm
{
    public string Code { get; set; } = "";
    public double Km { get; set; }
}

public sealed class PatrolRound
{
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

/// <summary>One beat of the chart: a pair of patrolmen walking between two KMs, turning back every round.</summary>
public sealed class PatrolBeat
{
    /// <summary>Section engineer unit the beat belongs to (BUDI, MLGH ...).</summary>
    public string Unit { get; set; } = "";
    public int Group { get; set; }
    /// <summary>"P-1&amp;2"</summary>
    public string Patrolmen { get; set; } = "";
    public double FromKm { get; set; }
    public double ToKm { get; set; }
    /// <summary>True when the first round starts at <see cref="FromKm"/> (walking towards higher KM).</summary>
    public bool StartsAtFromEnd { get; set; } = true;

    [JsonIgnore] public string Range => $"KM {Patrol.KmText(FromKm)} - {Patrol.KmText(ToKm)}";
    public bool Contains(double km) => km >= Math.Min(FromKm, ToKm) - 1e-6 && km <= Math.Max(FromKm, ToKm) + 1e-6;
}

public sealed class PatrolChart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Section { get; set; } = "";
    /// <summary>Patrolling season (blank = always).</summary>
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    /// <summary>Category of staff the chart is for (Cold Weather Patrolman, Monsoon Patrolman ...).</summary>
    public string Category { get; set; } = "Cold Weather Patrolman";
    public string Reference { get; set; } = "";
    public List<PatrolRound> Rounds { get; set; } = new();
    public List<PatrolBeat> Beats { get; set; } = new();

    public bool AppliesTo(DateOnly night) =>
        (ValidFrom is null || night >= ValidFrom) && (ValidTo is null || night <= ValidTo);
}

/// <summary>Staff detailed for one night (patrolmen) or one day (keymen), as sent by the section staff.</summary>
public sealed class DutyRoster
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Night of (19-20/11/2026 = 19.11.2026); for keymen the duty date.</summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Category { get; set; } = "Cold Weather Patrolman";
    /// <summary>Section engineer / area (BSSL, MLGH, KM 117/0 - 152/18 ...).</summary>
    public string Area { get; set; } = "";
    public string Source { get; set; } = "";
    public List<DutyBeat> Beats { get; set; } = new();

    [JsonIgnore] public bool IsNight => !Category.Contains("Keyman", StringComparison.OrdinalIgnoreCase);
    [JsonIgnore] public string Title => $"{Category} - {(IsNight ? "night of " + Patrol.NightText(Date) : Fmt.Date(Date))}" + (Area.Length > 0 ? $" - {Area}" : "");
}

public sealed class DutyBeat
{
    /// <summary>Beat / unit number as written (1, 07 ...).</summary>
    public string No { get; set; } = "";
    public double FromKm { get; set; }
    public double ToKm { get; set; }
    public List<DutyPerson> Persons { get; set; } = new();

    [JsonIgnore] public string Range => $"KM {Patrol.KmText(FromKm)} - {Patrol.KmText(ToKm)}";
    public bool Contains(double km) => km >= Math.Min(FromKm, ToKm) - 1e-6 && km <= Math.Max(FromKm, ToKm) + 1e-6;
}

public sealed class DutyPerson
{
    public string Name { get; set; } = "";
    public string Mobile { get; set; } = "";
    public bool HomeGuard { get; set; }
    /// <summary>Starting KM or any other note.</summary>
    public string Note { get; set; } = "";

    [JsonIgnore] public string Display => (HomeGuard ? "Home Guard " : "") + Name;
    [JsonIgnore] public bool MobileLooksRight => Regex.IsMatch(Mobile, @"^[6-9]\d{9}$");
    [JsonIgnore] public string MobileDisplay => Mobile.Length == 10 ? $"{Mobile[..5]} {Mobile[5..]}" : Mobile;
}

/// <summary>A meeting of the train with a patrol pair as the chart says it should happen.</summary>
public sealed class ChartCheckRow
{
    public PatrolBeat Beat { get; set; } = new();
    public double? ExpectedKm { get; set; }
    public TimeOnly? ExpectedTime { get; set; }
    public StaffSighting? Seen { get; set; }
    public double? SeenKm { get; set; }
    /// <summary>As per chart / Off position / Not noticed / Seen (not on chart beat)</summary>
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public List<DutyPerson> OnDuty { get; set; } = new();
}

public static class Patrol
{
    public const string AsPerChart = "As per chart";
    public const string OffPosition = "Off position";
    public const string NotNoticed = "Not recorded";
    public const string OutsideChart = "Not on chart";

    public static string KmText(double km)
    {
        var whole = (int)Math.Floor(km + 1e-9);
        var frac = km - whole;
        return frac < 0.005 ? $"{whole}/0" : $"{whole}/{Math.Round(frac * 1000):000}";
    }

    public static string NightText(DateOnly d) => $"{d.Day:00}-{d.AddDays(1).Day:00}/{d.AddDays(1).Month:00}/{d.AddDays(1).Year}";

    /// <summary>The night a time belongs to: before noon = the night that began the previous evening.</summary>
    public static DateOnly NightOf(DateOnly date, TimeOnly? time) =>
        time is TimeOnly t && t.Hour < 12 ? date.AddDays(-1) : date;

    /// <summary>KM value of "40/14", "152/900", "KM 66/0", "LC 66" (not known = null) or a station code (from chainage).</summary>
    public static double? KmOf(string location, IEnumerable<StationKm>? chainage = null)
    {
        var s = (location ?? "").Trim();
        var m = Regex.Match(s, @"^(?:KM\.?\s*)?(\d{1,4})(?:\s*/\s*(\d{1,3}))?", RegexOptions.IgnoreCase);
        if (m.Success && (m.Groups[2].Success || Regex.IsMatch(s, @"^KM", RegexOptions.IgnoreCase)))
            return ToKm(int.Parse(m.Groups[1].Value, Fmt.Inv), m.Groups[2].Success ? int.Parse(m.Groups[2].Value, Fmt.Inv) : 0, m.Groups[2].Value.Length);
        var code = Regex.Match(s.ToUpperInvariant(), @"^[A-Z]{2,5}").Value;
        var st = chainage?.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        return st?.Km;
    }

    /// <summary>KM 152/900 = 152.900 (metres); KM 40/14 = OHE mast 40/14, placed from the mast register (average span where not listed).</summary>
    public static double ToKm(int km, int sub, int digits) =>
        digits >= 3 ? km + sub / 1000.0 : sub == 0 ? km : OheMasts.KmOf(km, sub);

    /// <summary>Minutes from 18:00, so that a night (23:00 - 07:00) is one continuous range.</summary>
    public static double NightMinutes(TimeOnly t) => ((t.Hour * 60 + t.Minute + t.Second / 60.0) - 1080 + 1440) % 1440;

    public static TimeOnly FromNightMinutes(double nm)
    {
        var total = ((int)Math.Round(nm) + 1080) % 1440;
        return new TimeOnly(total / 60, total % 60);
    }

    /// <summary>Where the patrol pair of a beat should be at time t, or null outside patrolling hours.</summary>
    public static double? PositionAt(PatrolChart chart, PatrolBeat beat, double nm)
    {
        var rounds = chart.Rounds.Select(r => (S: NightMinutes(r.Start), E: NightMinutes(r.End))).ToList();
        for (int k = 0; k < rounds.Count; k++)
        {
            var (s, e) = rounds[k];
            if (e <= s) e += 1440;
            bool forward = (k % 2 == 0) == beat.StartsAtFromEnd;
            if (nm >= s && nm <= e)
            {
                var f = (nm - s) / (e - s);
                return forward ? beat.FromKm + f * (beat.ToKm - beat.FromKm) : beat.ToKm - f * (beat.ToKm - beat.FromKm);
            }
            if (k + 1 < rounds.Count && nm > e && nm < rounds[k + 1].S)
                return forward ? beat.ToKm : beat.FromKm;   // resting at the turning point between rounds
        }
        return null;
    }

    /// <summary>
    /// Running KM of the train at a given time. With the train's schedule in the timetable, the scheduled running and halts
    /// between the two stations are followed (stretched to the actual times); otherwise an even speed is assumed.
    /// </summary>
    public static Func<double, double>? TrainPosition(InspectionReport r, IList<StationKm> chainage, out double startNm, out double endNm,
        IEnumerable<TrainSchedule>? trains = null)
    {
        startNm = endNm = 0;
        var a = KmOf(r.FromStation, chainage);
        var b = KmOf(r.ToStation, chainage);
        if (a is null || b is null || r.Departure is null || r.Arrival is null) return null;
        startNm = NightMinutes(r.Departure.Value);
        endNm = NightMinutes(r.Arrival.Value);
        if (endNm <= startNm) endNm += 1440;
        if (Timetable.Find(trains, r.TrainNo) is TrainSchedule t && Timetable.Profile(t, r, chainage) is { } pts)
            return nm => Timetable.At(pts, nm);
        double s0 = startNm, e0 = endNm, ka = a.Value, kb = b.Value;
        return nm => ka + (kb - ka) * Math.Clamp((nm - s0) / (e0 - s0), 0, 1);
    }

    /// <summary>True when the position comes from the train's schedule rather than an even speed.</summary>
    public static bool UsesSchedule(InspectionReport r, IList<StationKm> chainage, IEnumerable<TrainSchedule>? trains) =>
        Timetable.Find(trains, r.TrainNo) is TrainSchedule t && Timetable.Profile(t, r, chainage) is not null;

    /// <summary>Approximate time the train passed a KM.</summary>
    public static TimeOnly? TimeAtKm(InspectionReport r, IList<StationKm> chainage, double km, IEnumerable<TrainSchedule>? trains = null)
    {
        var pos = TrainPosition(r, chainage, out var s, out var e, trains);
        if (pos is null) return null;
        double a = pos(s), b = pos(e);
        if (Math.Abs(b - a) < 1e-6 || km < Math.Min(a, b) - 0.3 || km > Math.Max(a, b) + 0.3) return null;
        double prev = pos(s);
        for (double nm = s + 0.25; nm <= e + 1e-9; nm += 0.25)
        {
            var x = pos(nm);
            if ((prev - km) * (x - km) <= 0 && Math.Abs(x - prev) > 1e-9)
                return FromNightMinutes(nm - 0.25 + 0.25 * (km - prev) / (x - prev));
            prev = x;
        }
        return null;
    }

    /// <summary>
    /// Every meeting of the train with a patrol pair on the chart, compared with the field staff recorded in the report.
    /// </summary>
    public static List<ChartCheckRow> Check(InspectionReport r, PatrolChart chart, IList<StationKm> chainage, IEnumerable<DutyRoster>? rosters = null,
        double tolerance = 1.0, IEnumerable<TrainSchedule>? trains = null)
    {
        var rows = new List<ChartCheckRow>();
        var pos = TrainPosition(r, chainage, out var s, out var e, trains);
        if (pos is null) return rows;
        var night = NightOf(r.Date, r.Departure);
        var duty = (rosters ?? Enumerable.Empty<DutyRoster>()).Where(d => d.Date == night && d.IsNight).ToList();

        foreach (var beat in chart.Beats)
        {
            // Walk the run in quarter minutes; the train meets the pair where (train KM - patrol KM) changes sign.
            const double step = 0.25;
            double lo = Math.Min(beat.FromKm, beat.ToKm), hi = Math.Max(beat.FromKm, beat.ToKm);
            double? prevDiff = null, prevNm = null;
            for (double nm = s; nm <= e + 1e-9; nm += step)
            {
                var x = pos(nm);
                var p = PositionAt(chart, beat, nm);
                if (p is null) { prevDiff = null; continue; }
                var diff = x - p.Value;
                bool near = x >= lo - 2 && x <= hi + 2;
                if (near && prevDiff is double pd && prevNm is double pn && (diff == 0 || Math.Sign(pd) != Math.Sign(diff)))
                {
                    var meetNm = pn + (nm - pn) * Math.Abs(pd) / Math.Max(1e-9, Math.Abs(pd) + Math.Abs(diff));
                    var meetKm = pos(meetNm);
                    if (meetKm >= lo - 0.05 && meetKm <= hi + 0.05)
                    {
                        rows.Add(new ChartCheckRow
                        {
                            Beat = beat,
                            ExpectedKm = Math.Round(Math.Clamp(meetKm, lo, hi), 2),
                            ExpectedTime = FromNightMinutes(meetNm),
                            OnDuty = duty.SelectMany(d => d.Beats).Where(b => b.Contains((lo + hi) / 2)).SelectMany(b => b.Persons).ToList()
                        });
                        break;
                    }
                }
                prevDiff = diff;
                prevNm = nm;
            }
        }

        // Match the patrolmen recorded in the report.
        var patrolSeen = r.Sightings.Where(x => x.Category.Contains("Patrol", StringComparison.OrdinalIgnoreCase) || x.Category.Contains("Keyman", StringComparison.OrdinalIgnoreCase))
            .Select(x => (S: x, Km: KmOf(x.Location, chainage))).Where(x => x.Km is not null).ToList();
        var used = new HashSet<StaffSighting>();
        foreach (var row in rows)
        {
            var best = patrolSeen.Where(x => !used.Contains(x.S) && row.Beat.Contains(x.Km!.Value))
                .OrderBy(x => Math.Abs(x.Km!.Value - row.ExpectedKm!.Value)).FirstOrDefault();
            if (best.S is null)
            {
                row.Status = NotNoticed;
                row.Detail = $"{row.Beat.Patrolmen} ({row.Beat.Unit}) should have been met near KM {KmText(row.ExpectedKm!.Value)} at about {Fmt.Time(row.ExpectedTime)} hrs; not recorded in the report.";
                continue;
            }
            used.Add(best.S);
            row.Seen = best.S;
            row.SeenKm = best.Km;
            var dev = Math.Abs(best.Km!.Value - row.ExpectedKm!.Value);
            row.Status = dev <= tolerance ? AsPerChart : OffPosition;
            row.Detail = row.Status == AsPerChart
                ? $"Met at {best.S.Location} (chart: KM {KmText(row.ExpectedKm.Value)}, {Fmt.Time(row.ExpectedTime)} hrs)."
                : $"Met at {best.S.Location}; as per chart they should have been near KM {KmText(row.ExpectedKm.Value)} at {Fmt.Time(row.ExpectedTime)} hrs ({dev:0.0} km away).";
        }
        foreach (var (sg, km) in patrolSeen.Where(x => !used.Contains(x.S)))
        {
            var beat = chart.Beats.FirstOrDefault(b => b.Contains(km!.Value));
            rows.Add(new ChartCheckRow
            {
                Beat = beat ?? new PatrolBeat { Patrolmen = "-", FromKm = km!.Value, ToKm = km.Value },
                Seen = sg,
                SeenKm = km,
                Status = OutsideChart,
                Detail = beat is null ? $"{sg.Location}: no beat on the chart here." : $"{sg.Location}: a second sighting on beat {beat.Patrolmen}."
            });
        }
        return rows.OrderBy(x => x.ExpectedKm ?? x.SeenKm ?? 0).ToList();
    }

    /// <summary>Shortcoming lines for chart deviations, to add to the report.</summary>
    public static List<string> Shortcomings(IEnumerable<ChartCheckRow> rows) =>
        rows.Where(x => x.Status is NotNoticed or OffPosition)
            .Select(x => x.Status == NotNoticed
                ? $"{x.Beat.Patrolmen} ({x.Beat.Unit}, {x.Beat.Range}) not noticed near KM {KmText(x.ExpectedKm!.Value)} at about {Fmt.Time(x.ExpectedTime)} hrs as per patrolling chart."
                : $"{x.Beat.Patrolmen} met at {x.Seen!.Location} instead of near KM {KmText(x.ExpectedKm!.Value)} as per patrolling chart.")
            .ToList();

    /// <summary>Staff on duty at a KM on a night (patrolmen) or day (keymen).</summary>
    public static List<(DutyRoster Roster, DutyBeat Beat)> OnDutyAt(IEnumerable<DutyRoster> rosters, double km, DateOnly night, DateOnly day) =>
        rosters.Where(r => r.IsNight ? r.Date == night : r.Date == day)
            .SelectMany(r => r.Beats.Where(b => b.Contains(km)).Select(b => (r, b)))
            .ToList();

    // ------------------------------------------------------------------ built-in data

    /// <summary>Chainage of the Kota - Chittaurgarh section stations (KM from GQL), from the Winter Patrolling Chart 2026-27 and the
    /// Kota Division map (GQL, GQL 'W' Cabin, THEA, BUDI, SNAR, JNRI, URML, SMPA, MLGH, BNDI, PSLI, BSSL, CNA) and RBS coaching distances for the rest.</summary>
    public static List<StationKm> DefaultChainage() => new()
    {
        new() { Code = "SGAC", Km = -8.27 }, new() { Code = "KOTA", Km = -5.56 }, new() { Code = "KCCN", Km = -3.25 },
        new() { Code = "GQL", Km = 0.0 }, new() { Code = "GWCN", Km = 1.703 }, new() { Code = "GQLW", Km = 1.703 }, new() { Code = "DEIT", Km = 9.00 },
        new() { Code = "THEA", Km = 18.280 }, new() { Code = "BUDI", Km = 32.202 }, new() { Code = "SNAR", Km = 42.501 },
        new() { Code = "NIMA", Km = 54.24 }, new() { Code = "JNRI", Km = 66.000 }, new() { Code = "URML", Km = 72.903 },
        new() { Code = "SMPA", Km = 79.564 }, new() { Code = "GNRD", Km = 91.78 }, new() { Code = "MLGH", Km = 100.401 },
        new() { Code = "SISM", Km = 107.80 }, new() { Code = "BNDI", Km = 115.600 }, new() { Code = "PSLI", Km = 125.002 },
        new() { Code = "JUYA", Km = 131.60 }, new() { Code = "BSSL", Km = 139.903 }, new() { Code = "TVTN", Km = 149.81 },
        new() { Code = "CNA", Km = 158.253 }, new() { Code = "COR", Km = 165.90 }
    };

    /// <summary>
    /// Winter Patrolling Chart 2026-27, Kota - Chittaurgarh section (DRM/Kota letter No. W 503/2 Vol. VI dt 09.10.2026):
    /// SSE/P.Way/BUDI KM 1.00 - 77.00 and SSE/P.Way/MLGH KM 77.00 - 152.90; beats of 4 km, rounds 23.00-01.20, 01.50-04.10, 04.40-07.00.
    /// In each group the first beat starts from its lower KM end and the second from its higher KM end.
    /// </summary>
    public static PatrolChart WinterChartKotaCor2026()
    {
        var c = new PatrolChart
        {
            Name = "Winter Patrolling Chart 2026-27 (Kota - COR)",
            Section = "Kota-COR",
            Category = "Cold Weather Patrolman",
            ValidFrom = new DateOnly(2026, 11, 1),
            ValidTo = new DateOnly(2027, 3, 15),
            Reference = "Sr. DEN (Co)/Kota letter No. W 503/2 Vol. VI dt 09.10.2026; CTE/WCR letter No. W-HQ/W-41 Track/W-Prec./110/Part-I dt 30.09.2026",
            Rounds = new()
            {
                new() { Start = new TimeOnly(23, 0), End = new TimeOnly(1, 20) },
                new() { Start = new TimeOnly(1, 50), End = new TimeOnly(4, 10) },
                new() { Start = new TimeOnly(4, 40), End = new TimeOnly(7, 0) }
            }
        };
        void Unit(string unit, double from, double to)
        {
            int n = 0;
            for (double a = from; a < to - 1e-6; a += 4, n++)
            {
                var b = Math.Min(a + 4, to);
                c.Beats.Add(new PatrolBeat
                {
                    Unit = unit,
                    Group = n / 2 + 1,
                    Patrolmen = $"P-{2 * n + 1}&{2 * n + 2}",
                    FromKm = a,
                    ToKm = Math.Round(b, 3),
                    StartsAtFromEnd = n % 2 == 0
                });
            }
        }
        Unit("BUDI", 1, 77);
        Unit("MLGH", 77, 152.9);
        return c;
    }

    // ------------------------------------------------------------------ roster reader

    private const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string Phone = @"(?:\+?91[\s-]?)?[6-9]\d[\d ]{6,11}\d";
    private static readonly Regex DateLine = new(@"\b(?:date|dt|dated|tomorrow|today|tonight)\b\s*[=:\-.]?\s*(\d{1,2})(?:\s*[-–&]\s*(\d{1,2}))?\s*[/.\-]\s*(\d{1,2})\s*[/.\-]\s*(\d{2,4})", I);
    private static readonly Regex BeatLine = new(@"^\(?\s*(\d{1,2})\s*\)?\s*[.)]?\s*km\s*[-:.]?\s*(\d{1,4})\s*/\s*(\d{1,3})\s*(?:-|–|to)\s*(?:km\s*)?(\d{1,4})\s*/\s*(\d{1,3})", I);
    private static readonly Regex UnitBeatLine = new(@"(?:km\s*beat|beat\s*km|beat)\s*[-:.]*\s*(\d{1,4})\s*/\s*(\d{1,3})\s*(?:-|–|to)\s*(\d{1,4})\s*/\s*(\d{1,3})", I);
    private static readonly Regex UnitLine = new(@"^unit\s*(?:no)?\s*[.\-:]*\s*(\d{1,3})\b", I);
    private static readonly Regex TableRow = new(@"^(?:(\d{1,2})\s+)?(\d{1,4})\s*/\s*(\d{1,3})\s+([A-Za-z][A-Za-z .,()']*?)\s+(" + Phone + @")\s*$", I);
    private static readonly Regex PersonLine = new(@"^(?:\d{1,2}\s*[.)]?\s*)?([A-Za-z][A-Za-z .,()'’]*?)\s*[-–:,=]*\s*(" + Phone + @")\s*$", I);
    private static readonly Regex PhoneOnly = new(@"^\s*(" + Phone + @")\s*$", I);
    private static readonly Regex NameOnly = new(@"^(?:\d{1,2}\s*[.)]?\s*)?([A-Za-z][A-Za-z .']{2,40})$", I);
    private static readonly Regex Skip = new(@"^(key\s*man\b.*|r\s*,?\s*sir.*|respected.*|sir.*|tonight'?s?.*|tomorrow.*section.*|sr\.?\s*no.*|s\.?\s*no.*|name\s+of.*|gps)$", I);

    public static bool LooksLikeRoster(string text) =>
        Regex.Matches(text, Phone).Count >= 2 &&
        (Regex.IsMatch(text, @"\bkm\b|\d{1,4}/\d{1,3}", I)) &&
        !Regex.IsMatch(text, @"observations?\s*[:\-]?|shortcomings", I);

    /// <summary>Reads duty lists sent on WhatsApp or printed as a table (beat KM, names, mobile numbers).</summary>
    public static List<DutyRoster> ParseRosters(string text, string source = "Text")
    {
        var list = new List<DutyRoster>();
        DutyRoster? cur = null;
        DutyBeat? beat = null;
        string? pendingUnit = null;
        string? pendingName = null;
        DateOnly? date = null;
        string? category = null;

        bool curDated = false, curCategorized = false, categoryExplicit = false;

        DutyRoster Roster()
        {
            if (cur is null)
            {
                cur = new DutyRoster { Source = source, Category = category ?? "Cold Weather Patrolman", Date = date ?? DateOnly.FromDateTime(DateTime.Today) };
                curDated = date is not null;
                curCategorized = category is not null;
                list.Add(cur);
            }
            return cur;
        }
        void NewRosterIfUsed()
        {
            if (cur is not null && cur.Beats.Count > 0) { cur = null; beat = null; }
        }

        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var t = raw.Replace('\u00A0', ' ').Trim();
            var wa = ReportParser.WaBracket.Match(t);
            if (!wa.Success) wa = ReportParser.WaDash.Match(t);
            if (wa.Success) t = wa.Groups[1].Value.Trim();
            if (t.Length == 0) continue;

            var cat = CategoryOf(t);
            var dm = DateLine.Match(t);
            if (dm.Success)
            {
                int d = int.Parse(dm.Groups[1].Value, Fmt.Inv), mo = int.Parse(dm.Groups[3].Value, Fmt.Inv), y = int.Parse(dm.Groups[4].Value, Fmt.Inv);
                y = y < 100 ? 2000 + y : y < 1000 ? 2000 + y % 100 : y;
                if (mo is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(y, mo))
                {
                    var nd = new DateOnly(y, mo, d);
                    if (cur is not null && !curDated) { cur.Date = nd; curDated = true; }          // date written after the list
                    else if (date is not null && date != nd) { NewRosterIfUsed(); categoryExplicit = false; category = null; }
                    date = nd;
                }
            }
            if (cat is not null)
            {
                if (cur is not null && !curCategorized) { cur.Category = cat; curCategorized = true; }
                else if (cat != category) NewRosterIfUsed();
                category = cat;
                categoryExplicit = true;
            }
            var area = Regex.Match(t, @"km\s*(\d{1,4}\s*/\s*\d{1,3}\s*[-–]\s*\d{1,4}\s*/\s*\d{1,3})", I);
            if (cat is not null && area.Success) Roster().Area = "KM " + Regex.Replace(area.Groups[1].Value, @"\s", "");
            if ((cat is not null || dm.Success) && !Regex.IsMatch(t, Phone)) continue;

            var bm = BeatLine.Match(t);
            if (bm.Success)
            {
                beat = new DutyBeat
                {
                    No = bm.Groups[1].Value,
                    FromKm = ToKm(int.Parse(bm.Groups[2].Value, Fmt.Inv), int.Parse(bm.Groups[3].Value, Fmt.Inv), bm.Groups[3].Value.Length),
                    ToKm = ToKm(int.Parse(bm.Groups[4].Value, Fmt.Inv), int.Parse(bm.Groups[5].Value, Fmt.Inv), bm.Groups[5].Value.Length)
                };
                Roster().Beats.Add(beat);
                pendingName = null;
                continue;
            }
            var um = UnitLine.Match(t);
            if (um.Success)
            {
                pendingUnit = um.Groups[1].Value;
                pendingName = null;
                if (!categoryExplicit) { category = "Keyman"; categoryExplicit = true; }
                continue;
            }
            var ub = UnitBeatLine.Match(t);
            if (ub.Success)
            {
                if (!categoryExplicit) { category = "Keyman"; categoryExplicit = true; }
                beat = new DutyBeat
                {
                    No = pendingUnit ?? (Roster().Beats.Count + 1).ToString(Fmt.Inv),
                    FromKm = ToKm(int.Parse(ub.Groups[1].Value, Fmt.Inv), int.Parse(ub.Groups[2].Value, Fmt.Inv), ub.Groups[2].Value.Length),
                    ToKm = ToKm(int.Parse(ub.Groups[3].Value, Fmt.Inv), int.Parse(ub.Groups[4].Value, Fmt.Inv), ub.Groups[4].Value.Length)
                };
                Roster().Category = category ?? "Keyman";
                Roster().Beats.Add(beat);
                pendingUnit = null;
                continue;
            }
            var tr = TableRow.Match(t);
            if (tr.Success)
            {
                var km = ToKm(int.Parse(tr.Groups[2].Value, Fmt.Inv), int.Parse(tr.Groups[3].Value, Fmt.Inv), tr.Groups[3].Value.Length);
                if (tr.Groups[1].Success || beat is null)
                {
                    beat = new DutyBeat { No = tr.Groups[1].Success ? tr.Groups[1].Value : (Roster().Beats.Count + 1).ToString(Fmt.Inv), FromKm = km, ToKm = km };
                    Roster().Beats.Add(beat);
                }
                else
                {
                    beat.FromKm = Math.Min(beat.FromKm, km);
                    beat.ToKm = Math.Max(beat.ToKm, km);
                }
                var p = Person(tr.Groups[4].Value, tr.Groups[5].Value);
                p.Note = "from KM " + KmText(km);
                beat.Persons.Add(p);
                continue;
            }
            if (Skip.IsMatch(t) || Regex.IsMatch(t, @"^\d{1,4}$")) continue;
            var pm = PersonLine.Match(t);
            if (pm.Success && beat is not null)
            {
                beat.Persons.Add(Person(pm.Groups[1].Value, pm.Groups[2].Value));
                pendingName = null;
                continue;
            }
            var ph = PhoneOnly.Match(t);
            if (ph.Success && beat is not null && pendingName is not null)
            {
                beat.Persons.Add(Person(pendingName, ph.Groups[1].Value));
                pendingName = null;
                continue;
            }
            var nm = NameOnly.Match(t);
            if (nm.Success && beat is not null) pendingName = nm.Groups[1].Value;
        }
        list.RemoveAll(r => r.Beats.Count == 0);
        foreach (var r in list)
            foreach (var b in r.Beats.Where(b => b.ToKm < b.FromKm))
                (b.FromKm, b.ToKm) = (b.ToKm, b.FromKm);
        return list;
    }

    private static string? CategoryOf(string t)
    {
        if (Regex.IsMatch(t, @"\bcwp\b|cold\s*weather|winter\s*patrol", I)) return "Cold Weather Patrolman";
        if (Regex.IsMatch(t, @"hot\s*weather|\bhwp\b", I)) return "Hot Weather Patrolman";
        if (Regex.IsMatch(t, @"monsoon", I)) return "Monsoon Patrolman";
        if (Regex.IsMatch(t, @"night\s*patrol", I)) return "Night Patrolman";
        if (Regex.IsMatch(t, @"^(?!key\s*man\s*name).*key\s*man", I) && !Regex.IsMatch(t, @"^key\s*man\b", I)) return "Keyman";
        return null;
    }

    private static DutyPerson Person(string name, string phone)
    {
        var n = name.Trim().Trim(',', '-', '.', ' ');
        bool hg = Regex.IsMatch(n, @"home\s*g(?:ua|u|a)?r?d|\(?\bHG\b\)?", I);
        n = Regex.Replace(n, @"home\s*g(?:ua|u|a)?r?d\s*,?", "", I);
        n = Regex.Replace(n, @"\(?\bHG\b\)?", "", I);
        n = Regex.Replace(n, @"\s+", " ").Trim().Trim(',', '-', '.', ' ');
        n = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.ToLowerInvariant());
        var digits = Regex.Replace(phone, @"\D", "");
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        return new DutyPerson { Name = n, Mobile = digits, HomeGuard = hg };
    }

    /// <summary>Adds rosters, replacing beats of the same date, category and KM range.</summary>
    public static (int Rosters, int Beats) Merge(List<DutyRoster> into, IEnumerable<DutyRoster> rosters)
    {
        int nr = 0, nb = 0;
        foreach (var r in rosters)
        {
            var same = into.FirstOrDefault(x => x.Date == r.Date && x.Category == r.Category && (x.Area == r.Area || x.Area.Length == 0 || r.Area.Length == 0)
                                               && x.Beats.Any(b => r.Beats.Any(c => Math.Abs(b.FromKm - c.FromKm) < 0.01 && Math.Abs(b.ToKm - c.ToKm) < 0.01)));
            same ??= into.FirstOrDefault(x => x.Date == r.Date && x.Category == r.Category && x.Area == r.Area);
            if (same is null) { into.Add(r); nr++; nb += r.Beats.Count; continue; }
            foreach (var b in r.Beats)
            {
                same.Beats.RemoveAll(x => Math.Abs(x.FromKm - b.FromKm) < 0.01 && Math.Abs(x.ToKm - b.ToKm) < 0.01);
                same.Beats.Add(b);
                nb++;
            }
            same.Beats.Sort((a, b) => a.FromKm.CompareTo(b.FromKm));
            same.ModifiedUtc = DateTime.UtcNow;
        }
        return (nr, nb);
    }
}
