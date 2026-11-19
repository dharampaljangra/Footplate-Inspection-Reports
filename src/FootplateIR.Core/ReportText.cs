using System.Text;

namespace FootplateIR.Core;

/// <summary>The report as a WhatsApp / SMS message, in the same style as written by hand.</summary>
public static class ReportText
{
    public static string SightingText(StaffSighting s)
    {
        var loc = s.Location.Trim();
        var where = "at " + ReportLayout.KmDisplay(loc);
        var sb = new StringBuilder($"{s.Category} {where} - {Fmt.Nos(s.Count)}");
        if (s.Condition.Length > 0) sb.Append(' ').Append(s.Condition.ToLowerInvariant());
        if (s.Jacket == "Worn") sb.Append(s.Condition.Length > 0 ? " and" : "").Append(" with reflective jacket");
        else if (s.Jacket == "Not worn") sb.Append(s.Condition.Length > 0 ? " and" : "").Append(" without reflective jacket");
        if (s.Remarks.Length > 0) sb.Append(" (").Append(s.Remarks).Append(')');
        return sb.ToString().TrimEnd('.') + ".";
    }

    /// <summary>All observation paragraphs in printed order (field staff, general, visibility, weather).</summary>
    public static List<(string Text, List<string> Sub)> ObservationItems(InspectionReport r)
    {
        var list = new List<(string, List<string>)>();
        if (r.Sightings.Count > 0 || r.FieldStaffNote.Trim().Length > 0)
        {
            var note = r.FieldStaffNote.Trim();
            if (note.Length == 0) note = Defaults.FieldStaffNote;
            list.Add((note, r.Sightings.Select(SightingText).ToList()));
        }
        foreach (var o in r.Observations.Where(o => o.Trim().Length > 0)) list.Add((Fmt.EndWith(o, '.'), new List<string>()));
        if (r.Visibility.Trim().Length > 0) list.Add(($"Visibility: {r.Visibility.Trim().TrimEnd('.')}.", new List<string>()));
        if (r.Weather.Trim().Length > 0) list.Add(($"Weather: {r.Weather.Trim().TrimEnd('.')}.", new List<string>()));
        return list;
    }

    public static string ToMessage(InspectionReport r, Profile p)
    {
        var sb = new StringBuilder();
        if (p.Salutation.Trim().Length > 0) sb.AppendLine(p.Salutation.Trim());
        sb.AppendLine($"Date: {Fmt.Date(r.Date)},");
        sb.AppendLine($"Name: {r.InspectorName},");
        sb.AppendLine($"Designation: {r.Designation},");
        if (r.Mobile.Length > 0) sb.AppendLine($"Mobile: {r.Mobile},");
        sb.AppendLine($"Section: {r.Section},");
        sb.AppendLine($"Inspection: {r.InspectionLine}.");
        if (r.Departure is not null) sb.AppendLine($"{r.FromStation} departure: {Fmt.Hrs(r.Departure)},");
        if (r.Arrival is not null) sb.AppendLine($"{r.ToStation} arrival: {Fmt.Hrs(r.Arrival)},");
        if (r.LocoLine.Length > 0) sb.AppendLine($"Engine No. {r.LocoLine}.");
        var crew = new List<string>();
        if (r.LocoPilot.Length > 0) crew.Add($"LP: {Fmt.WithShri(r.LocoPilot, p.UseShri)}");
        if (r.AssistantLocoPilot.Length > 0) crew.Add($"{r.AlpDesignation}: {Fmt.WithShri(r.AssistantLocoPilot, p.UseShri)}");
        if (r.TrainManager.Length > 0) crew.Add($"{r.TmDesignation}: {Fmt.WithShri(r.TrainManager, p.UseShri)}");
        for (int i = 0; i < crew.Count; i++) sb.AppendLine(crew[i] + (i == crew.Count - 1 ? "." : ","));

        sb.AppendLine("Observations :");
        int n = 1;
        foreach (var (text, sub) in ObservationItems(r))
        {
            sb.AppendLine($"{n++}. {text}");
            for (int k = 0; k < sub.Count; k++) sb.AppendLine($"  {Fmt.Roman(k + 1)}. {sub[k]}");
        }
        sb.AppendLine();
        sb.AppendLine("Shortcomings:");
        if (r.Shortcomings.Count == 0) sb.AppendLine("Nil.");
        else if (r.Shortcomings.Count == 1) sb.AppendLine(r.Shortcomings[0]);
        else for (int k = 0; k < r.Shortcomings.Count; k++) sb.AppendLine($"{k + 1}. {r.Shortcomings[k]}");
        if (r.Locations.Count > 0)
        {
            sb.AppendLine("Locations to be attended for better running:");
            sb.AppendLine(Locations.Join(r.Locations));
        }
        if (r.ActionTaken.Trim().Length > 0) sb.AppendLine($"Action taken: {r.ActionTaken.Trim()}");
        if (r.Remarks.Trim().Length > 0) sb.AppendLine($"Remarks: {r.Remarks.Trim()}");
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <summary>Shortcoming lines for field staff found not alert / sleeping / without jacket.</summary>
    public static List<string> DeficientStaff(InspectionReport r) =>
        r.Sightings.Where(s => s.IsDeficient).Select(s =>
        {
            var where = ReportLayout.KmDisplay(s.Location);
            var what = new List<string>();
            if (s.Condition is "Not alert" or "Sleeping" or "Absent" or "Inside cabin")
                what.Add(s.Condition switch { "Sleeping" => "found sleeping", "Absent" => "not found on duty", "Inside cabin" => "found inside the cabin", _ => "not alert" });
            if (s.Jacket == "Not worn") what.Add("without reflective jacket");
            return $"{s.Category} at {where} ({Fmt.Nos(s.Count)}) {string.Join(" and ", what)}.";
        }).ToList();

    /// <summary>A remarks paragraph in the usual words, based on what the report contains.</summary>
    public static string SuggestRemarks(InspectionReport r)
    {
        if (r.Shortcomings.Count == 0 && r.Locations.Count == 0)
            return "The overall condition of the section was found satisfactory. No deficiency was noticed during the inspection.";
        var sb = new StringBuilder("The overall condition of the section was satisfactory except for the deficiencies noted above. ");
        sb.Append("Necessary action may please be taken ");
        var parts = new List<string>();
        if (r.Locations.Count > 0) parts.Add("for the locations requiring attention for better running");
        if (r.Shortcomings.Count > 0) parts.Add("on the shortcomings observed");
        sb.Append(string.Join(" and ", parts)).Append('.');
        if (r.Sightings.Any(s => s.IsDeficient))
            sb.Append(" The lapses of field staff may be brought to the notice of the concerned officials for suitable action.");
        return sb.ToString();
    }

    /// <summary>Points an inspection report should not miss; shown before saving.</summary>
    public static List<string> Check(InspectionReport r)
    {
        var w = new List<string>();
        if (r.Date > DateOnly.FromDateTime(DateTime.Today.AddDays(1))) w.Add("Date is in the future.");
        if (r.TrainNo.Trim().Length == 0) w.Add("Train number is blank.");
        if (r.FromStation.Trim().Length == 0 || r.ToStation.Trim().Length == 0) w.Add("From / To station is blank.");
        else if (string.Equals(r.FromStation.Trim(), r.ToStation.Trim(), StringComparison.OrdinalIgnoreCase)) w.Add("From and To station are the same.");
        if (r.Departure is null || r.Arrival is null) w.Add("Departure or arrival time is blank.");
        else if (r.Duration is TimeSpan d && d > TimeSpan.FromHours(14)) w.Add($"Footplate time is {Fmt.Duration(d)} - please check the times.");
        if (r.LocoNo.Trim().Length == 0) w.Add("Loco (engine) number is blank.");
        if (r.LocoPilot.Trim().Length == 0) w.Add("Loco Pilot's name is blank.");
        if (r.Mobile.Trim().Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(r.Mobile.Trim(), @"^\+?\d[\d\s-]{8,13}$")) w.Add("Mobile number does not look right.");
        foreach (var s in r.Sightings.Where(s => s.Location.Trim().Length == 0)) w.Add($"A {s.Category} entry has no location.");
        return w;
    }
}
