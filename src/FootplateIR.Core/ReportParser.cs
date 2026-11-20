using System.Text;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

public sealed class ParseResult
{
    public List<InspectionReport> Reports { get; } = new();
    public List<string> Messages { get; } = new();
}

/// <summary>
/// Reads footplate inspection reports from text: the WhatsApp message ("Sir, Date: ..., Inspection: ..., Observations: 1. ...,
/// Shortcomings: ..., Locations to be attended for better running: ..."), and the typed inspection notes
/// (label / value tables, crew table, numbered observations, locations table, remarks, signature).
/// Text read from Word, PDF, Excel or a photo (OCR) goes through the same reader.
/// Several reports in one text (a WhatsApp chat export, or two pasted messages) are split automatically.
/// </summary>
public static class ReportParser
{
    private const RegexOptions I = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // WhatsApp headers: "[22:57, 19/11/2026] Name: text" (iPhone / desktop copy) and "19/11/2026, 22:57 - Name: text" (Android export).
    internal static readonly Regex WaBracket = new(@"^‎?\[\d{1,2}[:.]\d{2}(?:[:.]\d{2})?(?:\s?[APap]\.?[Mm]\.?)?,\s*\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4}\]\s*[^:]{1,60}:\s?(.*)$");
    internal static readonly Regex WaDash = new(@"^‎?\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4},?\s+\d{1,2}[:.]\d{2}(?:\s?[APap]\.?[Mm]\.?)?\s+[-–]\s+[^:]{1,60}:\s?(.*)$");
    private static readonly Regex Salutation = new(@"^(respected\s+)?(sir|madam|sir\s*/\s*madam)\s*[,.!]?$", I);

    private const string Num = @"^\s*(?:\d{1,2}\s*[.)]\s*)?";
    private static readonly Regex ObsHead = new(Num + @"observations?\s*(?:made|noted)?\s*[:\-–]?\s*(.*)$", I);
    private static readonly Regex ShortHead = new(Num + @"short\s*-?\s*comings?(?:\s*/\s*deficienc\w*(?:\s+(?:noted|observed))?|\s+(?:observed|noticed|found|noted))?\s*[:\-–]?\s*(.*)$", I);
    private static readonly Regex LocHead = new(Num + @"locations?\b[^:]*?\batten(?:d|t)[a-z]*\b[^:]*?(?:better\s+running)?\s*[:\-–]?\s*(.*)$", I);
    private static readonly Regex RemarksHead = new(Num + @"remarks?\s*[:\-–]?\s*(.*)$", I);
    private static readonly Regex ActionHead = new(Num + @"action\s*(?:taken|required|proposed)?\s*[:\-–]\s*(.*)$", I);
    private static readonly Regex Numbered = new(@"^\s*(?:\d{1,2}\.\d{1,2}\.?|\d{1,2}\s*[.)])(?:\s+(.*)|\s*$)");
    private static readonly Regex KeyValue = new(@"^([A-Za-z][A-Za-z .()/&'’]{0,45}?)\s*[:\-–]\s*(.*)$");
    private static readonly Regex LocoLine = new(@"^(?:engine|loco(?:motive)?)(?!\s*pilot)\s*(?:no\.?|number|#)?\s*[:\-–]?\s*(.+)$", I);
    private static readonly Regex TitleLine = new(@"^(?<kind>(?:night|day|surprise)?\s*foot\s*-?\s*plate\s+inspection)(?:\s+(?:note|report))?\s*(?:\((?<p>[^)]*)\))?\.?$", I);
    private static readonly Regex ParenOnly = new(@"^\((?<p>[^)]{2,60})\)$");
    private static readonly Regex RouteRx = new(@"^(?:(?:ex|from)\s+)?(?<from>[A-Za-z]{2,5}(?:'[A-Za-z]')?)\s*(?:\bto\b|–|-)\s*(?<to>[A-Za-z]{2,5}(?:'[A-Za-z]')?)(?:\s+(?:by|in)\s+(?:train\s*(?:no\.?)?\s*)?(?<train>[0-9][0-9A-Z]{2,5}))?$", I);
    private static readonly Regex DateRx = new(@"\b\d{1,2}[./-]\d{1,2}[./-]\d{2,4}\b");
    private static readonly Regex MastNote = new(@"\(\s*\d{1,4}\.\d{3}(?:\s*-\s*\d{1,4}\.\d{3})?\s*km\s*\)", RegexOptions.IgnoreCase);

    private static readonly HashSet<string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        "date", "date of inspection", "section", "train no", "train", "inspected by", "inspecting official", "designation", "mobile",
        "mob", "type of inspection", "inspection", "route", "locomotive", "engine no", "engine", "loco no", "cab", "loco pilot", "lp",
        "sr assistant loco pilot", "assistant loco pilot", "sr alp", "alp", "train manager", "tm", "guard", "visibility", "weather"
    };
    private static readonly HashSet<string> TableHeaders = new(StringComparer.OrdinalIgnoreCase) { "name", "hq", "designation", "s no", "sno" };

    public static ParseResult Parse(string text, Profile profile, Masters masters, string source = "Text")
    {
        var res = new ParseResult();
        foreach (var chunk in SplitMessages(text))
        {
            if (!LooksLikeReport(chunk)) continue;
            var r = ParseOne(chunk, profile, masters);
            r.Source = source;
            res.Reports.Add(r);
            res.Messages.Add(Describe(r));
        }
        if (res.Reports.Count == 0)
            res.Messages.Add("No inspection report found in the text. A report needs an 'Inspection' line or title, and 'Observations'.");
        return res;
    }

    public static string Describe(InspectionReport r) =>
        $"{Fmt.Date(r.Date)}  {r.TrainNo}  {r.Route}  {Fmt.Time(r.Departure)}-{Fmt.Time(r.Arrival)}: " +
        $"{r.Sightings.Count} field staff, {r.Observations.Count} observations, {r.Shortcomings.Count} shortcomings, {r.Locations.Count} locations";

    /// <summary>Splits a WhatsApp chat / pasted text into one block per message, and two reports in one message apart.</summary>
    public static List<string> SplitMessages(string text)
    {
        var blocks = new List<StringBuilder>();
        StringBuilder? cur = null;
        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Replace(' ', ' ').Replace(' ', ' ').TrimEnd();
            var m = WaBracket.Match(line);
            if (!m.Success) m = WaDash.Match(line);
            if (m.Success)
            {
                cur = new StringBuilder();
                blocks.Add(cur);
                line = m.Groups[1].Value;
            }
            if (cur is null) { cur = new StringBuilder(); blocks.Add(cur); }
            cur.Append(line).Append('\n');
        }

        var result = new List<string>();
        foreach (var b in blocks)
        {
            var part = new StringBuilder();
            bool seenBody = false;
            foreach (var line in b.ToString().Split('\n'))
            {
                var t = line.Trim();
                bool startsNew = Salutation.IsMatch(t) || Regex.IsMatch(t, @"^date\s*[:\-–]", I);
                if (startsNew && seenBody)
                {
                    result.Add(part.ToString());
                    part.Clear();
                    seenBody = false;
                }
                if (Regex.IsMatch(t, @"^(inspection|observations?)\b", I)) seenBody = true;
                part.Append(line).Append('\n');
            }
            result.Add(part.ToString());
        }
        return result.Where(s => s.Trim().Length > 0).ToList();
    }

    public static bool LooksLikeReport(string chunk) =>
        Regex.IsMatch(chunk, @"inspection|footplate", I) &&
        Regex.IsMatch(chunk, @"observation|departure|arrival|short\s*-?comings?|locations?\s+to\s+be", I);

    private enum Part { Header, Observations, Shortcomings, Locations, Remarks, Action, End }

    private sealed class State
    {
        public required InspectionReport R;
        public Part Part = Part.Header;
        public string? Pending;
        public string? PendingHqFor;
        public bool LocTable;
        /// <summary>Column expected next in the locations table: 1 location, 2 nature, 3 compliance (after a serial number).</summary>
        public int LocCol;
        public readonly List<StringBuilder> Items = new();
        public readonly StringBuilder LocText = new();
    }

    public static InspectionReport ParseOne(string chunk, Profile profile, Masters masters)
    {
        var r = new InspectionReport
        {
            Kind = "", Purposes = new List<string>(), Visibility = "", FieldStaffNote = "", AlpDesignation = "", TmDesignation = ""
        };
        var st = new State { R = r };
        var remarks = new StringBuilder();
        var action = new StringBuilder();

        foreach (var raw in chunk.Split('\n'))
        {
            var t = MastNote.Replace(raw, "").Trim();   // "(35.492 - 35.825 km)" printed under mast locations by this app
            if (t.Length == 0 || Salutation.IsMatch(t) || IsNoise(t)) continue;
            if (st.Part == Part.End) continue;

            Match m;
            if (st.Part != Part.Locations && Regex.IsMatch(t, Num + @"locations?\b", I) && (m = LocHead.Match(t)).Success)
            {
                st.Part = Part.Locations;
                var colon = t.IndexOf(':');
                if (colon >= 0) st.LocText.Append(t[(colon + 1)..]).Append(", ");   // list written on the heading line itself
                continue;
            }
            if (st.Part is Part.Header or Part.Observations && Regex.IsMatch(t, Num + "short", I) && (m = ShortHead.Match(t)).Success)
            {
                st.Part = Part.Shortcomings;
                t = m.Groups[1].Value.Trim();
                if (t.Length == 0) continue;
            }
            else if (st.Part == Part.Header && Regex.IsMatch(t, Num + "observation", I) && (m = ObsHead.Match(t)).Success)
            {
                st.Part = Part.Observations;
                st.Pending = null;
                t = m.Groups[1].Value.Trim();
                if (t.Length == 0) continue;
            }
            else if (st.Part is Part.Observations or Part.Shortcomings or Part.Locations && Regex.IsMatch(t, @"^\s*\d{1,2}\s*[.)]\s*remarks?\b|^\s*remarks?\s*[:\-–]", I) && (m = RemarksHead.Match(t)).Success)
            {
                st.Part = Part.Remarks;
                t = m.Groups[1].Value.Trim();
                if (t.Length == 0) continue;
            }
            else if (st.Part is Part.Shortcomings or Part.Locations or Part.Remarks && (m = ActionHead.Match(t)).Success)
            {
                st.Part = Part.Action;
                t = m.Groups[1].Value.Trim();
                if (t.Length == 0) continue;
            }

            if (st.Part != Part.Header && IsSignature(t, r, profile))
            {
                st.Part = Part.End;
                continue;
            }

            switch (st.Part)
            {
                case Part.Header:
                    HeaderLine(st, t);
                    break;
                case Part.Observations:
                    var n = Numbered.Match(t);
                    if (n.Success) st.Items.Add(new StringBuilder(n.Groups[1].Value));
                    else if (st.Items.Count == 0) st.Items.Add(new StringBuilder(t));
                    else st.Items[^1].Append('\n').Append(t);
                    break;
                case Part.Shortcomings:
                    var s = Regex.Replace(t, @"^(\d{1,2}\s*[.)]|[-•*])\s*", "").Trim().TrimEnd(',', ';');
                    if (s.Length == 0 || Regex.IsMatch(s, @"^(nil|none|no shortcomings?)\.?$", I)) break;
                    if (r.Shortcomings.Count > 0 && !Numbered.IsMatch(t) && char.IsLower(s[0]))
                        r.Shortcomings[^1] = r.Shortcomings[^1].TrimEnd('.') + " " + Fmt.EndWith(s, '.');   // wrapped line
                    else r.Shortcomings.Add(Fmt.EndWith(s, '.'));
                    break;
                case Part.Locations:
                    LocationLine(st, t);
                    break;
                case Part.Remarks:
                    remarks.Append(t).Append(' ');
                    break;
                case Part.Action:
                    action.Append(t).Append(' ');
                    break;
            }
        }

        foreach (var item in st.Items) Observation(r, item.ToString(), masters);
        r.Locations.InsertRange(0, Locations.ParseList(st.LocText.ToString()));
        r.Remarks = remarks.ToString().Trim();
        r.ActionTaken = action.ToString().Trim();

        if (r.InspectorName.Length == 0) r.InspectorName = profile.Name;
        if (r.Designation.Length == 0) r.Designation = profile.Designation;
        if (r.Mobile.Length == 0) r.Mobile = profile.Mobile;
        if (r.Section.Length == 0) r.Section = profile.Section;
        if (r.Kind.Length == 0) r.Kind = "Footplate Inspection";
        if (r.AlpDesignation.Length == 0) r.AlpDesignation = "Sr. ALP";
        if (r.TmDesignation.Length == 0) r.TmDesignation = "TM";
        return r;
    }

    private static bool IsNoise(string t) =>
        Regex.IsMatch(t, @"^page\s*\d+(\s*of\s*\d+)?$", I) ||
        Regex.IsMatch(t, @"^crew\s+on\s+board\s*:?$", I) ||
        (Regex.IsMatch(t, @"foot\s*plate", I) && DateRx.IsMatch(t) && !t.Contains(':') && (t.Contains('|') || t.Contains('–')));

    private static bool IsSignature(string t, InspectionReport r, Profile p)
    {
        var x = t.Trim().Trim('(', ')', ' ', '.');
        if (x.Length == 0) return false;
        foreach (var name in new[] { r.InspectorName, p.Name })
            if (name.Length > 3 && string.Equals(Fmt.CleanName(x), name, StringComparison.OrdinalIgnoreCase)) return true;
        return t.StartsWith('(') && t.EndsWith(')') && Regex.IsMatch(x, @"^[A-Z][a-z]+ [A-Z][A-Za-z. ]{2,35}$") && !Regex.IsMatch(x, @"\b(sej|rr|curve|km)\b", I);
    }

    private static string Clean(string v) => v.Trim().TrimEnd(',', ';', ' ').Trim();

    private static string NormLabel(string s)
    {
        var k = Regex.Replace(s, @"\([^)]*\)", " ");
        k = k.Replace(".", " ").Replace("’", "'").Trim().TrimEnd(':').ToLowerInvariant();
        return Regex.Replace(k, @"\s+", " ").Trim();
    }

    private static bool IsLabel(string t, out string label)
    {
        label = NormLabel(t);
        if (t.Contains(':')) return false;
        if (Labels.Contains(label)) return true;
        if (Regex.IsMatch(t, @"^[A-Z]{2,5}(?:'[A-Z]')?\s+(departure|arrival)$", I)) return true;
        if (Regex.IsMatch(t, @"^(departure\s+from|arrival\s+at)\s+[A-Z]{2,5}(?:'[A-Z]')?$", I)) return true;
        return false;
    }

    private static void HeaderLine(State st, string t)
    {
        var r = st.R;
        t = Regex.Replace(t, @"^[•●▪◦\-*]\s*", "").Trim();
        if (t.Length == 0) return;

        // HQ written below the crew member's name (crew table).
        if (st.PendingHqFor is string role)
        {
            st.PendingHqFor = null;
            if (!IsLabel(t, out _) && !TableHeaders.Contains(NormLabel(t)) && Regex.IsMatch(t, @"^[A-Za-z][A-Za-z .']{1,20}$") && !t.Contains("Shri"))
            {
                SetHq(r, role, t);
                return;
            }
        }

        // Value written below its label (tables in typed notes).
        if (st.Pending is string pending)
        {
            if (TableHeaders.Contains(NormLabel(t)) && !IsLabel(t, out _)) { st.Pending = null; return; }
            st.Pending = null;
            if (!IsLabel(t, out _))
            {
                Apply(st, pending, t);
                return;
            }
        }

        var title = TitleLine.Match(t);
        if (title.Success)
        {
            r.Kind = NormKind(title.Groups["kind"].Value);
            if (title.Groups["p"].Success) r.Purposes = SplitPurposes(title.Groups["p"].Value);
            return;
        }
        var paren = ParenOnly.Match(t);
        if (paren.Success && r.Kind.Length > 0 && r.Purposes.Count == 0)
        {
            r.Purposes = SplitPurposes(paren.Groups["p"].Value);
            return;
        }

        if (t.Contains('|'))
        {
            foreach (var p in t.Split('|')) HeaderLine(st, p.Trim());
            return;
        }

        var route = RouteRx.Match(t);
        if (route.Success && Regex.IsMatch(t, @"^(ex|from)\s", I))
        {
            r.FromStation = route.Groups["from"].Value.ToUpperInvariant();
            r.ToStation = route.Groups["to"].Value.ToUpperInvariant();
            if (route.Groups["train"].Success && r.TrainNo.Length == 0) r.TrainNo = route.Groups["train"].Value;
            return;
        }

        if (IsLabel(t, out _))
        {
            st.Pending = t;
            return;
        }

        foreach (var (rx, key) in InlineLabels)
        {
            var im = rx.Match(t);
            if (im.Success) { Apply(st, key.Replace("{0}", im.Groups[1].Value), im.Groups[2].Value); return; }
        }

        var lm = LocoLine.Match(t);
        if (lm.Success)
        {
            ParseLoco(r, lm.Groups[1].Value);
            return;
        }
        var kv = KeyValue.Match(t);
        if (kv.Success) Apply(st, kv.Groups[1].Value, kv.Groups[2].Value);
    }

    private static readonly (Regex Rx, string Key)[] InlineLabels =
    {
        (new Regex(@"^date\s+of\s+inspection()\s+(\d.+)$", I), "date"),
        (new Regex(@"^train\s+no\.?()\s+(\d.+)$", I), "train no"),
        (new Regex(@"^departure\s+from\s+([A-Z]{2,5})\s+(\d.+)$", I), "{0} departure"),
        (new Regex(@"^arrival\s+at\s+([A-Z]{2,5})\s+(\d.+)$", I), "{0} arrival"),
        (new Regex(@"^([A-Z]{2,5})\s+departure\s+(\d.+)$", I), "{0} departure"),
        (new Regex(@"^([A-Z]{2,5})\s+arrival\s+(\d.+)$", I), "{0} arrival"),
        (new Regex(@"^mobile()\s+(\d{10}.*)$", I), "mobile"),
        (new Regex(@"^cab()\s+(cab.*|no.*|\d)$", I), "cab"),
    };

    private static void Apply(State st, string keyRaw, string value)
    {
        var r = st.R;
        var key = NormLabel(keyRaw);
        var val = Clean(value);
        var valNoDot = val.TrimEnd('.').Trim();
        if (val.Length == 0) { st.Pending = keyRaw; return; }
        if (valNoDot is "-" or "–" or "--" or "NA" or "N/A" or "Nil") return;

        switch (key)
        {
            case "date" or "dated" or "date of inspection":
                if (Fmt.ParseDate(val) is DateOnly d) r.Date = d;
                return;
            case "name" or "name of official" or "inspected by" or "inspecting official":
                var c = valNoDot.IndexOf(',');
                if (c > 0 && key != "name")
                {
                    r.InspectorName = Fmt.CleanName(valNoDot[..c]);
                    if (r.Designation.Length == 0) r.Designation = valNoDot[(c + 1)..].Trim();
                }
                else r.InspectorName = Fmt.CleanName(valNoDot);
                return;
            case "designation" or "desig" or "desg":
                r.Designation = valNoDot;
                return;
            case "mobile" or "mob" or "mobile no" or "mob no" or "contact" or "phone" or "cug" or "cug no":
                r.Mobile = valNoDot;
                return;
            case "section":
                r.Section = valNoDot;
                return;
            case "inspection" or "type of inspection" or "nature of inspection":
                ParseInspection(r, valNoDot);
                return;
            case "route":
                var rm = RouteRx.Match(valNoDot);
                if (rm.Success)
                {
                    r.FromStation = rm.Groups["from"].Value.ToUpperInvariant();
                    r.ToStation = rm.Groups["to"].Value.ToUpperInvariant();
                }
                return;
            case "train" or "train no" or "train number" or "by train":
                var tm = Regex.Match(valNoDot, @"^([0-9][0-9A-Z]{2,5})\s*(?:\((.*)\))?", I);
                if (tm.Success)
                {
                    r.TrainNo = tm.Groups[1].Value;
                    var rr = RouteRx.Match(tm.Groups[2].Value.Trim());
                    if (rr.Success)
                    {
                        r.FromStation = rr.Groups["from"].Value.ToUpperInvariant();
                        r.ToStation = rr.Groups["to"].Value.ToUpperInvariant();
                    }
                }
                return;
            case "visibility":
                r.Visibility = valNoDot;
                return;
            case "weather":
                r.Weather = valNoDot;
                return;
            case "engine" or "engine no" or "loco" or "loco no" or "locomotive" or "locomotive no":
                ParseLoco(r, val);
                return;
            case "cab" or "cab no":
                var cm = Regex.Match(val, @"(\d)");
                if (cm.Success) r.Cab = "Cab" + cm.Groups[1].Value;
                return;
            case "lp" or "loco pilot" or "lpm" or "lpp" or "lpg" or "driver":
                SetCrew(st, "lp", val);
                return;
        }
        if (Regex.IsMatch(key, @"\balp\b|assistant loco pilot"))
        {
            r.AlpDesignation = Regex.IsMatch(key, @"\bsr\b|senior") ? "Sr. ALP" : "ALP";
            SetCrew(st, "alp", val);
            return;
        }
        if (Regex.IsMatch(key, @"^(sr )?(tm|train manager|guard|gd)$"))
        {
            r.TmDesignation = key.Contains("guard") || key == "gd" ? "Guard" : key.StartsWith("sr") ? "Sr. TM" : "TM";
            SetCrew(st, "tm", val);
            return;
        }
        var dep = Regex.Match(key, @"^(?:([a-z']{2,6})\s+(?:departure|dep|dept|departed)|departure from ([a-z']{2,6}))$");
        if (dep.Success)
        {
            r.Departure = Fmt.ParseTime(valNoDot) ?? r.Departure;
            var code = (dep.Groups[1].Success ? dep.Groups[1].Value : dep.Groups[2].Value).ToUpperInvariant();
            if (r.FromStation.Length == 0) r.FromStation = code;
            return;
        }
        var arr = Regex.Match(key, @"^(?:([a-z']{2,6})\s+(?:arrival|arr|arrived)|arrival at ([a-z']{2,6}))$");
        if (arr.Success)
        {
            r.Arrival = Fmt.ParseTime(valNoDot) ?? r.Arrival;
            var code = (arr.Groups[1].Success ? arr.Groups[1].Value : arr.Groups[2].Value).ToUpperInvariant();
            if (r.ToStation.Length == 0) r.ToStation = code;
            return;
        }
        if (key is "departure" or "dep") r.Departure = Fmt.ParseTime(valNoDot) ?? r.Departure;
        if (key is "arrival" or "arr") r.Arrival = Fmt.ParseTime(valNoDot) ?? r.Arrival;
    }

    /// <summary>"Shri D.K. Mandal, Kota" = name and headquarters.</summary>
    private static void SetCrew(State st, string role, string value)
    {
        var v = value.Trim().TrimEnd('.', ',');
        var hq = "";
        var c = v.LastIndexOf(',');
        if (c > 0)
        {
            var tail = v[(c + 1)..].Trim();
            if (Regex.IsMatch(tail, @"^[A-Za-z][A-Za-z .']{1,20}$")) { hq = tail; v = v[..c]; }
        }
        var name = Fmt.CleanName(v);
        switch (role)
        {
            case "lp": st.R.LocoPilot = name; break;
            case "alp": st.R.AssistantLocoPilot = name; break;
            default: st.R.TrainManager = name; break;
        }
        if (hq.Length > 0) SetHq(st.R, role, hq);
        else st.PendingHqFor = role;
    }

    private static void SetHq(InspectionReport r, string role, string hq)
    {
        hq = hq.Trim().TrimEnd('.', ',');
        switch (role)
        {
            case "lp": r.LocoPilotHq = hq; break;
            case "alp": r.AlpHq = hq; break;
            default: r.TmHq = hq; break;
        }
    }

    private static string NormKind(string kind)
    {
        kind = Regex.Replace(kind, @"\s+", " ").Trim().TrimEnd('-', ',', '.').Trim();
        if (Regex.IsMatch(kind, @"night", I) && Regex.IsMatch(kind, @"foot\s*-?\s*plate", I)) return "Night Footplate Inspection";
        if (Regex.IsMatch(kind, @"\bday\b", I) && Regex.IsMatch(kind, @"foot\s*-?\s*plate", I)) return "Day Footplate Inspection";
        if (Regex.IsMatch(kind, @"surprise", I) && Regex.IsMatch(kind, @"foot\s*-?\s*plate", I)) return "Surprise Footplate Inspection";
        if (Regex.IsMatch(kind, @"^foot\s*-?\s*plate(\s+inspection)?$", I)) return "Footplate Inspection";
        return kind;
    }

    private static List<string> SplitPurposes(string p) =>
        p.Split(new[] { '+', ',', '&', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant())
            .ToList();

    private static readonly Regex InspectionRx = new(
        @"^(?<kind>.*?)\s*(?:\((?<p>[^)]*)\))?\s*(?:(?i:ex|from)\s+)?(?<from>[A-Z]{2,5}(?:'[A-Z]')?)\s*(?:(?i:to)|-|–)\s*(?<to>[A-Z]{2,5}(?:'[A-Z]')?)\b(?:\s*(?i:by|in)\s+(?:(?i:train)\s*(?:(?i:no)\.?)?\s*)?(?<train>[0-9][0-9A-Z]{2,5}))?(?<rest>.*)$");

    public static void ParseInspection(InspectionReport r, string v)
    {
        var m = InspectionRx.Match(v);
        string kind;
        string purposes;
        if (m.Success && m.Groups["kind"].Value.Trim().Length > 0)
        {
            kind = m.Groups["kind"].Value;
            purposes = m.Groups["p"].Value;
            r.FromStation = m.Groups["from"].Value;
            r.ToStation = m.Groups["to"].Value;
            if (m.Groups["train"].Success) r.TrainNo = m.Groups["train"].Value;
            var rest = m.Groups["rest"].Value.Trim().Trim('(', ')', '.', ' ');
            if (rest.Length > 2 && r.TrainName.Length == 0 && Regex.IsMatch(rest, "[A-Za-z]{3}")) r.TrainName = rest;
        }
        else
        {
            var p = v.IndexOf('(');
            kind = p > 0 ? v[..p] : v;
            var q = p >= 0 ? v.IndexOf(')', p) : -1;
            purposes = p >= 0 && q > p ? v[(p + 1)..q] : "";
            var by = Regex.Match(v, @"\bby\s+(?:train\s*(?:no\.?)?\s*)?([0-9][0-9A-Z]{2,5})", I);
            if (by.Success) r.TrainNo = by.Groups[1].Value;
        }
        r.Kind = NormKind(kind);
        if (purposes.Trim().Length > 0) r.Purposes = SplitPurposes(purposes);
    }

    public static void ParseLoco(InspectionReport r, string v)
    {
        var parts = v.Trim().TrimEnd('.', ',', ';').Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (r.LocoNo.Length == 0 && Regex.IsMatch(p, @"^\d{4,6}$")) { r.LocoNo = p; continue; }
            var cab = Regex.Match(p, @"^cab\s*(?:no\.?)?\s*[-#]?\s*(\d)$", I);
            if (cab.Success) { r.Cab = "Cab" + cab.Groups[1].Value; continue; }
            if (r.LocoType.Length == 0 && Regex.IsMatch(p, @"^(W[A-Z]{2}\s*-?\s*\d{1,2}[A-Z]{0,2}|MEMU|EMU|DEMU)$", I))
            {
                r.LocoType = Regex.Replace(p.ToUpperInvariant(), @"[\s-]", "");
                continue;
            }
            if (r.LocoNo.Length == 0 && Regex.IsMatch(p, @"^\d{4,6}\s*[A-Z]?$", I)) { r.LocoNo = p; continue; }
            if (r.Shed.Length == 0 && Regex.IsMatch(p, @"^[A-Za-z]{2,5}$")) { r.Shed = p.ToUpperInvariant(); continue; }
        }
    }

    // ------------------------------------------------------------ locations

    private static void LocationLine(State st, string t)
    {
        var r = st.R;
        if (Regex.IsMatch(t, @"^(s\.?\s*no\.?|location\s*(\(km\))?|nature\s+of|compliance)\b", I) && !Locations.IsKm(t))
        {
            st.LocTable = true;
            return;
        }
        if (!st.LocTable)
        {
            st.LocText.Append(t).Append(", ");
            return;
        }
        // Mast / chainage line printed under the location by this app (it may wrap into pieces).
        if (Regex.IsMatch(t, @"^(OHE\s+\d|ch\.\s*\d)", I) ||
            Regex.IsMatch(t, @"^\d{1,3}\.\d{3}(\s*-\s*\d{1,3}\.\d{3})?(\s*km)?\)?$", I)) return;
        if (Regex.IsMatch(t, @"^\d{1,3}\.?$")) { st.LocCol = 1; return; }   // serial number cell: a new row follows
        var last = r.Locations.Count > 0 ? r.Locations[^1] : null;
        bool looksLoc = Regex.IsMatch(t, @"^KM\b", I) || Locations.IsKm(t);
        if (st.LocCol == 1 || (st.LocCol == 0 && (looksLoc || (Regex.IsMatch(t, @"^[A-Z]{2,5}(?:'[A-Z]')?\b") && (last is null || last.Feature.Length > 0)))))
        {
            r.Locations.AddRange(Locations.ParseList(t));
            if (st.LocCol == 1) st.LocCol = 2;
            return;
        }
        if (looksLoc && st.LocCol >= 2) { r.Locations.AddRange(Locations.ParseList(t)); st.LocCol = 2; return; }
        if (last is null) return;
        if (st.LocCol == 3 || Regex.IsMatch(t, @"^(attended|complied|done|ok)\b", I))
        {
            if (Regex.IsMatch(t, @"^(attended|complied|done|ok)\b", I)) last.Status = LocationStatus.Attended;
            else if (Regex.IsMatch(t, @"under\s+watch", I)) last.Status = LocationStatus.Watch;
            st.LocCol = 4;
            return;
        }
        if (st.LocCol == 2) st.LocCol = 3;
        if (Regex.IsMatch(t, @"^to\s+be\s+attended(\s+for\s+better\s+running)?\.?$", I)) return;
        last.Feature = last.Feature.Length == 0 ? t.TrimEnd('.') : last.Feature + " " + t.TrimEnd('.');
    }

    // ------------------------------------------------------------ observations

    private static readonly Regex StaffWords = new(@"gate\s*keeper|watch\s*m[ae]n|patrol\s*m[ae]n|key\s*m[ae]n|gate\s*m[ae]n", I);
    private static readonly Regex WeatherWords = new(@"\b(rain|raining|drizzle|shower|fog|foggy|mist|misty|haz[ey]|storm|thunder|wind|windy|dust|cloudy|lightning)\b", I);
    private static readonly Regex Heading = new(@"^([A-Za-z][A-Za-z /&]{2,45}):\s*(.+)$", RegexOptions.Singleline);

    private static void Observation(InspectionReport r, string item, Masters masters)
    {
        var flat = Regex.Replace(item, @"\s+", " ").Trim();
        if (flat.Length == 0) return;
        var checks = Regex.Match(flat, @"^items\s+checked[^:]*:\s*(.+)$", I);
        if (checks.Success)
        {
            foreach (var part in checks.Groups[1].Value.TrimEnd('.').Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!r.Checks.Contains(part)) r.Checks.Add(part);
            return;
        }
        if (StaffWords.IsMatch(flat) && ParseStaff(r, item)) return;

        var body = flat;
        var h = Heading.Match(flat);
        if (h.Success)
        {
            body = h.Groups[2].Value.Trim();
            var head = h.Groups[1].Value.Trim();
            if (Regex.IsMatch(head, @"^visibility$", I)) { r.Visibility = body.TrimEnd('.'); return; }
            if (Regex.IsMatch(head, @"^weather$", I)) { AddWeather(r, body); return; }
            if (Regex.IsMatch(head, @"section|visibility|weather", I))
            {
                foreach (var sentence in Regex.Split(body, @"(?<=\.)\s+(?=[A-Z])"))
                    Single(r, sentence.Trim(), masters);
                return;
            }
        }
        Single(r, body, masters);
    }

    private static void Single(InspectionReport r, string flat, Masters masters)
    {
        if (flat.Length == 0) return;
        var vis = Regex.Match(flat, @"^visibility\s*(?:was|is|remained)?\s*[:\-–]?\s*(.*)$", I);
        if (vis.Success) { r.Visibility = vis.Groups[1].Value.Trim().TrimEnd('.'); return; }
        var wea = Regex.Match(flat, @"^weather\s*[:\-–]\s*(.*)$", I);
        if (wea.Success) { AddWeather(r, wea.Groups[1].Value); return; }
        if (Regex.IsMatch(flat, @"^weather\b", I)) { AddWeather(r, flat); return; }
        var std = MatchStandard(flat, masters);
        if (std is not null)
        {
            if (!r.Observations.Contains(std)) r.Observations.Add(std);
            return;
        }
        if (WeatherWords.IsMatch(flat) && flat.Length < 140) { AddWeather(r, flat); return; }
        r.Observations.Add(Fmt.EndWith(flat, '.'));
    }

    private static void AddWeather(InspectionReport r, string text)
    {
        var t = text.Trim().TrimEnd('.');
        r.Weather = r.Weather.Length == 0 ? t : r.Weather + "; " + t;
    }

    /// <summary>The standard observation this text says (same words, small differences allowed), or null.</summary>
    public static string? MatchStandard(string text, Masters masters)
    {
        var a = Words(text);
        if (a.Count == 0) return null;
        string? best = null;
        double bestScore = 0;
        foreach (var s in masters.StandardObservations)
        {
            var b = Words(s);
            if (b.Count == 0) continue;
            double inter = a.Intersect(b).Count();
            var score = inter / a.Union(b).Count();
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return bestScore >= 0.5 ? best : null;
    }

    private static readonly HashSet<string> StopWords = new() { "the", "and", "they", "were", "was", "also", "of", "to", "with", "etc", "en", "route", "during", "run" };

    private static HashSet<string> Words(string s) =>
        Regex.Matches(s.ToLowerInvariant(), "[a-z]{2,}").Select(m => m.Value).Where(w => !StopWords.Contains(w)).ToHashSet();

    // ------------------------------------------------------------ field staff

    private static readonly (Regex Rx, string Category)[] Categories =
    {
        (new Regex(@"monsoon\s*patrol\s*m[ae]n", I), "Monsoon Patrolman"),
        (new Regex(@"hot\s*weather\s*patrol\s*m[ae]n", I), "Hot Weather Patrolman"),
        (new Regex(@"cold\s*weather\s*patrol\s*m[ae]n|\bCWP\b|winter\s*patrol\s*m[ae]n", I), "Cold Weather Patrolman"),
        (new Regex(@"night\s*patrol\s*m[ae]n", I), "Night Patrolman"),
        (new Regex(@"stationary\s*watch\s*m[ae]n", I), "Stationary Watchman"),
        (new Regex(@"bridge\s*watch\s*m[ae]n", I), "Bridge Watchman"),
        (new Regex(@"(?<!stationary\s*)(?<!bridge\s*)watch\s*m[ae]n", I), "Stationary Watchman"),
        (new Regex(@"gate\s*(?:keeper|m[ae]n)s?|\bGKs?\b", I), "Gate Keeper"),
        (new Regex(@"key\s*m[ae]n", I), "Keyman"),
        (new Regex(@"(?<!monsoon\s*)(?<!weather\s*)(?<!night\s*)patrol\s*m[ae]n", I), "Patrolman"),
    };

    private const string NosTail = @"(?:\s*(?:nos|no)\b\.?)?";
    private static readonly Regex[] HitPatterns =
    {
        new(@"(?:\bKM\.?\s*)?(?<loc>\d{1,4}\s*/\s*\d{1,2}(?:\s*-\s*\d{1,2})?)_?\s*[-–]\s*(?<n>\d{1,2})(?!\s*/)" + NosTail, I),
        new(@"(?:\bKM\.?\s*)?(?<loc>\d{1,4}\s*/\s*\d{1,2}(?:\s*-\s*\d{1,2})?)\s*\(\s*(?<n>\d{1,2})\s*(?:nos|no)\b\.?\s*\)?", I),
        new(@"\bLC\s*[-.]?\s*(?:No\.?\s*)?(?<lc>\d{1,3}[A-Z]?)(?:\s*[-–]\s*(?<n>\d{1,2})" + NosTail + ")?", I),
        new(@"\b(?:(?i:at)\s+)?(?<loc>[A-Z]{2,5}(?:'[A-Z]')?)\s*[-–]\s*(?<n>\d{1,2})(?!\s*/)(?:\s*(?i:nos|no)\b\.?)?"),
        new(@"\b(?:(?i:at)\s+)?(?<loc>[A-Z]{2,5}(?:'[A-Z]')?)(?:\s+(?i:station|yard))?\s*\(\s*(?<n>\d{1,2})\s*(?i:nos|no)\b\.?"),
        // Table rows read from a typed note: "Stationary Watchman KM 40/14 02 Alert Worn"
        new(@"\bKM\.?\s*(?<loc>\d{1,4}\s*/\s*\d{1,2}(?:\s*-\s*\d{1,2})?)\s+(?<n>\d{1,2})(?![\d/])", I),
        new(@"(?<=(?i:man|men|keeper|keepers)\s)(?<loc>[A-Z]{2,5}(?:'[A-Z]')?)\s+(?<n>\d{1,2})(?![\d/])"),
        new(@"\bKM\.?\s*(?<loc>\d{1,4}\s*/\s*\d{1,2}(?:\s*-\s*\d{1,2})?)", I),
    };
    private static readonly HashSet<string> NotStations = new(StringComparer.OrdinalIgnoreCase)
    {
        "KM", "LC", "TP", "SEJ", "GK", "GKS", "SM", "SMS", "LP", "ALP", "TM", "PSR", "PSRS", "CD", "CDS", "AT", "NO", "NOS", "RR", "CWP", "HG"
    };
    private static readonly HashSet<string> KnownWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alert", "and", "with", "in", "reflective", "jacket", "jackets", "put", "on", "proper", "without", "sleeping",
        "not", "found", "were", "was", "the", "a", "wearing", "worn", "no", "inside", "cabin", "both", "all", "also",
        "his", "their", "duty", "at", "following", "locations", "location", "station", "nos"
    };

    /// <summary>
    /// Reads the field staff item ("Stationary watchmen ( KM 40/14-02, 115/02-02 ...) and Monsoon Patrolmen at i KM 48/17-02nos alert, ii. ...").
    /// Returns false when no staff location is found and the item is better kept as a plain observation.
    /// </summary>
    public static bool ParseStaff(InspectionReport r, string item)
    {
        var text = Regex.Replace(item, @"\s+", " ").Replace('—', '–');

        var cats = new List<(int Index, int End, string Cat)>();
        foreach (var (rx, cat) in Categories)
            foreach (Match m in rx.Matches(text))
                if (!cats.Any(c => m.Index < c.End && c.Index < m.Index + m.Length))
                    cats.Add((m.Index, m.Index + m.Length, cat));
        cats.Sort((a, b) => a.Index.CompareTo(b.Index));

        var hits = new List<(int Index, int End, string Loc, int Count)>();
        foreach (var rx in HitPatterns)
            foreach (Match m in rx.Matches(text))
            {
                int idx = m.Index, end = m.Index + m.Length;
                if (hits.Any(h => idx < h.End && h.Index < end) || cats.Any(c => idx < c.End && c.Index < end)) continue;
                string loc;
                if (m.Groups["lc"].Success) loc = "LC " + m.Groups["lc"].Value;
                else
                {
                    loc = Regex.Replace(m.Groups["loc"].Value, @"\s", "");
                    if (NotStations.Contains(loc)) continue;
                }
                var n = m.Groups["n"].Success ? int.Parse(m.Groups["n"].Value, Fmt.Inv) : 1;
                hits.Add((idx, end, loc, n));
            }
        hits.Sort((a, b) => a.Index.CompareTo(b.Index));

        if (hits.Count == 0)
        {
            if (r.Sightings.Count > 0 || r.FieldStaffNote.Length > 0) return false;
            r.FieldStaffNote = Fmt.EndWith(text.Trim(), '.');
            return true;
        }

        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            var catMatch = cats.LastOrDefault(c => c.Index < h.Index);
            var cat = catMatch.Cat ?? "Field staff";
            int stop = i + 1 < hits.Count ? hits[i + 1].Index : text.Length;
            var nextCat = cats.FirstOrDefault(c => c.Index >= h.End && c.Index < stop);
            if (nextCat.Cat is not null) stop = nextCat.Index;
            var s = new StaffSighting { Category = cat, Location = h.Loc, Count = Math.Max(1, h.Count), Condition = "", Jacket = "" };
            Describe(s, text[h.End..stop], keepRemarks: true);

            // "... were found alert and wearing reflective jackets at the following locations:" applies to the list after it.
            if (catMatch.Cat is not null && (s.Condition.Length == 0 || s.Jacket.Length == 0))
            {
                var firstHit = hits.First(x => x.Index > catMatch.Index);
                var lead = new StaffSighting { Condition = "", Jacket = "" };
                Describe(lead, text[catMatch.End..firstHit.Index], keepRemarks: false);
                if (s.Condition.Length == 0) s.Condition = lead.Condition;
                if (s.Jacket.Length == 0) s.Jacket = lead.Jacket;
            }
            r.Sightings.Add(s);
        }

        if (r.FieldStaffNote.Length == 0)
        {
            var mentioned = cats.Select(c => c.Cat).Distinct().ToList();
            r.FieldStaffNote = mentioned.Count > 0
                ? $"En-route {JoinAnd(mentioned.Select(Plural))} were checked as under:"
                : "En-route field staff were checked as under:";
        }
        return true;
    }

    private static void Describe(StaffSighting s, string tail, bool keepRemarks)
    {
        var c = tail;
        for (int k = 0; k < 3; k++)
        {
            c = c.Trim().TrimEnd(',', '.', ';', ' ', '(', '-', '|', '•');
            c = Regex.Replace(c, @"(?:^|[\s,.;])(?:[ivx]{1,4})\s*[.)]?\s*$", "", I);
            c = Regex.Replace(c, @"\b(and|at|in|&)\s*$", "", I);
            c = Regex.Replace(c, @"(?:^|\s)(?:\d{1,3}|-|–)\s*$", "");   // next row's serial number / empty cells of a table
        }
        c = Regex.Replace(c, @"(?:^|\s)[-–](?=\s|$)", " ");
        c = c.Replace("(", " ").Replace(")", " ").Replace("|", " ").Replace("•", " ");
        c = Regex.Replace(c, @"\s+", " ").Trim().Trim(',', '.', ';', '-', '_', ' ', ':');

        if (Regex.IsMatch(c, @"without\s+(?:\w+\s+)?jackets?|no\s+(?:\w+\s+)?jackets?|not\s+(?:wearing|worn)|jackets?\s+not\s+(?:worn|put)", I)) s.Jacket = "Not worn";
        else if (Regex.IsMatch(c, @"jackets?|\bworn\b", I)) s.Jacket = "Worn";

        if (Regex.IsMatch(c, @"sleep", I)) s.Condition = "Sleeping";
        else if (Regex.IsMatch(c, @"not\s+alert|un-?alert", I)) s.Condition = "Not alert";
        else if (Regex.IsMatch(c, @"inside\s+(?:the\s+)?cabin", I)) s.Condition = "Inside cabin";
        else if (Regex.IsMatch(c, @"absent|not\s+found|not\s+available|did\s+not\s+appear", I)) s.Condition = "Absent";
        else if (Regex.IsMatch(c, @"\balert\b", I)) s.Condition = "Alert";

        if (!keepRemarks) return;
        var words = Regex.Matches(c, @"[A-Za-z0-9]+").Select(m => m.Value).ToList();
        if (words.Any(w => !KnownWords.Contains(w))) s.Remarks = c;
    }

    public static string Plural(string category) => category switch
    {
        _ when category.EndsWith("man", StringComparison.Ordinal) => category[..^3] + "men",
        _ when category.EndsWith("Keeper", StringComparison.Ordinal) => category + "s",
        _ => category
    };

    public static string JoinAnd(IEnumerable<string> items)
    {
        var l = items.ToList();
        return l.Count switch
        {
            0 => "",
            1 => l[0],
            _ => string.Join(", ", l.Take(l.Count - 1)) + " and " + l[^1]
        };
    }
}
