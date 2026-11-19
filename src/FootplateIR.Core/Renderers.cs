using System.Net;
using System.Text;

namespace FootplateIR.Core;

/// <summary>Draws a <see cref="DocModel"/> as PDF.</summary>
public static class PdfRenderer
{
    private const float ML = 42, MR = 42, MT = 38, MB = 52;

    public static byte[] Render(DocModel doc)
    {
        var pdf = new PdfWriter { Title = doc.Title, Author = doc.Author, Subject = doc.Footer, Keywords = "Footplate inspection" };
        if (doc.EmbeddedData is not null) pdf.EmbeddedData = DataPacking.Pack(doc.EmbeddedData);
        pdf.NewPage(doc.Landscape);
        float y = MT;
        float W() => pdf.PageWidth - ML - MR;
        float Bottom() => pdf.PageHeight - MB;
        void Ensure(float h)
        {
            if (y + h > Bottom()) { pdf.NewPage(doc.Landscape); y = MT; }
        }

        foreach (var block in doc.Blocks)
        {
            switch (block)
            {
                case TitleBlock t:
                    foreach (var line in PdfWriter.Wrap(t.Text, W(), t.Size, t.Bold))
                    {
                        Ensure(t.Size * 1.3f);
                        pdf.TextCenter(ML + W() / 2, y + t.Size * 0.9f, line, t.Size, t.Bold, t.Color);
                        y += t.Size * 1.3f;
                    }
                    break;
                case RuleBlock r:
                    Ensure(8);
                    y += 2.5f;
                    pdf.Line(ML, y, ML + W(), y, r.Thickness, r.Color);
                    y += 5;
                    break;
                case SectionBlock s:
                    Ensure(42);
                    y += 7;
                    pdf.Text(ML, y + 10, s.Text, 11, true, false, Theme.Navy);
                    y += 17;
                    break;
                case GridBlock g:
                    y = Grid(pdf, g, y, W(), Bottom, () => { pdf.NewPage(doc.Landscape); return MT; });
                    y += 2;
                    break;
                case TableBlock tb:
                    y = Table(pdf, tb, y, W(), Bottom, () => { pdf.NewPage(doc.Landscape); return MT; });
                    y += 3;
                    break;
                case ParaBlock p:
                    {
                        float indent = p.Indent + (p.Number.Length > 0 ? 18 : 0);
                        float lineH = p.Size * 1.32f;
                        float leadW = p.Lead.Length > 0 ? PdfWriter.Width(p.Lead, p.Size, true) : 0;
                        var lines = WrapFirst(p.Text, W() - indent - leadW, W() - indent, p.Size);
                        for (int i = 0; i < lines.Count; i++)
                        {
                            Ensure(lineH);
                            float baseY = y + p.Size * 0.95f;
                            if (i == 0)
                            {
                                if (p.Number.Length > 0) pdf.Text(ML + p.Indent, baseY, p.Number, p.Size, true);
                                if (leadW > 0) pdf.Text(ML + indent, baseY, p.Lead, p.Size, true);
                                pdf.Text(ML + indent + leadW, baseY, lines[i], p.Size, false, p.Italic);
                            }
                            else pdf.Text(ML + indent, baseY, lines[i], p.Size, false, p.Italic);
                            y += lineH;
                        }
                        y += 2.5f;
                        break;
                    }
                case SignatureBlock sg:
                    Ensure(sg.Lines.Length * 13 + 4);
                    for (int i = 0; i < sg.Lines.Length; i++)
                    {
                        pdf.TextRight(ML + W(), y + 10, sg.Lines[i], 10, i == 0);
                        y += 13;
                    }
                    break;
                case SpacerBlock sp:
                    y += sp.Height;
                    break;
                case PageBreakBlock:
                    pdf.NewPage(doc.Landscape);
                    y = MT;
                    break;
            }
        }

        int total = pdf.PageCount;
        for (int i = 0; i < total; i++)
        {
            pdf.SelectPage(i);
            float fy = pdf.PageHeight - 34;
            pdf.Line(ML, fy, pdf.PageWidth - MR, fy, 0.4f, Theme.Border);
            var footer = doc.Footer;
            while (footer.Length > 10 && PdfWriter.Width(footer, 7.5f) > pdf.PageWidth - ML - MR - 70) footer = footer[..^2];
            pdf.Text(ML, fy + 11, footer, 7.5f, false, false, Theme.Grey);
            pdf.TextRight(pdf.PageWidth - MR, fy + 11, $"Page {i + 1} of {total}", 7.5f, false, Theme.Grey);
        }
        return pdf.ToBytes();
    }

    /// <summary>Wraps text whose first line is shorter (it follows a bold lead).</summary>
    internal static List<string> WrapFirst(string text, float firstWidth, float width, float size)
    {
        var all = new List<string>();
        var paras = (text ?? "").Replace("\r", "").Split('\n');
        for (int pi = 0; pi < paras.Length; pi++)
        {
            var words = paras[pi].Split(' ');
            var cur = new StringBuilder();
            foreach (var w in words)
            {
                float max = all.Count == 0 ? firstWidth : width;
                var trial = cur.Length == 0 ? w : cur + " " + w;
                if (PdfWriter.Width(trial, size) <= max || cur.Length == 0)
                {
                    cur.Clear().Append(trial);
                    continue;
                }
                all.Add(cur.ToString());
                cur.Clear().Append(w);
            }
            all.Add(cur.ToString());
        }
        // Very long single words: let the general wrapper split them.
        return all.SelectMany(l => PdfWriter.Width(l, size) > width ? PdfWriter.Wrap(l, width, size) : new List<string> { l }).ToList();
    }

    private static float Grid(PdfWriter pdf, GridBlock g, float y, float width, Func<float> bottom, Func<float> newPage)
    {
        int ppr = Math.Max(1, g.PairsPerRow);
        float labelW = width / ppr * 0.36f, valueW = width / ppr * 0.64f;
        const float size = 9f, pad = 4f, lineH = 11.5f;
        for (int i = 0; i < g.Pairs.Count; i += ppr)
        {
            var cells = g.Pairs.Skip(i).Take(ppr).ToList();
            int maxLines = 1;
            var wrapped = cells.Select(c => (L: PdfWriter.Wrap(c.Label, labelW - 2 * pad, size, true), V: PdfWriter.Wrap(c.Value, valueW - 2 * pad, size))).ToList();
            foreach (var w in wrapped) maxLines = Math.Max(maxLines, Math.Max(w.L.Count, w.V.Count));
            float h = maxLines * lineH + 2 * pad - 2;
            if (y + h > bottom()) y = newPage();
            float x = 42;
            for (int c = 0; c < ppr; c++)
            {
                pdf.Rect(x, y, labelW, h, Theme.LabelFill, Theme.Border, 0.5f);
                pdf.Rect(x + labelW, y, valueW, h, null, Theme.Border, 0.5f);
                if (c < wrapped.Count)
                {
                    float ly = y + pad + size * 0.9f + (maxLines - wrapped[c].L.Count) * lineH / 2;
                    foreach (var l in wrapped[c].L) { pdf.Text(x + pad, ly, l, size, true); ly += lineH; }
                    float vy = y + pad + size * 0.9f + (maxLines - wrapped[c].V.Count) * lineH / 2;
                    foreach (var v in wrapped[c].V) { pdf.Text(x + labelW + pad, vy, v, size); vy += lineH; }
                }
                x += labelW + valueW;
            }
            y += h;
        }
        return y;
    }

    private static float Table(PdfWriter pdf, TableBlock t, float y, float width, Func<float> bottom, Func<float> newPage)
    {
        int cols = t.Headers.Length;
        var rel = t.Widths.Length == cols ? t.Widths : Enumerable.Repeat(1f / cols, cols).ToArray();
        var sum = rel.Sum();
        var cw = rel.Select(w => w / sum * width).ToArray();
        float size = t.FontSize, pad = 3.5f, lineH = size * 1.25f;

        float Row(string[] cells, bool header, string? fill, float yy)
        {
            var wrapped = new List<List<string>>();
            for (int c = 0; c < cols; c++)
                wrapped.Add(PdfWriter.Wrap(c < cells.Length ? cells[c] : "", cw[c] - 2 * pad, size, header || t.BoldColumns.Contains(c)));
            float h = wrapped.Max(w => w.Count) * lineH + 2 * pad;
            float x = 42;
            for (int c = 0; c < cols; c++)
            {
                pdf.Rect(x, yy, cw[c], h, header ? Theme.Navy : fill, Theme.Border, 0.5f);
                float ty = yy + pad + size * 0.9f;
                foreach (var l in wrapped[c])
                {
                    bool bold = header || t.BoldColumns.Contains(c);
                    var color = header ? "FFFFFF" : "000000";
                    if (t.CenterColumns.Contains(c)) pdf.TextCenter(x + cw[c] / 2, ty, l, size, bold, color);
                    else pdf.Text(x + pad, ty, l, size, bold, false, color);
                    ty += lineH;
                }
                x += cw[c];
            }
            return h;
        }

        float Height(string[] cells, bool header)
        {
            int max = 1;
            for (int c = 0; c < cols; c++)
                max = Math.Max(max, PdfWriter.Wrap(c < cells.Length ? cells[c] : "", cw[c] - 2 * pad, size, header || t.BoldColumns.Contains(c)).Count);
            return max * lineH + 2 * pad;
        }

        // Start on a new page rather than leave the heading with one or two rows at the foot of a page.
        var first = t.Rows.Take(3).Sum(r => Height(r, false));
        if (y + Height(t.Headers, true) + first > bottom()) y = newPage();
        y += Row(t.Headers, true, null, y);
        for (int i = 0; i < t.Rows.Count; i++)
        {
            var h = Height(t.Rows[i], false);
            if (y + h > bottom())
            {
                y = newPage();
                y += Row(t.Headers, true, null, y);
            }
            var fill = t.HighlightRows.Contains(i) ? Theme.Warn : i % 2 == 1 ? Theme.AltFill : null;
            y += Row(t.Rows[i], false, fill, y);
        }
        return y;
    }
}

/// <summary>Draws a <see cref="DocModel"/> as a printable HTML page (print / "Save as PDF" from any browser).</summary>
public static class HtmlRenderer
{
    private static string E(string s) => WebUtility.HtmlEncode(s ?? "").Replace("\n", "<br>");

    public static string Render(DocModel doc)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append($"<meta name=\"author\" content=\"{E(doc.Author)}\"><title>{E(doc.Title)}</title><style>");
        sb.Append($"@page{{size:A4 {(doc.Landscape ? "landscape" : "portrait")};margin:14mm 14mm 16mm 14mm}}");
        sb.Append("body{font-family:Arial,Helvetica,sans-serif;font-size:10pt;color:#000;max-width:" + (doc.Landscape ? "1050px" : "760px") + ";margin:16px auto;padding:0 12px}");
        sb.Append($".t{{text-align:center;margin:2px 0;color:#{Theme.Navy}}} .rule{{border:0;border-top:1.4px solid #{Theme.Navy};margin:6px 0}}");
        sb.Append($"h2{{font-size:11pt;color:#{Theme.Navy};margin:12px 0 5px}} table{{border-collapse:collapse;width:100%;margin:2px 0 6px}}");
        sb.Append($"td,th{{border:1px solid #{Theme.Border};padding:4px 5px;vertical-align:top}} th{{background:#{Theme.Navy};color:#fff;text-align:left}}");
        sb.Append($"td.l{{background:#{Theme.LabelFill};font-weight:bold;width:17%}} tr.alt td{{background:#{Theme.AltFill}}} tr.warn td{{background:#{Theme.Warn}}}");
        sb.Append(".p{margin:3px 0;text-align:justify} .p .n{display:inline-block;width:22px;font-weight:bold} .sig{text-align:right;margin-top:24px;line-height:1.4}");
        sb.Append(".c{text-align:center} .b{font-weight:bold} .foot{margin-top:18px;border-top:1px solid #ccc;color:#555;font-size:8pt;padding-top:3px}");
        sb.Append(".pb{page-break-after:always;height:0} @media print{.foot{display:none}}</style></head><body>");
        foreach (var block in doc.Blocks)
        {
            switch (block)
            {
                case TitleBlock t:
                    sb.Append($"<div class=\"t\" style=\"font-size:{t.Size}pt;{(t.Bold ? "font-weight:bold;" : "")}color:#{t.Color}\">{E(t.Text)}</div>");
                    break;
                case RuleBlock:
                    sb.Append("<hr class=\"rule\">");
                    break;
                case SectionBlock s:
                    sb.Append($"<h2>{E(s.Text)}</h2>");
                    break;
                case GridBlock g:
                    sb.Append("<table>");
                    for (int i = 0; i < g.Pairs.Count; i += g.PairsPerRow)
                    {
                        sb.Append("<tr>");
                        foreach (var (l, v) in g.Pairs.Skip(i).Take(g.PairsPerRow)) sb.Append($"<td class=\"l\">{E(l)}</td><td>{E(v)}</td>");
                        sb.Append("</tr>");
                    }
                    sb.Append("</table>");
                    break;
                case TableBlock tb:
                    sb.Append($"<table style=\"font-size:{tb.FontSize}pt\"><tr>");
                    var sum = tb.Widths.Length == tb.Headers.Length ? tb.Widths.Sum() : 0;
                    for (int c = 0; c < tb.Headers.Length; c++)
                        sb.Append(sum > 0 ? $"<th style=\"width:{tb.Widths[c] / sum * 100:0}%\">{E(tb.Headers[c])}</th>" : $"<th>{E(tb.Headers[c])}</th>");
                    sb.Append("</tr>");
                    for (int i = 0; i < tb.Rows.Count; i++)
                    {
                        var cls = tb.HighlightRows.Contains(i) ? " class=\"warn\"" : i % 2 == 1 ? " class=\"alt\"" : "";
                        sb.Append($"<tr{cls}>");
                        for (int c = 0; c < tb.Headers.Length; c++)
                        {
                            var css = (tb.BoldColumns.Contains(c) ? "b " : "") + (tb.CenterColumns.Contains(c) ? "c" : "");
                            sb.Append($"<td class=\"{css.Trim()}\">{E(c < tb.Rows[i].Length ? tb.Rows[i][c] : "")}</td>");
                        }
                        sb.Append("</tr>");
                    }
                    sb.Append("</table>");
                    break;
                case ParaBlock p:
                    sb.Append($"<div class=\"p\" style=\"font-size:{p.Size}pt;{(p.Italic ? "font-style:italic;" : "")}{(p.Justify ? "" : "text-align:left;")}{(p.Number.Length > 0 ? "padding-left:22px;text-indent:-22px;" : "")}\">");
                    if (p.Number.Length > 0) sb.Append($"<span class=\"n\" style=\"text-indent:0\">{E(p.Number)}</span>");
                    if (p.Lead.Length > 0) sb.Append($"<b>{E(p.Lead)}</b>");
                    sb.Append(E(p.Text)).Append("</div>");
                    break;
                case SignatureBlock sg:
                    sb.Append("<div class=\"sig\">");
                    for (int i = 0; i < sg.Lines.Length; i++) sb.Append(i == 0 ? $"<b>{E(sg.Lines[i])}</b><br>" : $"{E(sg.Lines[i])}<br>");
                    sb.Append("</div>");
                    break;
                case SpacerBlock sp:
                    sb.Append($"<div style=\"height:{sp.Height}px\"></div>");
                    break;
                case PageBreakBlock:
                    sb.Append("<div class=\"pb\"></div>");
                    break;
            }
        }
        sb.Append($"<div class=\"foot\">{E(doc.Footer)}</div>");
        if (doc.EmbeddedData is not null) sb.Append($"<script type=\"application/json\" id=\"firdata\">{DataPacking.Pack(doc.EmbeddedData)}</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}

/// <summary>Compressed, base-64 copy of the reports placed inside exported files (read back on import).</summary>
public static class DataPacking
{
    public const string Marker = "FIRDATA1:";

    public static string Pack(string json)
    {
        var raw = Encoding.UTF8.GetBytes(json);
        using var ms = new MemoryStream();
        using (var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw, 0, raw.Length);
        return Marker + Convert.ToBase64String(ms.ToArray());
    }

    public static string? Unpack(string packed)
    {
        var s = packed.Trim();
        var i = s.IndexOf(Marker, StringComparison.Ordinal);
        if (i < 0) return null;
        s = new string(s[(i + Marker.Length)..].Where(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=').ToArray());
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(s));
            using var z = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
            using var r = new StreamReader(z, Encoding.UTF8);
            return r.ReadToEnd();
        }
        catch { return null; }
    }
}
