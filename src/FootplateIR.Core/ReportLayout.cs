using System.Text.Json;
using System.Text.RegularExpressions;

namespace FootplateIR.Core;

/// <summary>Official layout of the inspection note and of the registers, shared by every exporter.</summary>
public static class ReportLayout
{
    /// <summary>Heading printed before a standard observation in the inspection note.</summary>
    public static string HeadingOf(string observation)
    {
        var o = observation.ToLowerInvariant();
        if (Regex.IsMatch(o, @"\bsms?\b|station master|pointsm")) return "Station Staff";
        if (Regex.IsMatch(o, @"^(the )?lp and alp|loco crew|loco pilot")) return "Loco Crew";
        if (Regex.IsMatch(o, @"train manager|\bguard\b")) return "Train Manager";
        if (Regex.IsMatch(o, @"section was found|section was normal|track was")) return "Section Condition";
        return "";
    }

    public static string KindTitle(InspectionReport r) => (r.Kind.Length > 0 ? r.Kind : "Footplate Inspection").ToUpperInvariant() + " NOTE";

    public static string Purposes(InspectionReport r) => r.Purposes.Count > 0 ? "(" + string.Join(" + ", r.Purposes) + ")" : "";

    public static string FooterOf(InspectionReport r) =>
        $"{r.Kind} {Purposes(r)} – {r.FromStation} to {r.ToStation}" + (r.TrainNo.Length > 0 ? $" – Train {r.TrainNo}" : "") + $" – {Fmt.Date(r.Date)}";

    private static string Crew(string name, bool shri) => name.Length == 0 ? "-" : Fmt.WithShri(name, shri);

    public static string LocoOfficial(InspectionReport r)
    {
        var type = Regex.Replace(r.LocoType, @"^(W[A-Z]{2})(\d)", "$1-$2");
        var parts = new[] { r.LocoNo, type, r.Shed }.Where(s => s.Length > 0);
        return string.Join(" / ", parts);
    }

    public static string CabOfficial(InspectionReport r) =>
        Regex.Match(r.Cab, @"\d").Success ? "Cab No. " + Regex.Match(r.Cab, @"\d").Value : r.Cab;

    /// <summary>The inspection note of one report (blocks appended to <paramref name="doc"/>).</summary>
    public static void AddReport(DocModel doc, InspectionReport r, AppData data)
    {
        var p = data.Profile;
        doc.Add(new TitleBlock { Text = p.Railway.ToUpperInvariant(), Size = 15 })
           .Add(new TitleBlock { Text = p.Division.ToUpperInvariant(), Size = 11 });
        if (p.Office.Trim().Length > 0) doc.Add(new TitleBlock { Text = p.Office, Size = 9.5f, Bold = false, Color = Theme.Grey });
        doc.Add(new RuleBlock { Thickness = 1.4f })
           .Add(new TitleBlock { Text = KindTitle(r), Size = 13.5f, Color = "000000" });
        if (r.Purposes.Count > 0) doc.Add(new TitleBlock { Text = Purposes(r), Size = 10, Color = "000000" });
        var sub = new List<string>();
        if (r.Section.Length > 0) sub.Add("Section: " + r.Section);
        if (r.FromStation.Length > 0) sub.Add($"Ex {r.FromStation} to {r.ToStation}" + (r.TrainNo.Length > 0 ? $" by {r.TrainNo}" : ""));
        sub.Add("Date: " + Fmt.Date(r.Date));
        doc.Add(new TitleBlock { Text = string.Join("  |  ", sub), Size = 9.5f, Bold = false, Color = Theme.Grey })
           .Add(new RuleBlock { Thickness = 0.8f });

        if (p.Addressee.Trim().Length > 0)
        {
            doc.Add(new ParaBlock { Text = "To,\n" + p.Addressee.Trim(), Justify = false });
            doc.Add(new SpacerBlock { Height = 2 });
        }

        // 1. Particulars
        int sec = 1;
        doc.Add(new SectionBlock { Text = $"{sec++}. Inspection Particulars" });
        var dur = r.Duration;
        var km = TrainKm(r, data.Masters.Chainage);
        doc.Add(new GridBlock
        {
            Pairs = new()
            {
                ("Inspected by", Crew(r.InspectorName, true)), ("Designation", r.Designation),
                ("Mobile", r.Mobile), ("Section", r.Section),
                ("Type of Inspection", $"{r.Kind} {Purposes(r)}".Trim()), ("Train No.", r.TrainNo + (r.TrainName.Length > 0 ? $" ({r.TrainName})" : "")),
                ("Route", $"{r.FromStation} to {r.ToStation}"), ("Footplate time", Fmt.Duration(dur) + (km is double k ? $"  ({k:0.0} km)" : "")),
                ($"{r.FromStation} Departure", r.Departure is null ? "" : Fmt.Time(r.Departure) + " hrs"), ($"{r.ToStation} Arrival", r.Arrival is null ? "" : Fmt.Time(r.Arrival) + " hrs"),
                ("Locomotive", LocoOfficial(r)), ("Cab", CabOfficial(r)),
                ("Visibility", r.Visibility), ("Weather", r.Weather.Length > 0 ? r.Weather : "-")
            }
        });

        // 2. Crew
        doc.Add(new SectionBlock { Text = $"{sec++}. Crew on Board" });
        doc.Add(new TableBlock
        {
            Headers = new[] { "Designation", "Name", "HQ" },
            Widths = new[] { 0.42f, 0.40f, 0.18f },
            Rows = new()
            {
                new[] { "Loco Pilot (LP)", Crew(r.LocoPilot, p.UseShri), r.LocoPilotHq },
                new[] { r.AlpDesignation == "ALP" ? "Assistant Loco Pilot (ALP)" : "Sr. Assistant Loco Pilot (Sr. ALP)", Crew(r.AssistantLocoPilot, p.UseShri), r.AlpHq },
                new[] { r.TmDesignation switch { "Guard" => "Guard", "Sr. TM" => "Sr. Train Manager (Sr. TM)", _ => "Train Manager (TM)" }, Crew(r.TrainManager, p.UseShri), r.TmHq }
            }
        });

        // 3. Observations
        doc.Add(new SectionBlock { Text = $"{sec++}. Observations" });
        int n = 1;
        if (r.Sightings.Count > 0 || r.FieldStaffNote.Trim().Length > 0)
        {
            var note = r.FieldStaffNote.Trim().Length > 0 ? r.FieldStaffNote.Trim() : Defaults.FieldStaffNote;
            doc.Add(new ParaBlock { Number = $"{n++}.", Lead = "Field Staff: ", Text = note });
            if (r.Sightings.Count > 0)
            {
                var rows = r.Sightings.Select((s, i) => new[]
                {
                    (i + 1).ToString(Fmt.Inv), s.Category, KmDisplay(s.Location) + MastLine(s.Location), s.Count.ToString("00", Fmt.Inv), s.Condition.Length > 0 ? s.Condition : "-",
                    s.Jacket.Length > 0 ? s.Jacket : "-", s.Remarks
                }).ToList();
                doc.Add(new TableBlock
                {
                    Headers = new[] { "S. No.", "Staff", "Location", "Nos.", "Found", "Reflective jacket", "Remarks" },
                    Widths = new[] { 0.07f, 0.21f, 0.15f, 0.07f, 0.12f, 0.13f, 0.25f },
                    Rows = rows,
                    BoldColumns = new[] { 2 },
                    CenterColumns = new[] { 0, 3 },
                    HighlightRows = r.Sightings.Select((s, i) => (s, i)).Where(x => x.s.IsDeficient).Select(x => x.i).ToHashSet(),
                    FontSize = 8.5f
                });
            }
        }
        var sectionLines = new List<string>();
        foreach (var o in r.Observations.Where(o => o.Trim().Length > 0))
        {
            var h = HeadingOf(o);
            if (h == "Section Condition") { sectionLines.Add(Fmt.EndWith(o, '.')); continue; }
            doc.Add(new ParaBlock { Number = $"{n++}.", Lead = h.Length > 0 ? h + ": " : "", Text = Fmt.EndWith(o, '.') });
        }
        if (r.Visibility.Trim().Length > 0) sectionLines.Add($"Visibility: {r.Visibility.Trim().TrimEnd('.')}.");
        if (r.Weather.Trim().Length > 0) sectionLines.Add($"Weather: {r.Weather.Trim().TrimEnd('.')}.");
        if (sectionLines.Count > 0)
            doc.Add(new ParaBlock { Number = $"{n++}.", Lead = "Section Condition / Visibility / Weather: ", Text = string.Join(" ", sectionLines) });

        // 4. Shortcomings
        doc.Add(new SectionBlock { Text = $"{sec++}. Shortcomings / Deficiencies Noted" });
        if (r.Shortcomings.Count == 0) doc.Add(new ParaBlock { Text = "Nil." });
        else for (int i = 0; i < r.Shortcomings.Count; i++) doc.Add(new ParaBlock { Number = $"{i + 1}.", Text = r.Shortcomings[i] });

        // 5. Locations
        doc.Add(new SectionBlock { Text = $"{sec++}. Locations to be Attended for Better Running" });
        if (r.Locations.Count == 0) doc.Add(new ParaBlock { Text = "Nil." });
        else
            doc.Add(new TableBlock
            {
                Headers = new[] { "S. No.", "Location (KM)", "Nature of Defect / Remarks", "Compliance" },
                Widths = new[] { 0.08f, 0.24f, 0.40f, 0.28f },
                Rows = r.Locations.Select((l, i) => new[]
                {
                    (i + 1).ToString(Fmt.Inv), KmDisplay(l.Location) + MastLine(l.Location), l.Feature.Length > 0 ? l.Feature : "To be attended for better running",
                    l.Status == LocationStatus.Attended ? "Attended" + (l.AttendedOn is DateOnly d ? " on " + Fmt.Date(d) : "") + (l.ActionTaken.Length > 0 ? " - " + l.ActionTaken : "")
                        : l.Status == LocationStatus.Watch ? "Under watch" : ""
                }).ToList(),
                BoldColumns = new[] { 1 },
                CenterColumns = new[] { 0 }
            });

        // 6. Patrolling chart check (winter / monsoon patrolling)
        if (data.Settings.PrintChartCheck)
        {
            var night = Patrol.NightOf(r.Date, r.Departure);
            var chart = data.Charts.FirstOrDefault(c => c.AppliesTo(night) && c.Beats.Count > 0);
            if (chart is not null)
            {
                var rows = Patrol.Check(r, chart, data.Masters.Chainage, data.Rosters, data.Settings.ChartToleranceKm, data.Trains);
                if (rows.Count > 0)
                {
                    doc.Add(new SectionBlock { Text = $"{sec++}. Patrolling Chart Check ({chart.Name})" });
                    doc.Add(new TableBlock
                    {
                        Headers = new[] { "Beat", "Patrolmen", "As per chart (KM / time)", "Recorded", "Status" },
                        Widths = new[] { 0.20f, 0.20f, 0.22f, 0.20f, 0.18f },
                        Rows = rows.Select(x => new[]
                        {
                            x.Beat.Range, x.Beat.Patrolmen + (x.Beat.Unit.Length > 0 ? $" ({x.Beat.Unit})" : "") + (x.OnDuty.Count > 0 ? "\n" + string.Join(", ", x.OnDuty.Select(d => d.Display)) : ""),
                            x.ExpectedKm is double ek ? $"KM {Patrol.KmText(ek)} / {Fmt.Time(x.ExpectedTime)}" : "-",
                            x.Seen is null ? "-" : $"{KmDisplay(x.Seen.Location)} ({x.Seen.Count:00})" +
                                (x.Seen.Condition.Length > 0 && x.Seen.Condition != "Alert" ? ", " + x.Seen.Condition.ToLowerInvariant() : "") +
                                (x.Seen.Jacket == "Not worn" ? ", no jacket" : ""),
                            x.Status
                        }).ToList(),
                        HighlightRows = rows.Select((x, i) => (x, i)).Where(t => t.x.Status is Patrol.OffPosition || t.x.Seen?.IsDeficient == true).Select(t => t.i).ToHashSet(),
                        FontSize = 8.5f
                    });
                    doc.Add(new ParaBlock
                    {
                        Text = Patrol.UsesSchedule(r, data.Masters.Chainage, data.Trains)
                            ? $"Meeting points worked out from the actual departure / arrival times and the schedule of train {r.TrainNo}; minor differences are expected."
                            : "Meeting points worked out from the departure and arrival times at an even speed; minor differences are expected.",
                        Size = 8, Italic = true
                    });
                }
            }
        }

        if (r.Remarks.Trim().Length > 0)
        {
            doc.Add(new SectionBlock { Text = $"{sec++}. Remarks" });
            doc.Add(new ParaBlock { Text = r.Remarks.Trim() });
        }
        if (r.ActionTaken.Trim().Length > 0)
        {
            doc.Add(new SectionBlock { Text = $"{sec++}. Action Taken / Advised" });
            doc.Add(new ParaBlock { Text = r.ActionTaken.Trim() });
        }

        doc.Add(new SpacerBlock { Height = 18 });
        doc.Add(new SignatureBlock
        {
            Lines = new[] { $"({r.InspectorName})", r.Designation, r.Mobile.Length > 0 ? "Mob: " + r.Mobile : "", "Date: " + Fmt.Date(r.Date) }
                .Where(s => s.Length > 0).ToArray()
        });
        if (p.CopyTo.Count > 0)
        {
            doc.Add(new SpacerBlock { Height = 6 });
            doc.Add(new ParaBlock { Lead = "Copy to: ", Text = string.Join("\n", p.CopyTo.Select((c, i) => $"{i + 1}. {c}")), Justify = false, Size = 9 });
        }
    }

    /// <summary>Second line with the KM of the OHE masts ("(35.492 - 35.825 km)") when they are in the mast register.</summary>
    public static string MastLine(string loc)
    {
        var d = OheMasts.Describe(loc);
        return d.Length > 0 ? "\n(" + d + ")" : "";
    }

    /// <summary>"KM 35/10-15" for KM locations, the text as written otherwise.</summary>
    public static string KmDisplay(string loc) =>
        Locations.IsKm(loc) && !loc.TrimStart().StartsWith("KM", StringComparison.OrdinalIgnoreCase) ? "KM " + loc.Trim() : loc.Trim();

    public static double? TrainKm(InspectionReport r, IList<StationKm> chainage)
    {
        var a = Patrol.KmOf(r.FromStation, chainage);
        var b = Patrol.KmOf(r.ToStation, chainage);
        return a is double x && b is double y ? Math.Abs(y - x) : null;
    }

    public static DocModel Report(InspectionReport r, AppData data)
    {
        var doc = new DocModel
        {
            Title = $"{r.Kind} Note – {r.FromStation} to {r.ToStation} – {Fmt.Date(r.Date)}",
            Footer = FooterOf(r),
            EmbeddedData = JsonSerializer.Serialize(new List<InspectionReport> { r }, FirJsonContext.Default.ListInspectionReport)
        };
        AddReport(doc, r, data);
        return doc;
    }

    /// <summary>Several inspection notes in one file, one after the other (each on a new page).</summary>
    public static DocModel Reports(IList<InspectionReport> reports, AppData data, string title)
    {
        var doc = new DocModel
        {
            Title = title,
            Footer = title,
            EmbeddedData = JsonSerializer.Serialize(reports.ToList(), FirJsonContext.Default.ListInspectionReport)
        };
        for (int i = 0; i < reports.Count; i++)
        {
            if (i > 0) doc.Add(new PageBreakBlock());
            AddReport(doc, reports[i], data);
        }
        return doc;
    }

    /// <summary>Register of footplate inspections for a period (one line per inspection).</summary>
    public static DocModel Register(IList<InspectionReport> reports, AppData data, string period)
    {
        var p = data.Profile;
        var doc = new DocModel
        {
            Title = $"Register of Footplate Inspections - {period}",
            Footer = $"Register of Footplate Inspections - {period} - {p.Name}, {p.Designation}",
            Landscape = true,
            EmbeddedData = JsonSerializer.Serialize(reports.ToList(), FirJsonContext.Default.ListInspectionReport)
        };
        doc.Add(new TitleBlock { Text = p.Railway.ToUpperInvariant() + " - " + p.Division.ToUpperInvariant(), Size = 12 })
           .Add(new TitleBlock { Text = "REGISTER OF FOOTPLATE INSPECTIONS", Size = 13, Color = "000000" })
           .Add(new TitleBlock { Text = $"{p.Name}, {p.Designation}  |  Section: {p.Section}  |  Period: {period}", Size = 9.5f, Bold = false, Color = Theme.Grey })
           .Add(new RuleBlock());
        var ordered = reports.OrderBy(r => r.Date).ThenBy(r => r.Departure).ToList();
        doc.Add(new TableBlock
        {
            Headers = new[] { "S. No.", "Date", "Inspection", "Train", "From - To", "Dep.", "Arr.", "Duration", "Km", "Loco", "LP / ALP / TM", "Staff seen", "Deficient", "Short-\ncomings", "Locations" },
            Widths = new[] { 0.035f, 0.065f, 0.12f, 0.05f, 0.075f, 0.04f, 0.04f, 0.05f, 0.04f, 0.09f, 0.17f, 0.05f, 0.055f, 0.055f, 0.06f },
            Rows = ordered.Select((r, i) => new[]
            {
                (i + 1).ToString(Fmt.Inv), Fmt.Date(r.Date), $"{r.Kind.Replace(" Inspection", "")} {Purposes(r)}".Trim(), r.TrainNo, $"{r.FromStation} - {r.ToStation}",
                Fmt.Time(r.Departure), Fmt.Time(r.Arrival), Fmt.Duration(r.Duration),
                TrainKm(r, data.Masters.Chainage) is double k ? k.ToString("0", Fmt.Inv) : "", r.LocoLine,
                string.Join(" / ", new[] { r.LocoPilot, r.AssistantLocoPilot, r.TrainManager }.Where(s => s.Length > 0)),
                r.Sightings.Sum(s => s.Count).ToString(Fmt.Inv), r.Sightings.Count(s => s.IsDeficient).ToString(Fmt.Inv),
                r.Shortcomings.Count.ToString(Fmt.Inv), r.Locations.Count.ToString(Fmt.Inv)
            }).ToList(),
            CenterColumns = new[] { 0, 5, 6, 8, 11, 12, 13, 14 },
            FontSize = 7.5f
        });
        var nights = ordered.Count(r => r.Kind.Contains("Night", StringComparison.OrdinalIgnoreCase));
        var hours = ordered.Sum(r => r.Duration?.TotalHours ?? 0);
        var kms = ordered.Sum(r => TrainKm(r, data.Masters.Chainage) ?? 0);
        doc.Add(new SpacerBlock { Height = 4 });
        doc.Add(new ParaBlock
        {
            Lead = "Summary: ",
            Text = $"{ordered.Count} footplate inspections ({nights} by night, {ordered.Count - nights} by day), {hours:0.0} hours on the footplate, about {kms:0} km covered; " +
                   $"{ordered.Sum(r => r.Shortcomings.Count)} shortcomings and {ordered.Sum(r => r.Locations.Count)} locations reported for attention.",
            Size = 9
        });
        doc.Add(new SpacerBlock { Height = 16 });
        doc.Add(new SignatureBlock { Lines = new[] { $"({p.Name})", p.Designation } });
        return doc;
    }

    /// <summary>Locations reported for better running, grouped by spot, with how often each was reported and its compliance.</summary>
    public static DocModel LocationRegister(IList<InspectionReport> reports, AppData data, string period, bool pendingOnly)
    {
        var p = data.Profile;
        var groups = LocationGroups(reports).Where(g => !pendingOnly || g.Pending > 0).ToList();
        var doc = new DocModel
        {
            Title = $"Locations to be Attended - {period}",
            Footer = $"Locations to be attended for better running - {period}",
            Landscape = false
        };
        doc.Add(new TitleBlock { Text = p.Railway.ToUpperInvariant() + " - " + p.Division.ToUpperInvariant(), Size = 12 })
           .Add(new TitleBlock { Text = "LOCATIONS TO BE ATTENDED FOR BETTER RUNNING" + (pendingOnly ? " (PENDING)" : ""), Size = 12.5f, Color = "000000" })
           .Add(new TitleBlock { Text = $"As felt during footplate inspections  |  Section: {p.Section}  |  Period: {period}", Size = 9.5f, Bold = false, Color = Theme.Grey })
           .Add(new RuleBlock());
        doc.Add(new TableBlock
        {
            Headers = new[] { "S. No.", "Location (KM)", "Nature / Remarks", "Reported on", "Times", "Status / Compliance" },
            Widths = new[] { 0.07f, 0.17f, 0.25f, 0.22f, 0.08f, 0.21f },
            Rows = groups.Select((g, i) => new[]
            {
                (i + 1).ToString(Fmt.Inv), string.Join(", ", g.Items.Select(x => KmDisplay(x.Loc.Location)).Distinct()) + MastLine(g.Items[0].Loc.Location),
                string.Join("; ", g.Items.Select(x => x.Loc.Feature).Where(f => f.Length > 0).Distinct()),
                string.Join(", ", g.Items.Select(x => Fmt.Date(x.Report.Date)).Distinct()),
                g.Items.Count.ToString(Fmt.Inv),
                g.Pending > 0 ? (g.Items.Count > 1 ? "Pending (repeated)" : "Pending")
                              : "Attended" + (g.Items.Select(x => x.Loc.AttendedOn).Where(d => d is not null).Max() is DateOnly d ? " on " + Fmt.Date(d) : "")
            }).ToList(),
            BoldColumns = new[] { 1 },
            CenterColumns = new[] { 0, 4 },
            HighlightRows = groups.Select((g, i) => (g, i)).Where(x => x.g.Pending > 0 && x.g.Items.Count > 1).Select(x => x.i).ToHashSet(),
            FontSize = 8.5f
        });
        doc.Add(new SpacerBlock { Height = 16 });
        doc.Add(new SignatureBlock { Lines = new[] { $"({p.Name})", p.Designation } });
        return doc;
    }

    public sealed record LocationGroup(string Key, List<(InspectionReport Report, AttentionLocation Loc)> Items)
    {
        public int Pending => Items.Count(x => x.Loc.Status != LocationStatus.Attended);
    }

    public static List<LocationGroup> LocationGroups(IEnumerable<InspectionReport> reports) =>
        reports.SelectMany(r => r.Locations.Select(l => (Report: r, Loc: l)))
            .GroupBy(x => x.Loc.Key)
            .Select(g => new LocationGroup(g.Key, g.OrderBy(x => x.Report.Date).ToList()))
            .OrderBy(g => Locations.SortValue(g.Items[0].Loc.Location)).ThenBy(g => g.Key)
            .ToList();

    /// <summary>Duty list with mobile numbers (for printing / sharing).</summary>
    public static DocModel Roster(IList<DutyRoster> rosters, AppData data)
    {
        var doc = new DocModel { Title = "Patrolmen / Keymen on Duty", Footer = "Patrolmen / Keymen on duty" };
        foreach (var r in rosters)
        {
            doc.Add(new TitleBlock { Text = ReportParser.Plural(r.Category).ToUpperInvariant() + (r.IsNight ? " ON NIGHT DUTY" : " ON DUTY"), Size = 12 })
               .Add(new TitleBlock { Text = (r.IsNight ? "Night of " + Patrol.NightText(r.Date) : "Date: " + Fmt.Date(r.Date)) + (r.Area.Length > 0 ? "  |  " + r.Area : ""), Size = 9.5f, Bold = false, Color = Theme.Grey })
               .Add(new RuleBlock());
            var rows = new List<string[]>();
            foreach (var b in r.Beats.OrderBy(b => b.FromKm))
                for (int i = 0; i < Math.Max(1, b.Persons.Count); i++)
                {
                    var person = i < b.Persons.Count ? b.Persons[i] : null;
                    rows.Add(new[] { i == 0 ? b.No : "", i == 0 ? b.Range : "", person?.Display ?? "-", person?.MobileDisplay ?? "", person?.Note ?? "" });
                }
            doc.Add(new TableBlock
            {
                Headers = new[] { "Beat / Unit", "KM", "Name", "Mobile", "Remarks" },
                Widths = new[] { 0.10f, 0.22f, 0.32f, 0.18f, 0.18f },
                Rows = rows,
                CenterColumns = new[] { 0 },
                BoldColumns = new[] { 3 }
            });
            doc.Add(new SpacerBlock { Height = 10 });
        }
        return doc;
    }
}
