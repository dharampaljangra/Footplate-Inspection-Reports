using System.Globalization;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

/// <summary>One OHE mast from the OHE layout plans (LOP).</summary>
public sealed record OheMast(string No, int Km, int Serial, double Chainage, string Type, string Sheet)
{
    public string Display => $"Mast {No} = KM {Chainage:0.000}" + (Type.Length > 0 ? $", {Type}" : "") + (Sheet.Length > 0 ? $", LOP {Sheet}" : "");
}

/// <summary>
/// OHE mast register of the section (mast number, KM + metre, mast type, LOP sheet) - the same data as in the
/// Track Machine Progress app (Kota division, GQL - SMPA, KM 5 - 79). Locations in the reports written as
/// KM / mast ("40/14", "35/10-15") are placed by it to the metre; outside the register an average span is assumed.
/// </summary>
public static class OheMasts
{
    private static readonly Lazy<List<OheMast>> Data = new(Load);
    private static readonly Lazy<Dictionary<string, OheMast>> ByNo = new(() =>
        Data.Value.GroupBy(m => m.No).ToDictionary(g => g.Key, g => g.First()));
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Average distance between masts where the register has no data (km).</summary>
    public const double AverageSpan = 0.0585;

    public static IReadOnlyList<OheMast> All => Data.Value;
    public static bool HasData => Data.Value.Count > 0;
    public static string Coverage => HasData ? $"KM {Data.Value[0].Chainage:0.0} - {Data.Value[^1].Chainage:0.0} ({Data.Value.Count} masts)" : "no masts";

    private static List<OheMast> Load()
    {
        var list = new List<OheMast>();
        using var s = typeof(OheMasts).Assembly.GetManifestResourceStream("FootplateIR.Core.ohe_masts_kota.csv");
        if (s is null) return list;
        using var r = new StreamReader(s);
        string[]? head = null;
        string? line;
        while ((line = r.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var c = line.Split(',');
            if (head is null) { head = c.Select(h => h.Trim().ToLowerInvariant()).ToArray(); continue; }
            string Col(string n) { var i = Array.IndexOf(head, n); return i >= 0 && i < c.Length ? c[i].Trim() : ""; }
            var no = Normal(Col("mast"));
            var m = Regex.Match(no, @"^(\d+)/(\d+)");
            if (!m.Success || !double.TryParse(Col("chainage"), NumberStyles.Float, Inv, out var ch)) continue;
            list.Add(new OheMast(no, int.Parse(m.Groups[1].Value, Inv), int.Parse(m.Groups[2].Value, Inv), ch, Col("masttype"), Col("drawing")));
        }
        return list.OrderBy(x => x.Chainage).ToList();
    }

    /// <summary>"31/05" → "31/5".</summary>
    public static string Normal(string mast)
    {
        var m = Regex.Match(mast ?? "", @"^\s*(?:OHE|MAST|M|KM)?\.?\s*[-:]?\s*(\d{1,4})\s*/\s*(\d{1,3})([A-Z]?)\s*$", RegexOptions.IgnoreCase);
        if (!m.Success) return (mast ?? "").Trim().ToUpperInvariant();
        var serial = m.Groups[2].Value.TrimStart('0');
        return $"{m.Groups[1].Value.TrimStart('0').PadLeft(1, '0')}/{(serial.Length > 0 ? serial : "0")}{m.Groups[3].Value.ToUpperInvariant()}";
    }

    public static OheMast? Find(string mast) => ByNo.Value.TryGetValue(Normal(mast), out var m) ? m : null;

    public static OheMast? Find(int km, int serial) => ByNo.Value.TryGetValue($"{km}/{serial}", out var m) ? m : null;

    /// <summary>KM of "KM / mast": from the register, else KM + mast number × average span.</summary>
    public static double KmOf(int km, int serial) =>
        Find(km, serial)?.Chainage ?? km + Math.Min(serial * AverageSpan, 0.99);

    /// <summary>
    /// The stretch a location covers: "35/10-15" (masts 35/10 to 35/15), "71/15-72/04", "40/14" (one mast).
    /// Null for yard locations ("JNRI S3 to S4") and LCs.
    /// </summary>
    public static (double From, double To, bool FromRegister)? Range(string location)
    {
        var s = Regex.Replace(location ?? "", @"^\s*KM\.?\s*", "", RegexOptions.IgnoreCase);
        var m = Regex.Match(s, @"^(\d{1,4})\s*/\s*(\d{1,3})(?![\d.])(?:\s*[-–]\s*(?:(\d{1,4})\s*/\s*)?(\d{1,3})(?![\d/.]))?");
        if (!m.Success) return null;
        int km1 = int.Parse(m.Groups[1].Value, Inv), s1 = int.Parse(m.Groups[2].Value, Inv);
        int km2 = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, Inv) : km1;
        int s2 = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, Inv) : s1;
        var a = Find(km1, s1);
        var b = Find(km2, s2);
        double ca = a?.Chainage ?? KmOf(km1, s1), cb = b?.Chainage ?? KmOf(km2, s2);
        return (Math.Min(ca, cb), Math.Max(ca, cb), a is not null && b is not null);
    }

    /// <summary>"35.492 - 35.825 km" / "40.869 km" for a mast location, "" when it is not one or the masts are not in the register.</summary>
    public static string Describe(string location)
    {
        if (Range(location) is not { FromRegister: true } r) return "";
        return Math.Abs(r.To - r.From) < 0.0005 ? $"{r.From:0.000} km" : $"{r.From:0.000} - {r.To:0.000} km";
    }

    /// <summary>Masts nearest to a KM (for "which mast is this?").</summary>
    public static List<OheMast> Near(double km, int count = 3) =>
        Data.Value.OrderBy(m => Math.Abs(m.Chainage - km)).Take(count).OrderBy(m => m.Chainage).ToList();
}
