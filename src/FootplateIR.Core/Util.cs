using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

/// <summary>Formatting and parsing of dates and times in the railway style (21.07.2026, 0027hrs).</summary>
public static class Fmt
{
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Date(DateOnly d) => d.ToString("dd.MM.yyyy", Inv);

    /// <summary>0027hrs</summary>
    public static string Hrs(TimeOnly? t) => t is TimeOnly v ? v.ToString("HHmm", Inv) + "hrs" : "";

    /// <summary>00:27</summary>
    public static string Time(TimeOnly? t) => t is TimeOnly v ? v.ToString("HH:mm", Inv) : "";

    public static string Duration(TimeSpan? t) => t is TimeSpan v ? $"{(int)v.TotalHours}h {v.Minutes:00}m" : "";

    private static readonly Regex DateRx = new(@"(\d{1,2})\s*[./\-]\s*(\d{1,2})\s*[./\-]\s*(\d{2,4})", RegexOptions.Compiled);
    private static readonly Regex TimeRx = new(@"^(\d{1,2})\s*[:.]?\s*(\d{2})\s*(?:hrs?\.?|hours|h)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Reads 21.07.2026, 21/07/26, 21-7-2026 (day first, as written in India).</summary>
    public static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = DateRx.Match(s);
        if (!m.Success) return null;
        int d = int.Parse(m.Groups[1].Value, Inv), mo = int.Parse(m.Groups[2].Value, Inv), y = int.Parse(m.Groups[3].Value, Inv);
        if (y < 100) y += 2000;
        if (mo < 1 || mo > 12 || d < 1 || d > DateTime.DaysInMonth(y, mo)) return null;
        return new DateOnly(y, mo, d);
    }

    /// <summary>Reads 0027hrs, 00:27, 0:27 hrs, 2.44, 244.</summary>
    public static TimeOnly? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim().TrimEnd(',', '.', ';');
        var m = TimeRx.Match(t);
        if (!m.Success) return null;
        int h = int.Parse(m.Groups[1].Value, Inv), mi = int.Parse(m.Groups[2].Value, Inv);
        if (h == 24 && mi == 0) h = 0;
        if (h > 23 || mi > 59) return null;
        return new TimeOnly(h, mi);
    }

    /// <summary>Removes a leading "Shri"/"Sh."/"Smt." and trailing punctuation from a name.</summary>
    public static string CleanName(string? s)
    {
        var t = (s ?? "").Trim().TrimEnd(',', '.', ';', ' ');
        t = Regex.Replace(t, @"^(Shri|Sh\.?|Smt\.?|Mr\.?|Ms\.?|Kumari)\s+", "", RegexOptions.IgnoreCase);
        return Regex.Replace(t, @"\s{2,}", " ").Trim();
    }

    public static string WithShri(string name, bool useShri) =>
        string.IsNullOrWhiteSpace(name) ? "" : (useShri ? "Shri " + name : name);

    /// <summary>Roman numerals for sub-items (i, ii, iii ...).</summary>
    public static string Roman(int n)
    {
        string[] r = { "", "i", "ii", "iii", "iv", "v", "vi", "vii", "viii", "ix", "x", "xi", "xii", "xiii", "xiv", "xv", "xvi", "xvii", "xviii", "xix", "xx" };
        return n > 0 && n < r.Length ? r[n] : n.ToString(Inv);
    }

    public static string Nos(int n) => n == 1 ? "01 no." : $"{n:00} nos.";

    public static string EndWith(string s, char c)
    {
        s = s.Trim();
        if (s.Length == 0) return s;
        return s[^1] is '.' or ':' or '?' or '!' ? s : s + c;
    }
}

/// <summary>Track locations: KM/TP formats and grouping of the same spot reported more than once.</summary>
public static class Locations
{
    private static readonly Regex KmRx = new(@"^\s*(?:KM\.?\s*)?(\d{1,4})\s*/\s*(\d{1,2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsKm(string loc) => KmRx.IsMatch(loc ?? "");

    /// <summary>KM 095/04 for "95/04-05" and "95/04-03" alike; upper-case text for yard locations.</summary>
    public static string KeyOf(string loc)
    {
        var m = KmRx.Match(loc ?? "");
        if (m.Success)
            return $"KM {int.Parse(m.Groups[1].Value, Fmt.Inv):000}/{int.Parse(m.Groups[2].Value, Fmt.Inv):00}";
        return Regex.Replace((loc ?? "").ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
    }

    /// <summary>Numeric order along the section (KM first, then yards alphabetically).</summary>
    public static double SortValue(string loc)
    {
        var m = KmRx.Match(loc ?? "");
        if (!m.Success) return 1e9;
        return int.Parse(m.Groups[1].Value, Fmt.Inv) + int.Parse(m.Groups[2].Value, Fmt.Inv) / 100.0;
    }

    /// <summary>
    /// Splits "KM 35/10-15, 36/05-06, 61/12(SEJ), JNRI S3 to S4, 67/11." into locations.
    /// Commas inside brackets do not split; a bracketed word becomes the feature (SEJ).
    /// </summary>
    public static List<AttentionLocation> ParseList(string text)
    {
        var list = new List<AttentionLocation>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        var parts = new List<string>();
        var cur = new System.Text.StringBuilder();
        int depth = 0;
        foreach (var ch in text)
        {
            if (ch == '(') depth++;
            if (ch == ')') depth = Math.Max(0, depth - 1);
            if (depth == 0 && (ch == ',' || ch == ';' || ch == '\n' || ch == '\r'))
            {
                parts.Add(cur.ToString());
                cur.Clear();
            }
            else cur.Append(ch);
        }
        parts.Add(cur.ToString());
        foreach (var raw in parts)
        {
            var p = raw.Trim().TrimEnd('.', ' ').Trim();
            p = Regex.Replace(p, @"^(\d{1,2}[.)]|[ivx]{1,4}[.)])\s+", "", RegexOptions.IgnoreCase);   // numbering
            p = Regex.Replace(p, @"^(and|&)\s+", "", RegexOptions.IgnoreCase);
            p = Regex.Replace(p, @"^KM\.?\s*", "", RegexOptions.IgnoreCase).Trim();
            if (p.Length == 0) continue;
            var feature = "";
            var fm = Regex.Match(p, @"\(([^)]*)\)\s*$");
            if (fm.Success)
            {
                feature = fm.Groups[1].Value.Trim();
                p = p[..fm.Index].Trim();
            }
            p = Regex.Replace(p, @"\s{2,}", " ");
            if (p.Length == 0 && feature.Length > 0) { p = feature; feature = ""; }
            list.Add(new AttentionLocation { Location = p, Feature = feature });
        }
        return list;
    }

    /// <summary>"KM 35/10-15, 36/05-06, 61/12(SEJ), JNRI S3 to S4." (as written in the WhatsApp report)</summary>
    public static string Join(IEnumerable<AttentionLocation> locs)
    {
        var items = locs.Select(l => l.Location + (l.Feature.Length > 0 ? $"({l.Feature})" : "")).ToList();
        if (items.Count == 0) return "";
        var s = string.Join(", ", items);
        return (IsKm(items[0]) ? "KM " : "") + s + ".";
    }
}

/// <summary>Station codes and names: the built-in WCR (RBS) list plus stations added by the user.</summary>
public static class Stations
{
    private static readonly Lazy<Dictionary<string, string>> Names = new(Load);

    /// <summary>Kota - Chittaurgarh section in running order (KOTA end first).</summary>
    public static readonly string[] KotaCorSection =
    {
        "KOTA", "KCCN", "GQL", "GWCN", "DEIT", "THEA", "BUDI", "SNAR", "NIMA", "JNRI", "URML", "SMPA", "GNRD",
        "MLGH", "SISM", "BNDI", "PSLI", "JUYA", "BSSL", "TVTN", "CNA", "COR"
    };

    public static IReadOnlyDictionary<string, string> All => Names.Value;

    public static string? NameOf(string? code, Masters? m = null)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim().ToUpperInvariant();
        var extra = m?.ExtraStations.FirstOrDefault(s => string.Equals(s.Code, c, StringComparison.OrdinalIgnoreCase));
        if (extra is not null) return extra.Name;
        return Names.Value.TryGetValue(c, out var n) ? n : null;
    }

    public static bool IsKnown(string? code, Masters? m = null) => NameOf(code, m) is not null;

    /// <summary>Station codes the user is most likely to type first: the section, then the extras.</summary>
    public static IEnumerable<string> Suggestions(Masters? m = null) =>
        KotaCorSection.Concat(m?.ExtraStations.Select(s => s.Code) ?? Enumerable.Empty<string>()).Distinct();

    private static Dictionary<string, string> Load()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var s = typeof(Stations).Assembly.GetManifestResourceStream("FootplateIR.Core.rbs_wcr_network.txt");
        if (s is null) return d;
        using var r = new StreamReader(s);
        bool names = false;
        string? line;
        while ((line = r.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line == "#") { names = true; continue; }
            if (!names || line.Length == 0 || line.StartsWith('#')) continue;
            var i = line.IndexOf('=');
            if (i > 0) d[line[..i].Trim()] = line[(i + 1)..].Trim();
        }
        // Cabins and halts written in the reports that are not separate coaching stations.
        d.TryAdd("GQLW", "Gurla 'W' Cabin");
        return d;
    }
}
