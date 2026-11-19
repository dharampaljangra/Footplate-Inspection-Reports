using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace FootplateIR.Core;

/// <summary>
/// A small PDF writer (pure .NET, no native libraries) so that PDF export works the same on Windows and Android.
/// Uses the standard Helvetica fonts (no embedding needed), WinAnsi encoding and compressed page streams.
/// Coordinates are in points from the TOP-left corner of the page.
/// </summary>
public sealed class PdfWriter
{
    public const float A4W = 595.28f, A4H = 841.89f;
    private readonly List<(float W, float H, StringBuilder Content)> _pages = new();
    private StringBuilder? _cur;
    private float _h;
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Keywords { get; set; } = "";
    /// <summary>Data embedded as a PDF comment so that the app can read its own exports back exactly.</summary>
    public string? EmbeddedData { get; set; }
    public int PageCount => _pages.Count;
    public float PageWidth { get; private set; } = A4W;
    public float PageHeight => _h;

    public void NewPage(bool landscape = false)
    {
        PageWidth = landscape ? A4H : A4W;
        _h = landscape ? A4W : A4H;
        _cur = new StringBuilder();
        _pages.Add((PageWidth, _h, _cur));
    }

    /// <summary>Continue drawing on an earlier page (used for "Page n of N" footers).</summary>
    public void SelectPage(int index)
    {
        var p = _pages[index];
        _cur = p.Content;
        PageWidth = p.W;
        _h = p.H;
    }

    private static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public void Text(float x, float y, string s, float size, bool bold = false, bool italic = false, string color = "000000")
    {
        if (_cur is null || string.IsNullOrEmpty(s)) return;
        var font = bold ? "F2" : italic ? "F3" : "F1";
        _cur.Append("BT ").Append(Rgb(color, false)).Append(" /").Append(font).Append(' ').Append(N(size)).Append(" Tf ")
            .Append(N(x)).Append(' ').Append(N(_h - y)).Append(" Td (").Append(Escape(s)).Append(") Tj ET\n");
    }

    public void TextRight(float xRight, float y, string s, float size, bool bold = false, string color = "000000") =>
        Text(xRight - Width(s, size, bold), y, s, size, bold, false, color);

    public void TextCenter(float xCenter, float y, string s, float size, bool bold = false, string color = "000000") =>
        Text(xCenter - Width(s, size, bold) / 2, y, s, size, bold, false, color);

    public void Line(float x1, float y1, float x2, float y2, float width = 0.5f, string color = "000000")
    {
        _cur?.Append(Rgb(color, true)).Append(' ').Append(N(width)).Append(" w ")
            .Append(N(x1)).Append(' ').Append(N(_h - y1)).Append(" m ").Append(N(x2)).Append(' ').Append(N(_h - y2)).Append(" l S\n");
    }

    public void Rect(float x, float y, float w, float h, string? fill = null, string? stroke = "000000", float width = 0.5f)
    {
        if (_cur is null) return;
        var r = $"{N(x)} {N(_h - y - h)} {N(w)} {N(h)} re";
        if (fill is not null && stroke is not null)
            _cur.Append(Rgb(fill, false)).Append(' ').Append(Rgb(stroke, true)).Append(' ').Append(N(width)).Append(" w ").Append(r).Append(" B\n");
        else if (fill is not null)
            _cur.Append(Rgb(fill, false)).Append(' ').Append(r).Append(" f\n");
        else if (stroke is not null)
            _cur.Append(Rgb(stroke, true)).Append(' ').Append(N(width)).Append(" w ").Append(r).Append(" S\n");
    }

    private static string Rgb(string hex, bool stroke)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) hex = "000000";
        float r = int.Parse(hex[..2], NumberStyles.HexNumber) / 255f, g = int.Parse(hex[2..4], NumberStyles.HexNumber) / 255f, b = int.Parse(hex[4..], NumberStyles.HexNumber) / 255f;
        return $"{r.ToString("0.###", CultureInfo.InvariantCulture)} {g.ToString("0.###", CultureInfo.InvariantCulture)} {b.ToString("0.###", CultureInfo.InvariantCulture)} {(stroke ? "RG" : "rg")}";
    }

    // ---------------------------------------------------------------- text measuring and wrapping

    private static readonly short[] Helv =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };
    private static readonly short[] HelvB =
    {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
    };

    public static float Width(string s, float size, bool bold = false)
    {
        double w = 0;
        foreach (var ch in Map(s))
        {
            int c = ch;
            w += c is >= 32 and <= 126 ? (bold ? HelvB : Helv)[c - 32] : c switch
            {
                0x96 => 556, 0x97 => 1000, 0x91 or 0x92 => 222, 0x93 or 0x94 => 333, 0x95 => 350, 0x85 => 1000, 0xB0 => 400,
                _ => 556
            };
        }
        return (float)(w * size / 1000.0);
    }

    /// <summary>Breaks text into lines no wider than <paramref name="maxWidth"/>; keeps explicit line breaks.</summary>
    public static List<string> Wrap(string text, float maxWidth, float size, bool bold = false)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var words = para.Split(' ');
            var cur = new StringBuilder();
            foreach (var w0 in words)
            {
                var w = w0;
                var trial = cur.Length == 0 ? w : cur + " " + w;
                if (Width(trial, size, bold) <= maxWidth) { cur.Clear().Append(trial); continue; }
                if (cur.Length > 0) { lines.Add(cur.ToString()); cur.Clear(); }
                while (Width(w, size, bold) > maxWidth && w.Length > 1)
                {
                    int n = w.Length - 1;
                    while (n > 1 && Width(w[..n], size, bold) > maxWidth) n--;
                    lines.Add(w[..n]);
                    w = w[n..];
                }
                cur.Append(w);
            }
            lines.Add(cur.ToString());
        }
        return lines;
    }

    // ---------------------------------------------------------------- encoding

    private static string Map(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '–' => '\u0096', '—' => '\u0097', '‘' => '\u0091', '’' => '\u0092', '“' => '\u0093', '”' => '\u0094',
                '•' => '\u0095', '…' => '\u0085', '\t' => ' ', ' ' => ' ',
                _ when ch < 256 => ch,
                _ => '?'
            });
        }
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in Map(s))
        {
            if (ch is '(' or ')' or '\\') sb.Append('\\').Append(ch);
            else if (ch < 32) sb.Append(' ');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string PdfString(string s) => "(" + Escape(s) + ")";

    // ---------------------------------------------------------------- file

    public byte[] ToBytes()
    {
        if (_pages.Count == 0) NewPage();
        var ms = new MemoryStream();
        var offsets = new List<long>();
        var latin = Encoding.Latin1;
        void W(string s) { var b = latin.GetBytes(s); ms.Write(b, 0, b.Length); }
        void Obj(int n, string body) { offsets.Add(ms.Position); W($"{n} 0 obj\n{body}\nendobj\n"); }

        W("%PDF-1.4\n%âãÏÓ\n");
        int pagesStart = 7;
        var kids = string.Join(" ", Enumerable.Range(0, _pages.Count).Select(i => $"{pagesStart + 2 * i} 0 R"));
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, $"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>");
        Obj(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Obj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Oblique /Encoding /WinAnsiEncoding >>");
        var now = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        Obj(6, $"<< /Title {PdfString(Title)} /Author {PdfString(Author)} /Subject {PdfString(Subject)} /Keywords {PdfString(Keywords)} " +
               $"/Creator (Footplate Inspection Reports) /Producer (Footplate Inspection Reports) /CreationDate (D:{now}) >>");
        for (int i = 0; i < _pages.Count; i++)
        {
            var (w, h, content) = _pages[i];
            int pageObj = pagesStart + 2 * i, contentObj = pageObj + 1;
            Obj(pageObj, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(w)} {N(h)}] " +
                         "/Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R >> >> " +
                         $"/Contents {contentObj} 0 R >>");
            var raw = latin.GetBytes(content.ToString());
            byte[] packed;
            using (var zs = new MemoryStream())
            {
                using (var z = new ZLibStream(zs, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw, 0, raw.Length);
                packed = zs.ToArray();
            }
            offsets.Add(ms.Position);
            W($"{contentObj} 0 obj\n<< /Length {packed.Length} /Filter /FlateDecode >>\nstream\n");
            ms.Write(packed, 0, packed.Length);
            W("\nendstream\nendobj\n");
        }
        if (EmbeddedData is not null)
        {
            // One comment line per 200 characters; ignored by PDF readers, read back by the importer.
            W("%FIRDATA-BEGIN\n");
            for (int i = 0; i < EmbeddedData.Length; i += 200) W("%" + EmbeddedData.Substring(i, Math.Min(200, EmbeddedData.Length - i)) + "\n");
            W("%FIRDATA-END\n");
        }
        long xref = ms.Position;
        W($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:0000000000} 00000 n \n");
        W($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R /Info 6 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }
}
