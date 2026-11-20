using System.Globalization;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

/// <summary>One OHE mast: number (KM/mast), exact chainage, mast type and layout-plan sheet.</summary>
public sealed class OheMast
{
    public string Mast { get; set; } = "";
    public double Chainage { get; set; }
    public string Type { get; set; } = "";
    public string Drawing { get; set; } = "";
}

/// <summary>
/// OHE mast register. Built in: the OHE layout plans (LOP) of GQL - SMPA, KM 5 - 79 (1397 masts, same register as the
/// Track Machine Progress app). Masts of other stretches can be added from a CSV in the same format.
/// Footplate notes write locations as mast numbers ("KM 40/14"), so a mast found here gives the exact running KM.
/// </summary>
public static class OheMasts
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Regex MastRx = new(@"^\s*(?:KM\.?|OHE\.?|MAST|M)?\s*[-.:]?\s*(\d{1,4})\s*/\s*(\d{1,2}[A-Z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Lazy<List<OheMast>> BuiltIn = new(LoadBuiltIn);
    private static List<OheMast> _extra = new();
    private static Dictionary<string, OheMast>? _index;

    public const string Source = "OHE layout plans (LOP), GQL - SMPA";

    public static IReadOnlyList<OheMast> All => BuiltIn.Value.Concat(_extra).OrderBy(m => m.Chainage).ToList();
    public static int Count => BuiltIn.Value.Count + _extra.Count;

    /// <summary>Masts added in the app (other stretches); replaces the previous extra list.</summary>
    public static void SetExtra(IEnumerable<OheMast>? extra)
    {
        _extra = extra?.ToList() ?? new List<OheMast>();
        _index = null;
    }

    private static Dictionary<string, OheMast> Index()
    {
        if (_index is not null) return _index;
        var d = new Dictionary<string, OheMast>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in BuiltIn.Value.Concat(_extra)) d[Normal(m.Mast)] = m;   // added masts win over built-in ones
        return _index = d;
    }

    /// <summary>"31/05" → "31/5".</summary>
    public static string Normal(string mast)
    {
        var m = MastRx.Match(mast ?? "");
        if (!m.Success) return (mast ?? "").Trim().ToUpperInvariant();
        var a = m.Groups[1].Value.TrimStart('0');
        var b = m.Groups[2].Value.TrimStart('0');
        return $"{(a.Length > 0 ? a : "0")}/{(b.Length > 0 ? b : "0").ToUpperInvariant()}";
    }

    /// <summary>The mast written at the start of a location ("40/14", "KM 35/10-15", "OHE 61/12(SEJ)"), or null.</summary>
    public static OheMast? Find(string? location)
    {
        var m = MastRx.Match(location ?? "");
        return m.Success && Index().TryGetValue(Normal(m.Value), out var mast) ? mast : null;
    }

    /// <summary>Masts at both ends of "35/10-15" or "71/15-72/04" (the second may be missing from the register).</summary>
    public static (OheMast? From, OheMast? To) Range(string? location)
    {
        var s = (location ?? "").Trim();
        var from = Find(s);
        var r = Regex.Match(s, @"^\s*(?:KM\.?|OHE\.?|MAST|M)?\s*[-.:]?\s*(\d{1,4})\s*/\s*\d{1,2}[A-Z]?\s*-\s*(?:(\d{1,4})\s*/\s*)?(\d{1,2}[A-Z]?)\b", RegexOptions.IgnoreCase);
        OheMast? to = null;
        if (r.Success) Index().TryGetValue(Normal($"{(r.Groups[2].Success ? r.Groups[2].Value : r.Groups[1].Value)}/{r.Groups[3].Value}"), out to);
        return (from, to);
    }

    /// <summary>Nearest mast to a chainage (within <paramref name="withinKm"/>).</summary>
    public static OheMast? Nearest(double chainage, double withinKm = 0.05) =>
        BuiltIn.Value.Concat(_extra).Select(m => (m, d: Math.Abs(m.Chainage - chainage))).Where(x => x.d <= withinKm)
            .OrderBy(x => x.d).Select(x => x.m).FirstOrDefault();

    /// <summary>"OHE 35/10 - 35/15, ch. 35.620 - 35.950" for a location, or "" when the masts are not in the register.</summary>
    public static string Describe(string? location)
    {
        var (a, b) = Range(location);
        if (a is null) return "";
        if (b is null || ReferenceEquals(a, b)) return $"OHE {a.Mast}, ch. {a.Chainage:0.000}";
        return $"OHE {a.Mast} - {b.Mast}, ch. {a.Chainage:0.000} - {b.Chainage:0.000}";
    }

    /// <summary>Reads "Mast,Km,Metre,Chainage,MastType,Line,Drawing" (lines starting with # are comments). Chainage = KM + metre / 1000 when blank.</summary>
    public static List<OheMast> Parse(string csv)
    {
        var list = new List<OheMast>();
        string[]? head = null;
        foreach (var raw in (csv ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var c = line.Split(',', ';', '\t');
            if (head is null)
            {
                head = c.Select(h => h.Trim().ToLowerInvariant().Replace(" ", "")).ToArray();
                if (!head.Contains("mast")) { head = new[] { "mast", "km", "metre", "chainage", "masttype", "line", "drawing" }; }
                else continue;
            }
            string Col(string name) { var i = Array.IndexOf(head, name); return i >= 0 && i < c.Length ? c[i].Trim() : ""; }
            var mast = Col("mast");
            if (!MastRx.IsMatch(mast)) continue;
            double ch;
            if (!double.TryParse(Col("chainage"), NumberStyles.Float, Inv, out ch))
            {
                if (!int.TryParse(Col("km"), NumberStyles.Integer, Inv, out var km) || !double.TryParse(Col("metre"), NumberStyles.Float, Inv, out var m)) continue;
                ch = km + m / 1000.0;
            }
            list.Add(new OheMast { Mast = Normal(mast), Chainage = ch, Type = Col("masttype"), Drawing = Col("drawing") });
        }
        return list;
    }

    private static List<OheMast> LoadBuiltIn()
    {
        using var s = typeof(OheMasts).Assembly.GetManifestResourceStream("FootplateIR.Core.ohe_masts_kota.csv");
        if (s is null) return new List<OheMast>();
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }
}
