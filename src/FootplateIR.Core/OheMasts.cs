using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

/// <summary>One OHE mast: number (KM/mast), exact chainage, mast type and layout-plan sheet.</summary>
public sealed class OheMast
{
    public string Mast { get; set; } = "";
    public double Chainage { get; set; }
    public string Type { get; set; } = "";
    public string Drawing { get; set; } = "";

    [JsonIgnore]
    public string No => Mast;

    [JsonIgnore]
    public int Km => int.TryParse(Mast.Split('/')[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var km) ? km : 0;

    [JsonIgnore]
    public int Serial
    {
        get
        {
            var m = Regex.Match(Mast, @"/(\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var serial) ? serial : 0;
        }
    }

    [JsonIgnore]
    public string Sheet { get => Drawing; set => Drawing = value; }

    [JsonIgnore]
    public string Display => $"Mast {Mast} = KM {Chainage:0.000}" + (Type.Length > 0 ? $", {Type}" : "") + (Drawing.Length > 0 ? $", LOP {Drawing}" : "");
}

/// <summary>
/// OHE mast register. Built in: the OHE layout plans (LOP) of GQL - SMPA, KM 5 - 79 (1397 masts, same register as the
/// Track Machine Progress app). Masts of other stretches can be added from a CSV in the same format.
/// Footplate notes write locations as mast numbers ("KM 40/14"), so a mast found here gives the exact running KM.
/// </summary>
public static class OheMasts
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Regex MastRx = new(@"^\s*(?:KM\.?|OHE\.?|MAST|M)?\s*[-.:]?\s*(\d{1,4})\s*/\s*(\d{1,3}[A-Z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RangeRx = new(@"^\s*(?:KM\.?|OHE\.?|MAST|M)?\s*[-.:]?\s*(?<km1>\d{1,4})\s*/\s*(?<s1>\d{1,2})(?<a1>[A-Z]?)(?![\d.])(?:\s*[-–]\s*(?:(?<km2>\d{1,4})\s*/\s*)?(?<s2>\d{1,2})(?<a2>[A-Z]?)(?![\d/.]))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Lazy<List<OheMast>> BuiltIn = new(LoadBuiltIn);
    private static List<OheMast> _extra = new();
    private static Dictionary<string, OheMast>? _index;

    /// <summary>Average distance between masts where the register has no data (km).</summary>
    public const double AverageSpan = 0.0585;

    public const string Source = "OHE layout plans (LOP), GQL - SMPA";

    public static IReadOnlyList<OheMast> All => BuiltIn.Value.Concat(_extra).OrderBy(m => m.Chainage).ToList();
    public static int Count => BuiltIn.Value.Count + _extra.Count;
    public static bool HasData => BuiltIn.Value.Count > 0;
    public static string Coverage => HasData ? $"KM {BuiltIn.Value[0].Chainage:0.0} - {BuiltIn.Value[^1].Chainage:0.0} ({BuiltIn.Value.Count} masts)" : "no masts";

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
        foreach (var m in BuiltIn.Value.Concat(_extra)) d[Normal(m.Mast)] = m;
        return _index = d;
    }

    /// <summary>"31/05" → "31/5".</summary>
    public static string Normal(string mast)
    {
        var m = MastRx.Match(mast ?? "");
        if (!m.Success) return (mast ?? "").Trim().ToUpperInvariant();
        var km = m.Groups[1].Value.TrimStart('0');
        var serial = m.Groups[2].Value;
        var suffix = serial.Length > 0 && char.IsLetter(serial[^1]) ? serial[^1..] : "";
        var number = suffix.Length > 0 ? serial[..^1] : serial;
        number = number.TrimStart('0');
        return $"{(km.Length > 0 ? km : "0")}/{(number.Length > 0 ? number : "0")}{suffix.ToUpperInvariant()}";
    }

    /// <summary>The mast written at the start of a location ("40/14", "KM 35/10-15", "OHE 61/12(SEJ)"), or null.</summary>
    public static OheMast? Find(string? location)
    {
        var m = MastRx.Match(location ?? "");
        return m.Success && Index().TryGetValue(Normal(m.Value), out var mast) ? mast : null;
    }

    public static OheMast? Find(int km, int serial) => Index().TryGetValue(Normal($"{km}/{serial}"), out var mast) ? mast : null;

    /// <summary>KM of "KM / mast": from the register, else KM + mast number × average span.</summary>
    public static double KmOf(int km, int serial) =>
        Find(km, serial)?.Chainage ?? km + Math.Min(serial * AverageSpan, 0.99);

    /// <summary>
    /// The stretch a location covers: "35/10-15" (masts 35/10 to 35/15), "71/15-72/04", "40/14" (one mast).
    /// Null for yard locations ("JNRI S3 to S4") and locations expressed in metres.
    /// </summary>
    public static (double From, double To, bool FromRegister)? Range(string? location)
    {
        var m = RangeRx.Match(location ?? "");
        if (!m.Success) return null;
        int km1 = int.Parse(m.Groups["km1"].Value, Inv), s1 = int.Parse(m.Groups["s1"].Value, Inv);
        int km2 = m.Groups["km2"].Success ? int.Parse(m.Groups["km2"].Value, Inv) : km1;
        int s2 = m.Groups["s2"].Success ? int.Parse(m.Groups["s2"].Value, Inv) : s1;
        var a = Find($"{km1}/{s1}{m.Groups["a1"].Value}");
        var b = Find($"{km2}/{s2}{m.Groups["a2"].Value}");
        double ca = a?.Chainage ?? KmOf(km1, s1), cb = b?.Chainage ?? KmOf(km2, s2);
        return (Math.Min(ca, cb), Math.Max(ca, cb), a is not null && b is not null);
    }

    /// <summary>"OHE 35/10 - 35/15, ch. 35.492 - 35.825 km" for a location, or "" when the masts are not in the register.</summary>
    public static string Describe(string? location)
    {
        var m = RangeRx.Match(location ?? "");
        if (!m.Success) return "";
        int km1 = int.Parse(m.Groups["km1"].Value, Inv), s1 = int.Parse(m.Groups["s1"].Value, Inv);
        int km2 = m.Groups["km2"].Success ? int.Parse(m.Groups["km2"].Value, Inv) : km1;
        int s2 = m.Groups["s2"].Success ? int.Parse(m.Groups["s2"].Value, Inv) : s1;
        var a = Find($"{km1}/{s1}{m.Groups["a1"].Value}");
        if (a is null) return "";
        var b = Find($"{km2}/{s2}{m.Groups["a2"].Value}");
        if (b is null || ReferenceEquals(a, b)) return $"OHE {a.Mast}, ch. {a.Chainage:0.000} km";
        return $"OHE {a.Mast} - {b.Mast}, ch. {a.Chainage:0.000} - {b.Chainage:0.000} km";
    }

    /// <summary>Masts nearest to a chainage (within <paramref name="withinKm"/>).</summary>
    public static OheMast? Nearest(double chainage, double withinKm = 0.05) =>
        All.Select(m => (m, d: Math.Abs(m.Chainage - chainage))).Where(x => x.d <= withinKm)
            .OrderBy(x => x.d).Select(x => x.m).FirstOrDefault();

    /// <summary>Masts nearest to a KM (for "which mast is this?").</summary>
    public static List<OheMast> Near(double km, int count = 3) =>
        All.OrderBy(m => Math.Abs(m.Chainage - km)).Take(count).OrderBy(m => m.Chainage).ToList();

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
                if (!head.Contains("mast")) head = new[] { "mast", "km", "metre", "chainage", "masttype", "line", "drawing" };
                else continue;
            }
            string Col(string name) { var i = Array.IndexOf(head, name); return i >= 0 && i < c.Length ? c[i].Trim() : ""; }
            var mast = Col("mast");
            if (!MastRx.IsMatch(mast)) continue;
            double ch;
            if (!double.TryParse(Col("chainage"), NumberStyles.Float, Inv, out ch))
            {
                if (!int.TryParse(Col("km"), NumberStyles.Integer, Inv, out var km) || !double.TryParse(Col("metre"), NumberStyles.Float, Inv, out var metre)) continue;
                ch = km + metre / 1000.0;
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
        return Parse(r.ReadToEnd()).OrderBy(x => x.Chainage).ToList();
    }
}
