using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace FootplateIR.Core;

// ============================================================================================
//  Train timetable kept in the app (works offline): schedules imported from the working time
//  table (Excel / PDF / photo) or from the schedule text copied from NTES, and used to fill
//  report times, list trains passing a station / KM, and place the train for the chart check.
//  Live running status is looked up on NTES itself (opened inside the app).
// ============================================================================================

public sealed class ScheduleStop
{
    public string Code { get; set; } = "";
    public TimeOnly? Arr { get; set; }
    public TimeOnly? Dep { get; set; }
    /// <summary>Day of the journey (1 = starting day) as printed in the schedule; 0 = not known.</summary>
    public int Day { get; set; }

    [JsonIgnore] public TimeOnly? Time => Dep ?? Arr;
    [JsonIgnore] public bool Halts => Arr is not null && Dep is not null && Arr != Dep;
}

public sealed class TrainSchedule
{
    public string TrainNo { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Days of running as written (Daily, Mon Thu ...).</summary>
    public string Days { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public List<ScheduleStop> Stops { get; set; } = new();

    [JsonIgnore] public string Title => TrainNo + (Name.Length > 0 ? " " + Name : "");
    [JsonIgnore] public string Route => Stops.Count > 0 ? $"{Stops[0].Code} - {Stops[^1].Code}" : "";

    public ScheduleStop? Stop(string code) =>
        Stops.FirstOrDefault(s => string.Equals(s.Code, (code ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A train passing a station / KM.</summary>
public sealed record TrainPass(TrainSchedule Train, TimeOnly Time, string Where, bool Stops, bool TowardsHigherKm, string Detail);

public static class Timetable
{
    private const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    public const string NtesUrl = "https://enquiry.indianrail.gov.in/mntes/";

    public static TrainSchedule? Find(IEnumerable<TrainSchedule>? trains, string? trainNo)
    {
        var n = (trainNo ?? "").Trim();
        return n.Length == 0 ? null : trains?.FirstOrDefault(t => t.TrainNo == n);
    }

    /// <summary>Scheduled departure from <paramref name="from"/> and arrival at <paramref name="to"/>.</summary>
    public static (TimeOnly? Dep, TimeOnly? Arr) TimesFor(TrainSchedule t, string from, string to) =>
        (t.Stop(from)?.Dep ?? t.Stop(from)?.Arr, t.Stop(to)?.Arr ?? t.Stop(to)?.Dep);

    // ------------------------------------------------------------------ position of a train

    /// <summary>
    /// (time, KM) points of the train between two stations from its schedule, stretched to the actual departure and arrival
    /// so that halts and slow stretches are placed right. Null when the schedule does not have both stations.
    /// </summary>
    public static List<(double Nm, double Km)>? Profile(TrainSchedule t, InspectionReport r, IList<StationKm> chainage)
    {
        int i = t.Stops.FindIndex(s => string.Equals(s.Code, r.FromStation, StringComparison.OrdinalIgnoreCase));
        int j = t.Stops.FindIndex(s => string.Equals(s.Code, r.ToStation, StringComparison.OrdinalIgnoreCase));
        if (i < 0 || j < 0 || i == j || r.Departure is null || r.Arrival is null) return null;
        int step = j > i ? 1 : -1;
        var pts = new List<(double Nm, double Km)>();
        double last = double.NaN, add = 0;
        for (int k = i; k != j + step; k += step)
        {
            var s = t.Stops[k];
            var km = Patrol.KmOf(s.Code, chainage);
            if (km is null) continue;
            foreach (var time in k == i ? new[] { s.Dep ?? s.Arr } : k == j ? new[] { s.Arr ?? s.Dep } : new[] { s.Arr, s.Dep })
            {
                if (time is not TimeOnly tm) continue;
                var nm = Patrol.NightMinutes(tm) + add;
                if (!double.IsNaN(last) && nm < last - 1) { add += 1440; nm += 1440; }
                pts.Add((nm, km.Value));
                last = nm;
            }
        }
        if (pts.Count < 2 || pts[^1].Nm - pts[0].Nm < 1) return null;
        // Stretch the scheduled times onto the actual departure / arrival.
        double s0 = Patrol.NightMinutes(r.Departure.Value), e0 = Patrol.NightMinutes(r.Arrival.Value);
        if (e0 <= s0) e0 += 1440;
        double a0 = pts[0].Nm, a1 = pts[^1].Nm;
        return pts.Select(p => (s0 + (p.Nm - a0) * (e0 - s0) / (a1 - a0), p.Km)).ToList();
    }

    public static double At(List<(double Nm, double Km)> pts, double nm)
    {
        if (nm <= pts[0].Nm) return pts[0].Km;
        for (int k = 1; k < pts.Count; k++)
            if (nm <= pts[k].Nm)
            {
                var (n0, k0) = pts[k - 1];
                var (n1, k1) = pts[k];
                return n1 - n0 < 1e-9 ? k1 : k0 + (k1 - k0) * (nm - n0) / (n1 - n0);
            }
        return pts[^1].Km;
    }

    // ------------------------------------------------------------------ trains passing a place

    /// <summary>Time a scheduled train passes a KM (between two of its stations with known KM), or null.</summary>
    public static (TimeOnly Time, bool Up)? TimeAtKm(TrainSchedule t, double km, IList<StationKm> chainage)
    {
        for (int k = 1; k < t.Stops.Count; k++)
        {
            var a = t.Stops[k - 1];
            var b = t.Stops[k];
            var ka = Patrol.KmOf(a.Code, chainage);
            var kb = Patrol.KmOf(b.Code, chainage);
            if (ka is null || kb is null || a.Time is null || (b.Arr ?? b.Dep) is null) continue;
            double lo = Math.Min(ka.Value, kb.Value), hi = Math.Max(ka.Value, kb.Value);
            if (km < lo - 1e-6 || km > hi + 1e-6 || hi - lo < 1e-6) continue;
            double na = Patrol.NightMinutes(a.Time.Value), nb = Patrol.NightMinutes((b.Arr ?? b.Dep)!.Value);
            if (nb < na) nb += 1440;
            var f = (km - ka.Value) / (kb.Value - ka.Value);
            return (Patrol.FromNightMinutes(na + f * (nb - na)), kb.Value > ka.Value);
        }
        return null;
    }

    /// <summary>Trains passing a station (code) or KM between two times (a night window like 22:00 - 07:00 works).</summary>
    public static List<TrainPass> Passing(IEnumerable<TrainSchedule> trains, string place, TimeOnly from, TimeOnly to, IList<StationKm> chainage)
    {
        var list = new List<TrainPass>();
        var code = Regex.Match((place ?? "").Trim().ToUpperInvariant(), @"^[A-Z]{2,5}$").Value;
        var km = Patrol.KmOf(place ?? "", chainage);
        double f0 = Patrol.NightMinutes(from), t0 = Patrol.NightMinutes(to);
        bool In(TimeOnly t)
        {
            var n = Patrol.NightMinutes(t);
            return t0 >= f0 ? n >= f0 && n <= t0 : n >= f0 || n <= t0;
        }
        foreach (var t in trains)
        {
            if (code.Length > 0 && t.Stop(code) is ScheduleStop st && st.Time is TimeOnly tm)
            {
                if (!In(tm)) continue;
                var idx = t.Stops.IndexOf(st);
                var next = t.Stops.Skip(idx + 1).Select(s => Patrol.KmOf(s.Code, chainage)).FirstOrDefault(k => k is not null);
                var prev = t.Stops.Take(idx).Reverse().Select(s => Patrol.KmOf(s.Code, chainage)).FirstOrDefault(k => k is not null);
                var here = Patrol.KmOf(code, chainage) ?? 0;
                bool up = next is double n ? n > here : prev is double p && p < here;
                var detail = st.Arr is null ? $"starts {Fmt.Time(st.Dep)}" : st.Dep is null ? $"ends {Fmt.Time(st.Arr)}"
                    : st.Halts ? $"arr {Fmt.Time(st.Arr)} dep {Fmt.Time(st.Dep)}" : $"passes {Fmt.Time(st.Dep)}";
                list.Add(new TrainPass(t, tm, code, st.Halts || st.Arr is null || st.Dep is null, up, detail));
            }
            else if (km is double k && TimeAtKm(t, k, chainage) is { } hit && In(hit.Time))
                list.Add(new TrainPass(t, hit.Time, "KM " + Patrol.KmText(k), false, hit.Up, $"about {Fmt.Time(hit.Time)}"));
        }
        return list.OrderBy(p => Patrol.NightMinutes(p.Time) - f0 < 0 ? Patrol.NightMinutes(p.Time) - f0 + 1440 : Patrol.NightMinutes(p.Time) - f0).ToList();
    }

    // ------------------------------------------------------------------ reading timetables

    // 00:27, 0:27 and 00.27 (dot only with a two-digit hour, so distances like 8.27 km are not read as times).
    private static readonly Regex TimeRx = new(@"(?<![\d:.])(?:(?<h>[01]?\d|2[0-3]):(?<m>[0-5]\d)|(?<h>[01]\d|2[0-3])\.(?<m>[0-5]\d)(?!\s*km))(?![\d:.])", I);

    private static List<TimeOnly> Times(string s) =>
        TimeRx.Matches(s).Select(m => new TimeOnly(int.Parse(m.Groups["h"].Value, Fmt.Inv), int.Parse(m.Groups["m"].Value, Fmt.Inv))).ToList();
    private static readonly Regex TrainHead = new(@"(?:train\s*(?:no\.?|number)?\s*[:\-]?\s*)?\b(?<no>0?\d{5})\b\s*[-–:]?\s*(?<name>[A-Za-z][A-Za-z .()/&'-]{2,60})?", I);
    private static readonly HashSet<string> NotCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SL", "NO", "SN", "KM", "ARR", "DEP", "DAY", "PF", "HALT", "SRC", "DSTN", "STN", "AM", "PM", "HRS", "JN", "RS", "THE", "AND", "TO"
    };

    public static bool LooksLikeTimetable(string text) =>
        TimeRx.Matches(text).Count >= 4 && Regex.IsMatch(text, @"\b\d{5}\b") &&
        !Regex.IsMatch(text, @"observations?\s*[:\-]?|shortcomings", I);

    /// <summary>
    /// Reads schedules written one stop per line — as copied from NTES "Train Schedule", typed, or read from a photo:
    /// a line with the train number (and name) starts a train; each following line with a station code and one or two
    /// times (arrival, departure) is a stop. "Source" / "Destination" are understood.
    /// </summary>
    public static List<TrainSchedule> Parse(string text, Masters? masters = null, string source = "Text")
    {
        var list = new List<TrainSchedule>();
        TrainSchedule? cur = null;
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var t = raw.Replace(' ', ' ').Trim();
            if (t.Length == 0) continue;
            var times = Times(t);
            var code = StationCode(t, masters);
            if (times.Count == 0 || code is null)
            {
                var h = TrainHead.Match(t);
                if (h.Success && times.Count == 0)
                {
                    var no = h.Groups["no"].Value[^5..];
                    cur = list.FirstOrDefault(x => x.TrainNo == no);
                    if (cur is null)
                    {
                        cur = new TrainSchedule { TrainNo = no, Name = Regex.Replace(h.Groups["name"].Value.Trim(), @"\s+", " "), Source = source };
                        list.Add(cur);
                    }
                    var days = Regex.Match(t, @"\b(daily|(?:(?:mon|tue|wed|thu|fri|sat|sun)[a-z]*[\s,]*){1,7})\b", I);
                    if (days.Success && cur.Days.Length == 0) cur.Days = days.Value.Trim(' ', ',');
                }
                continue;
            }
            if (cur is null) continue;
            var stop = new ScheduleStop { Code = code };
            var day = Regex.Match(t, @"\bday\s*[-:]?\s*(\d)\b", I);
            if (day.Success) stop.Day = int.Parse(day.Groups[1].Value, Fmt.Inv);
            bool src = Regex.IsMatch(t, @"\b(source|starts?|origin|src)\b", I) || cur.Stops.Count == 0 && times.Count == 1 && Regex.IsMatch(t, @"--|-\s*$");
            bool dst = Regex.IsMatch(t, @"\b(destination|ends?|terminat\w*|dstn)\b", I);
            if (times.Count >= 2) { stop.Arr = times[0]; stop.Dep = times[1]; }
            else if (src) stop.Dep = times[0];
            else if (dst) stop.Arr = times[0];
            else { stop.Arr = times[0]; stop.Dep = times[0]; }
            cur.Stops.RemoveAll(s => s.Code == code);
            cur.Stops.Add(stop);
        }
        list.RemoveAll(t => t.Stops.Count < 2);
        return list;
    }

    /// <summary>Station code on a schedule line: "(BUDI)", "BUDI", or a station name that is in the station list.</summary>
    private static string? StationCode(string line, Masters? masters)
    {
        var p = Regex.Match(line, @"\(([A-Z]{2,5})\)");
        if (p.Success && Stations.IsKnown(p.Groups[1].Value, masters)) return p.Groups[1].Value;
        foreach (Match m in Regex.Matches(line, @"\b[A-Z]{2,5}\b"))
            if (!NotCodes.Contains(m.Value) && Stations.IsKnown(m.Value, masters)) return m.Value;
        foreach (var c in masters?.Chainage.Select(c => c.Code) ?? Enumerable.Empty<string>())
            if (Regex.IsMatch(line, $@"\b{Regex.Escape(c)}\b")) return c;
        // A station name written in full ("Bundi") - match against the names of the section stations.
        foreach (var c in Stations.KotaCorSection)
        {
            var name = Stations.NameOf(c, masters);
            if (name is not null && name.Length > 3 && Regex.IsMatch(line, $@"\b{Regex.Escape(name)}\b", I)) return c;
        }
        return null;
    }

    /// <summary>
    /// Working time table in Excel: one sheet with train numbers across the top row(s) and stations down the first column(s);
    /// a cell holds the time ("00:27", "0.27", "00:25/00:27" for arrival / departure). Two rows for one station (arr / dep) work too.
    /// </summary>
    public static List<TrainSchedule> ReadExcel(string path, Masters? masters = null)
    {
        var list = new List<TrainSchedule>();
        using var wb = new XLWorkbook(path);
        foreach (var ws in wb.Worksheets)
        {
            var used = ws.RangeUsed();
            if (used is null) continue;
            int lastRow = used.LastRow().RowNumber(), lastCol = used.LastColumn().ColumnNumber();
            // Header row: the row with the most train numbers.
            int headRow = 0, best = 0;
            for (int r = 1; r <= Math.Min(lastRow, 30); r++)
            {
                int n = Enumerable.Range(1, lastCol).Count(c => Regex.IsMatch(ws.Cell(r, c).GetFormattedString().Trim(), @"^0?\d{5}$"));
                if (n > best) { best = n; headRow = r; }
            }
            if (best < 2) continue;
            var cols = Enumerable.Range(1, lastCol)
                .Select(c => (Col: c, No: ws.Cell(headRow, c).GetFormattedString().Trim()))
                .Where(x => Regex.IsMatch(x.No, @"^0?\d{5}$")).ToList();
            var trains = cols.ToDictionary(x => x.Col, x => new TrainSchedule { TrainNo = x.No.Length == 6 ? x.No[1..] : x.No, Source = "Excel" });
            string? lastCode = null;
            for (int r = headRow + 1; r <= lastRow; r++)
            {
                string? code = null;
                for (int c = 1; c <= Math.Min(3, cols[0].Col - 1); c++)
                {
                    code = StationCode(ws.Cell(r, c).GetFormattedString(), masters);
                    if (code is not null) break;
                }
                var sameStation = code is null && lastCode is not null;   // second (departure) row of the same station
                code ??= lastCode;
                if (code is null) continue;
                foreach (var (col, _) in cols)
                {
                    var cell = ws.Cell(r, col);
                    var times = CellTimes(cell);
                    if (times.Count == 0) continue;
                    var t = trains[col];
                    var stop = t.Stop(code);
                    if (stop is null) { stop = new ScheduleStop { Code = code }; t.Stops.Add(stop); }
                    if (times.Count >= 2) { stop.Arr = times[0]; stop.Dep = times[1]; }
                    else if (sameStation && stop.Arr is not null) stop.Dep = times[0];
                    else { stop.Arr ??= times[0]; stop.Dep = times[0]; }
                }
                lastCode = sameStation ? null : code;
            }
            list.AddRange(trains.Values.Where(t => t.Stops.Count >= 2));
        }
        return list;
    }

    private static List<TimeOnly> CellTimes(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime) return new() { TimeOnly.FromDateTime(cell.GetDateTime()) };
        if (cell.DataType == XLDataType.TimeSpan) { var ts = cell.GetTimeSpan(); return new() { new TimeOnly(ts.Hours, ts.Minutes) }; }
        if (cell.DataType == XLDataType.Number)
        {
            var v = cell.GetDouble();
            if (v > 0 && v < 1) { var m = (int)Math.Round(v * 1440) % 1440; return new() { new TimeOnly(m / 60, m % 60) }; }
            if (v >= 1 && v < 24) { int h = (int)v, mi = (int)Math.Round((v - h) * 100); if (mi < 60) return new() { new TimeOnly(h, mi) }; }
        }
        var s = cell.GetFormattedString();
        var list = Times(s);
        if (list.Count == 0 && Regex.IsMatch(s.Trim(), @"^\d{3,4}$"))
        {
            var n = int.Parse(s.Trim(), Fmt.Inv);
            if (n / 100 < 24 && n % 100 < 60) list.Add(new TimeOnly(n / 100, n % 100));
        }
        return list;
    }

    /// <summary>Adds / replaces schedules (same train number = newer copy wins).</summary>
    public static (int Added, int Updated) Merge(List<TrainSchedule> into, IEnumerable<TrainSchedule> trains)
    {
        int a = 0, u = 0;
        foreach (var t in trains)
        {
            var i = into.FindIndex(x => x.TrainNo == t.TrainNo);
            if (i < 0) { into.Add(t); a++; continue; }
            if (t.Name.Length == 0) t.Name = into[i].Name;
            if (t.Days.Length == 0) t.Days = into[i].Days;
            into[i] = t;
            u++;
        }
        into.Sort((x, y) => string.CompareOrdinal(x.TrainNo, y.TrainNo));
        return (a, u);
    }

    /// <summary>Schedule written back as text (for sharing or correcting).</summary>
    public static string ToText(TrainSchedule t)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{t.TrainNo} {t.Name}".Trim() + (t.Days.Length > 0 ? $" ({t.Days})" : ""));
        for (int i = 0; i < t.Stops.Count; i++)
        {
            var s = t.Stops[i];
            var arr = s.Arr is null ? "Source" : Fmt.Time(s.Arr);
            var dep = s.Dep is null ? "Destination" : Fmt.Time(s.Dep);
            sb.AppendLine($"{s.Code} {arr} {dep}" + (s.Day > 0 ? $" Day {s.Day}" : ""));
        }
        return sb.ToString();
    }
}
