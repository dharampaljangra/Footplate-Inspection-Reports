using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace FootplateIR.Core;

/// <summary>Draws a <see cref="DocModel"/> as a Word (.docx) document.</summary>
public static class DocxRenderer
{
    private const string Font = "Arial";

    public static byte[] Render(DocModel doc)
    {
        using var ms = new MemoryStream();
        using (var wd = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            wd.PackageProperties.Creator = doc.Author;
            wd.PackageProperties.Title = doc.Title;
            wd.PackageProperties.Subject = doc.Footer;
            var main = wd.AddMainDocumentPart();
            main.Document = new W.Document(new Body());
            var body = main.Document.Body!;
            int pageW = doc.Landscape ? 16838 : 11906, pageH = doc.Landscape ? 11906 : 16838;
            const int margin = 900;
            int contentW = pageW - 2 * margin;

            AddStyles(main);

            foreach (var block in doc.Blocks)
            {
                switch (block)
                {
                    case TitleBlock t:
                        body.Append(Para(new[] { RunOf(t.Text, t.Size, t.Bold, false, t.Color) }, JustificationValues.Center, after: 20));
                        break;
                    case RuleBlock r:
                        {
                            var p = new Paragraph(new ParagraphProperties(
                                new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = (uint)Math.Max(4, r.Thickness * 8), Color = r.Color, Space = 1 }),
                                new SpacingBetweenLines { After = "80", Before = "0" }));
                            body.Append(p);
                            break;
                        }
                    case SectionBlock s:
                        body.Append(Para(new[] { RunOf(s.Text, 11, true, false, Theme.Navy) }, JustificationValues.Left, before: 160, after: 60, keepNext: true));
                        break;
                    case GridBlock g:
                        body.Append(Grid(g, contentW));
                        body.Append(Para(Array.Empty<Run>(), JustificationValues.Left, after: 0, size: 4));
                        break;
                    case TableBlock tb:
                        body.Append(Table(tb, contentW));
                        body.Append(Para(Array.Empty<Run>(), JustificationValues.Left, after: 0, size: 4));
                        break;
                    case ParaBlock pb:
                        {
                            var runs = new List<Run>();
                            if (pb.Number.Length > 0) runs.Add(RunOf(pb.Number + "\t", pb.Size, true));
                            if (pb.Lead.Length > 0) runs.Add(RunOf(pb.Lead, pb.Size, true));
                            runs.Add(RunOf(pb.Text, pb.Size, false, pb.Italic));
                            var p = Para(runs, pb.Justify ? JustificationValues.Both : JustificationValues.Left, after: 50,
                                hanging: pb.Number.Length > 0 ? 360 : 0, left: (int)(pb.Indent * 20) + (pb.Number.Length > 0 ? 360 : 0));
                            body.Append(p);
                            break;
                        }
                    case SignatureBlock sg:
                        for (int i = 0; i < sg.Lines.Length; i++)
                            body.Append(Para(new[] { RunOf(sg.Lines[i], 10, i == 0) }, JustificationValues.Right, after: 0, keepNext: i < sg.Lines.Length - 1));
                        break;
                    case SpacerBlock sp:
                        body.Append(Para(Array.Empty<Run>(), JustificationValues.Left, after: (int)(sp.Height * 12), size: 4));
                        break;
                    case PageBreakBlock:
                        body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                        break;
                }
            }

            // Footer: text on the left, page number on the right.
            var footerPart = main.AddNewPart<FooterPart>();
            var fp = new Paragraph(
                new ParagraphProperties(
                    new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border, Space = 1 }),
                    new Tabs(new TabStop { Val = TabStopValues.Right, Position = contentW })),
                RunOf(doc.Footer, 7.5f, false, false, Theme.Grey),
                RunOf("\tPage ", 7.5f, false, false, Theme.Grey));
            fp.Append(new SimpleField(RunOf("1", 7.5f, false, false, Theme.Grey)) { Instruction = " PAGE " });
            fp.Append(RunOf(" of ", 7.5f, false, false, Theme.Grey));
            fp.Append(new SimpleField(RunOf("1", 7.5f, false, false, Theme.Grey)) { Instruction = " NUMPAGES " });
            footerPart.Footer = new Footer(fp);

            body.Append(new SectionProperties(
                new FooterReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(footerPart) },
                new W.PageSize { Width = (UInt32Value)(uint)pageW, Height = (UInt32Value)(uint)pageH, Orient = doc.Landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait },
                new PageMargin { Top = margin, Bottom = margin, Left = (UInt32Value)(uint)margin, Right = (UInt32Value)(uint)margin, Header = 400U, Footer = 400U, Gutter = 0U }));

            if (doc.EmbeddedData is not null)
            {
                var cx = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
                using var s = new MemoryStream(Encoding.UTF8.GetBytes($"<firdata xmlns=\"urn:footplate-inspection-reports\">{DataPacking.Pack(doc.EmbeddedData)}</firdata>"));
                cx.FeedData(s);
            }
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static void AddStyles(MainDocumentPart main)
    {
        var sp = main.AddNewPart<StyleDefinitionsPart>();
        sp.Styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font, EastAsia = Font },
                    new FontSize { Val = "20" }, new Languages { Val = "en-IN" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = "0", Line = "252", LineRule = LineSpacingRuleValues.Auto }))));
        sp.Styles.Save();
    }

    private static Run RunOf(string text, float size, bool bold = false, bool italic = false, string color = "000000")
    {
        var rp = new RunProperties(new RunFonts { Ascii = Font, HighAnsi = Font, ComplexScript = Font });
        if (bold) rp.Append(new Bold());
        if (italic) rp.Append(new Italic());
        if (color != "000000") rp.Append(new W.Color { Val = color });
        rp.Append(new FontSize { Val = ((int)Math.Round(size * 2)).ToString(Fmt.Inv) });
        var run = new Run(rp);
        var parts = (text ?? "").Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            var tabs = parts[i].Split('\t');
            for (int j = 0; j < tabs.Length; j++)
            {
                if (j > 0) run.Append(new TabChar());
                if (tabs[j].Length > 0) run.Append(new Text(tabs[j]) { Space = SpaceProcessingModeValues.Preserve });
            }
        }
        return run;
    }

    private static Paragraph Para(IEnumerable<Run> runs, JustificationValues align, int after = 60, int before = 0, int hanging = 0, int left = 0, bool keepNext = false, float size = 0)
    {
        var pp = new ParagraphProperties();
        if (keepNext) pp.Append(new KeepNext());
        pp.Append(new SpacingBetweenLines { Before = before.ToString(Fmt.Inv), After = after.ToString(Fmt.Inv) });
        if (hanging > 0 || left > 0) pp.Append(new Indentation { Left = left.ToString(Fmt.Inv), Hanging = hanging > 0 ? hanging.ToString(Fmt.Inv) : null });
        pp.Append(new Justification { Val = align });
        if (size > 0) pp.Append(new ParagraphMarkRunProperties(new FontSize { Val = ((int)(size * 2)).ToString(Fmt.Inv) }));
        var p = new Paragraph(pp);
        foreach (var r in runs) p.Append(r);
        return p;
    }

    private static TableProperties TableProps() => new(
        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
        new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Theme.Border }),
        new TableLayout { Type = TableLayoutValues.Fixed },
        new TableCellMarginDefault(
            new TopMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
            new TableCellLeftMargin { Width = (short)80, Type = TableWidthValues.Dxa },
            new BottomMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
            new TableCellRightMargin { Width = (short)80, Type = TableWidthValues.Dxa }));

    private static TableCell Cell(string text, int width, float size, bool bold, string? fill, string color = "000000", bool center = false)
    {
        var tcp = new TableCellProperties(new TableCellWidth { Width = width.ToString(Fmt.Inv), Type = TableWidthUnitValues.Dxa });
        if (fill is not null) tcp.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = fill });
        tcp.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        return new TableCell(tcp, Para(new[] { RunOf(text, size, bold, false, color) }, center ? JustificationValues.Center : JustificationValues.Left, after: 0));
    }

    private static Table Grid(GridBlock g, int contentW)
    {
        int ppr = Math.Max(1, g.PairsPerRow);
        int lw = (int)(contentW / ppr * 0.36), vw = contentW / ppr - lw;
        var t = new Table(TableProps());
        var grid = new TableGrid();
        for (int i = 0; i < ppr; i++) { grid.Append(new GridColumn { Width = lw.ToString(Fmt.Inv) }); grid.Append(new GridColumn { Width = vw.ToString(Fmt.Inv) }); }
        t.Append(grid);
        for (int i = 0; i < g.Pairs.Count; i += ppr)
        {
            var row = new TableRow(new TableRowProperties(new CantSplit()));
            foreach (var (l, v) in g.Pairs.Skip(i).Take(ppr))
            {
                row.Append(Cell(l, lw, 9, true, Theme.LabelFill));
                row.Append(Cell(v, vw, 9, false, null));
            }
            for (int k = g.Pairs.Skip(i).Take(ppr).Count(); k < ppr; k++) { row.Append(Cell("", lw, 9, true, Theme.LabelFill)); row.Append(Cell("", vw, 9, false, null)); }
            t.Append(row);
        }
        return t;
    }

    private static Table Table(TableBlock tb, int contentW)
    {
        int cols = tb.Headers.Length;
        var rel = tb.Widths.Length == cols ? tb.Widths : Enumerable.Repeat(1f / cols, cols).ToArray();
        var sum = rel.Sum();
        var cw = rel.Select(w => (int)(w / sum * contentW)).ToArray();
        var t = new Table(TableProps());
        var grid = new TableGrid();
        foreach (var w in cw) grid.Append(new GridColumn { Width = w.ToString(Fmt.Inv) });
        t.Append(grid);
        var head = new TableRow(new TableRowProperties(new TableHeader(), new CantSplit()));
        for (int c = 0; c < cols; c++) head.Append(Cell(tb.Headers[c], cw[c], tb.FontSize, true, Theme.Navy, "FFFFFF"));
        t.Append(head);
        for (int i = 0; i < tb.Rows.Count; i++)
        {
            var fill = tb.HighlightRows.Contains(i) ? Theme.Warn : i % 2 == 1 ? Theme.AltFill : null;
            var row = new TableRow(new TableRowProperties(new CantSplit()));
            for (int c = 0; c < cols; c++)
                row.Append(Cell(c < tb.Rows[i].Length ? tb.Rows[i][c] : "", cw[c], tb.FontSize, tb.BoldColumns.Contains(c), fill, "000000", tb.CenterColumns.Contains(c)));
            t.Append(row);
        }
        return t;
    }
}

/// <summary>Excel output: the inspection note laid out on a sheet, and the registers as data tables.</summary>
public static class XlsxRenderer
{
    private static XLColor C(string hex) => XLColor.FromHtml("#" + hex);

    /// <summary>The printed layout on one worksheet (A4, fit to width).</summary>
    public static void RenderSheet(IXLWorksheet ws, DocModel doc)
    {
        // Column grid wide enough for every table in the document.
        var tables = doc.Blocks.OfType<TableBlock>().ToList();
        int cols = Math.Max(4, tables.Count > 0 ? tables.Max(t => t.Headers.Length) : 4);
        double total = doc.Landscape ? 150 : 96;
        var widths = Enumerable.Repeat(total / cols, cols).ToArray();
        var widest = tables.OrderByDescending(t => t.Headers.Length).FirstOrDefault();
        if (widest is not null && widest.Headers.Length == cols && widest.Widths.Length == cols)
        {
            var s = widest.Widths.Sum();
            widths = widest.Widths.Select(w => Math.Max(5, w / s * total)).ToArray();
        }
        for (int c = 0; c < cols; c++) ws.Column(c + 1).Width = widths[c];
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 10;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.PageOrientation = doc.Landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Left = ws.PageSetup.Margins.Right = 0.4;
        ws.PageSetup.Margins.Top = ws.PageSetup.Margins.Bottom = 0.5;
        ws.PageSetup.Footer.Left.AddText(doc.Footer);
        ws.PageSetup.Footer.Right.AddText("Page &P of &N");
        ws.ShowGridLines = false;

        int row = 1;
        double CharsPerLine(int fromCol, int toCol) => Enumerable.Range(fromCol, toCol - fromCol + 1).Sum(c => widths[c - 1]) * 1.15;
        void Height(int r, string text, int fromCol, int toCol, double size = 10)
        {
            var lines = (text ?? "").Split('\n').Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / Math.Max(1, CharsPerLine(fromCol, toCol) * 10 / size))));
            ws.Row(r).Height = Math.Max(ws.Row(r).Height, lines * size * 1.45 + 3);
        }

        foreach (var block in doc.Blocks)
        {
            switch (block)
            {
                case TitleBlock t:
                    {
                        var rg = ws.Range(row, 1, row, cols).Merge();
                        rg.FirstCell().Value = t.Text;
                        rg.Style.Font.Bold = t.Bold;
                        rg.Style.Font.FontSize = t.Size;
                        rg.Style.Font.FontColor = C(t.Color);
                        rg.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Row(row).Height = t.Size * 1.6;
                        row++;
                        break;
                    }
                case RuleBlock r:
                    ws.Range(row, 1, row, cols).Style.Border.TopBorder = r.Thickness > 1 ? XLBorderStyleValues.Medium : XLBorderStyleValues.Thin;
                    ws.Range(row, 1, row, cols).Style.Border.TopBorderColor = C(r.Color);
                    ws.Row(row).Height = 5;
                    row++;
                    break;
                case SectionBlock s:
                    ws.Cell(row, 1).Value = s.Text;
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 11;
                    ws.Cell(row, 1).Style.Font.FontColor = C(Theme.Navy);
                    ws.Row(row).Height = 20;
                    row++;
                    break;
                case GridBlock g:
                    {
                        int ppr = Math.Max(1, g.PairsPerRow);
                        int span = cols / ppr;
                        int lab = Math.Max(1, span * 2 / 5);
                        for (int i = 0; i < g.Pairs.Count; i += ppr)
                        {
                            int c0 = 1;
                            foreach (var (l, v) in g.Pairs.Skip(i).Take(ppr))
                            {
                                int lEnd = c0 + lab - 1, vEnd = c0 + span - 1;
                                if (vEnd > cols || ppr == 1) vEnd = cols;
                                var lr = ws.Range(row, c0, row, lEnd);
                                if (lab > 1) lr.Merge();
                                lr.FirstCell().Value = l;
                                lr.Style.Font.Bold = true;
                                lr.Style.Fill.BackgroundColor = C(Theme.LabelFill);
                                var vr = ws.Range(row, lEnd + 1, row, vEnd);
                                if (vEnd > lEnd + 1) vr.Merge();
                                vr.FirstCell().Value = v;
                                foreach (var rg in new[] { lr, vr })
                                {
                                    rg.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                    rg.Style.Border.OutsideBorderColor = C(Theme.Border);
                                    rg.Style.Alignment.WrapText = true;
                                    rg.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                }
                                Height(row, v, lEnd + 1, vEnd, 10);
                                Height(row, l, c0, lEnd, 10);
                                c0 += span;
                            }
                            row++;
                        }
                        break;
                    }
                case TableBlock tb:
                    {
                        int n = tb.Headers.Length;
                        int Last(int c) => c == n - 1 ? cols : c + 1;
                        void Put(int r, int c, string text, bool bold, string? fill, string color, bool center)
                        {
                            var rg = ws.Range(r, c + 1, r, Last(c));
                            if (Last(c) > c + 1) rg.Merge();
                            rg.FirstCell().Value = text;
                            rg.Style.Font.Bold = bold;
                            rg.Style.Font.FontSize = tb.FontSize + 1;
                            rg.Style.Font.FontColor = C(color);
                            if (fill is not null) rg.Style.Fill.BackgroundColor = C(fill);
                            rg.Style.Alignment.WrapText = true;
                            rg.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                            if (center) rg.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            rg.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            rg.Style.Border.OutsideBorderColor = C(Theme.Border);
                            Height(r, text, c + 1, Last(c), tb.FontSize + 1);
                        }
                        for (int c = 0; c < n; c++) Put(row, c, tb.Headers[c], true, Theme.Navy, "FFFFFF", false);
                        row++;
                        for (int i = 0; i < tb.Rows.Count; i++)
                        {
                            var fill = tb.HighlightRows.Contains(i) ? Theme.Warn : i % 2 == 1 ? Theme.AltFill : null;
                            for (int c = 0; c < n; c++)
                                Put(row, c, c < tb.Rows[i].Length ? tb.Rows[i][c] : "", tb.BoldColumns.Contains(c), fill, "000000", tb.CenterColumns.Contains(c));
                            row++;
                        }
                        row++;
                        break;
                    }
                case ParaBlock p:
                    {
                        int start = p.Number.Length > 0 ? 2 : 1;
                        if (p.Number.Length > 0)
                        {
                            ws.Cell(row, 1).Value = p.Number;
                            ws.Cell(row, 1).Style.Font.Bold = true;
                            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                            ws.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                        }
                        var rg = ws.Range(row, start, row, cols).Merge();
                        var rt = rg.FirstCell().GetRichText();
                        if (p.Lead.Length > 0) rt.AddText(p.Lead).SetBold();
                        var body = rt.AddText(p.Text);
                        body.SetFontSize(p.Size + 0.5);
                        if (p.Italic) body.SetItalic();
                        rg.Style.Alignment.WrapText = true;
                        rg.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                        Height(row, p.Lead + p.Text, start, cols, p.Size + 0.5);
                        row++;
                        break;
                    }
                case SignatureBlock sg:
                    for (int i = 0; i < sg.Lines.Length; i++)
                    {
                        var rg = ws.Range(row, Math.Max(1, cols - 2), row, cols).Merge();
                        rg.FirstCell().Value = sg.Lines[i];
                        rg.Style.Font.Bold = i == 0;
                        rg.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        row++;
                    }
                    break;
                case SpacerBlock sp:
                    ws.Row(row).Height = Math.Max(4, sp.Height);
                    row++;
                    break;
                case PageBreakBlock:
                    ws.PageSetup.AddHorizontalPageBreak(row - 1);
                    break;
            }
        }
    }

    public static byte[] Render(DocModel doc, string sheetName = "Report")
    {
        using var wb = new XLWorkbook();
        wb.Properties.Author = doc.Author;
        wb.Properties.Title = doc.Title;
        RenderSheet(wb.Worksheets.Add(SafeName(sheetName)), doc);
        if (doc.EmbeddedData is not null) AddDataSheet(wb, doc.EmbeddedData);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static string SafeName(string s)
    {
        var n = new string((s ?? "Sheet").Select(c => "[]:*?/\\".Contains(c) ? '-' : c).ToArray()).Trim();
        if (n.Length == 0) n = "Sheet";
        return n.Length > 31 ? n[..31] : n;
    }

    /// <summary>Hidden sheet holding the reports so the app reads its own workbook back exactly.</summary>
    public static void AddDataSheet(XLWorkbook wb, string json)
    {
        var ws = wb.Worksheets.Add("_firdata");
        var packed = DataPacking.Pack(json);
        int r = 1;
        for (int i = 0; i < packed.Length; i += 30000) ws.Cell(r++, 1).Value = packed.Substring(i, Math.Min(30000, packed.Length - i));
        ws.Visibility = XLWorksheetVisibility.VeryHidden;
    }

    /// <summary>Workbook for a period: Register, field staff, shortcomings, locations, and each inspection note on its own sheet.</summary>
    public static byte[] Workbook(IList<InspectionReport> reports, AppData data, string period, bool includeNotes = true)
    {
        using var wb = new XLWorkbook();
        wb.Properties.Author = Defaults.AuthorCredit;
        wb.Properties.Title = "Footplate inspections - " + period;
        var ordered = reports.OrderBy(r => r.Date).ThenBy(r => r.Departure).ToList();

        var reg = wb.Worksheets.Add("Register");
        string[] head = { "S. No.", "Date", "Inspection", "Purpose", "Train No.", "From", "To", "Departure", "Arrival", "Duration (h:mm)", "Km",
                          "Loco No.", "Loco type", "Cab", "Shed", "LP", "LP HQ", "ALP", "ALP HQ", "TM", "TM HQ", "Visibility", "Weather",
                          "Staff seen (nos.)", "Deficient", "Shortcomings", "Locations", "Inspected by", "Designation", "Source" };
        for (int c = 0; c < head.Length; c++) reg.Cell(1, c + 1).Value = head[c];
        for (int i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            int row = i + 2;
            object?[] v =
            {
                i + 1, r.Date.ToDateTime(TimeOnly.MinValue), r.Kind, string.Join("+", r.Purposes), r.TrainNo, r.FromStation, r.ToStation,
                Fmt.Time(r.Departure), Fmt.Time(r.Arrival), r.Duration is TimeSpan d ? $"{(int)d.TotalHours}:{d.Minutes:00}" : "",
                ReportLayout.TrainKm(r, data.Masters.Chainage), r.LocoNo, r.LocoType, r.Cab, r.Shed, r.LocoPilot, r.LocoPilotHq,
                r.AssistantLocoPilot, r.AlpHq, r.TrainManager, r.TmHq, r.Visibility, r.Weather, r.Sightings.Sum(s => s.Count),
                r.Sightings.Count(s => s.IsDeficient), string.Join("\n", r.Shortcomings), Locations.Join(r.Locations), r.InspectorName, r.Designation, r.Source
            };
            for (int c = 0; c < v.Length; c++)
            {
                var cell = reg.Cell(row, c + 1);
                switch (v[c])
                {
                    case int n: cell.Value = n; break;
                    case double dd: cell.Value = Math.Round(dd, 1); break;
                    case DateTime dt: cell.Value = dt; cell.Style.DateFormat.Format = "dd.mm.yyyy"; break;
                    case null: break;
                    default: cell.Value = v[c]!.ToString(); break;
                }
            }
        }
        StyleTable(reg, head.Length, ordered.Count + 1, new[] { 6, 11, 22, 16, 9, 7, 7, 9, 9, 9, 6, 9, 9, 6, 7, 20, 9, 20, 9, 20, 9, 14, 18, 8, 8, 40, 40, 18, 16, 10 });

        var staff = wb.Worksheets.Add("Field staff");
        string[] sh = { "Date", "Train No.", "From - To", "Staff", "Location", "Nos.", "Found", "Reflective jacket", "Remarks" };
        for (int c = 0; c < sh.Length; c++) staff.Cell(1, c + 1).Value = sh[c];
        int sr = 2;
        foreach (var r in ordered)
            foreach (var s in r.Sightings)
            {
                staff.Cell(sr, 1).Value = r.Date.ToDateTime(TimeOnly.MinValue);
                staff.Cell(sr, 1).Style.DateFormat.Format = "dd.mm.yyyy";
                staff.Cell(sr, 2).Value = r.TrainNo;
                staff.Cell(sr, 3).Value = r.Route;
                staff.Cell(sr, 4).Value = s.Category;
                staff.Cell(sr, 5).Value = ReportLayout.KmDisplay(s.Location);
                staff.Cell(sr, 6).Value = s.Count;
                staff.Cell(sr, 7).Value = s.Condition;
                staff.Cell(sr, 8).Value = s.Jacket;
                staff.Cell(sr, 9).Value = s.Remarks;
                if (s.IsDeficient) staff.Range(sr, 1, sr, sh.Length).Style.Fill.BackgroundColor = C(Theme.Warn);
                sr++;
            }
        StyleTable(staff, sh.Length, sr - 1, new[] { 11, 9, 12, 22, 14, 6, 12, 14, 36 });

        var sc = wb.Worksheets.Add("Shortcomings");
        string[] sch = { "Date", "Train No.", "From - To", "S. No.", "Shortcoming" };
        for (int c = 0; c < sch.Length; c++) sc.Cell(1, c + 1).Value = sch[c];
        int scr = 2;
        foreach (var r in ordered)
            for (int k = 0; k < r.Shortcomings.Count; k++)
            {
                sc.Cell(scr, 1).Value = r.Date.ToDateTime(TimeOnly.MinValue);
                sc.Cell(scr, 1).Style.DateFormat.Format = "dd.mm.yyyy";
                sc.Cell(scr, 2).Value = r.TrainNo;
                sc.Cell(scr, 3).Value = r.Route;
                sc.Cell(scr, 4).Value = k + 1;
                sc.Cell(scr, 5).Value = r.Shortcomings[k];
                scr++;
            }
        StyleTable(sc, sch.Length, scr - 1, new[] { 11, 9, 12, 7, 90 });

        var loc = wb.Worksheets.Add("Locations");
        string[] lh = { "Location (KM)", "Nature / Remarks", "Reported on", "Train", "Times reported (this spot)", "Status", "Attended on", "Action taken" };
        for (int c = 0; c < lh.Length; c++) loc.Cell(1, c + 1).Value = lh[c];
        int lr = 2;
        foreach (var g in ReportLayout.LocationGroups(ordered))
            foreach (var (r, l) in g.Items)
            {
                loc.Cell(lr, 1).Value = ReportLayout.KmDisplay(l.Location);
                loc.Cell(lr, 2).Value = l.Feature;
                loc.Cell(lr, 3).Value = r.Date.ToDateTime(TimeOnly.MinValue);
                loc.Cell(lr, 3).Style.DateFormat.Format = "dd.mm.yyyy";
                loc.Cell(lr, 4).Value = r.TrainNo;
                loc.Cell(lr, 5).Value = g.Items.Count;
                loc.Cell(lr, 6).Value = l.Status;
                if (l.AttendedOn is DateOnly ad) { loc.Cell(lr, 7).Value = ad.ToDateTime(TimeOnly.MinValue); loc.Cell(lr, 7).Style.DateFormat.Format = "dd.mm.yyyy"; }
                loc.Cell(lr, 8).Value = l.ActionTaken;
                if (l.Status != LocationStatus.Attended && g.Items.Count > 1) loc.Range(lr, 1, lr, lh.Length).Style.Fill.BackgroundColor = C(Theme.Warn);
                lr++;
            }
        StyleTable(loc, lh.Length, lr - 1, new[] { 18, 24, 12, 9, 12, 12, 12, 30 });

        if (includeNotes)
            foreach (var r in ordered.Take(60))
            {
                var name = SafeName($"{r.Date:dd.MM.yy} {r.TrainNo} {r.FromStation}-{r.ToStation}");
                int k = 2;
                var baseName = name;
                while (wb.Worksheets.Contains(name)) name = SafeName($"{baseName[..Math.Min(baseName.Length, 27)]} ({k++})");
                RenderSheet(wb.Worksheets.Add(name), ReportLayout.Report(r, data));
            }

        AddDataSheet(wb, System.Text.Json.JsonSerializer.Serialize(ordered, FirJsonContext.Default.ListInspectionReport));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void StyleTable(IXLWorksheet ws, int cols, int lastRow, int[] widths)
    {
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 10;
        var head = ws.Range(1, 1, 1, cols);
        head.Style.Font.Bold = true;
        head.Style.Font.FontColor = XLColor.White;
        head.Style.Fill.BackgroundColor = C(Theme.Navy);
        head.Style.Alignment.WrapText = true;
        var all = ws.Range(1, 1, Math.Max(1, lastRow), cols);
        all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.InsideBorderColor = C(Theme.Border);
        all.Style.Border.OutsideBorderColor = C(Theme.Border);
        all.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        all.Style.Alignment.WrapText = true;
        for (int c = 0; c < cols && c < widths.Length; c++) ws.Column(c + 1).Width = widths[c];
        ws.SheetView.FreezeRows(1);
        if (lastRow > 1) ws.Range(1, 1, lastRow, cols).SetAutoFilter();
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.SetRowsToRepeatAtTop(1, 1);
    }
}
