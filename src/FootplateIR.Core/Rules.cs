namespace FootplateIR.Core;

/// <summary>One rule / instruction with its book and reference.</summary>
public sealed record RuleItem(string Book, string Ref, string Title, string Text, string Topic);

/// <summary>A shortcoming in the usual words, with the rule it falls under.</summary>
public sealed record StandardShortcoming(string Text, string Ref);

/// <summary>One inspection the schedule requires, and what was done in the period.</summary>
public sealed class ScheduleStatus
{
    public string Item { get; init; } = "";
    public string Ref { get; init; } = "";
    public string Required { get; init; } = "";
    public string Period { get; init; } = "";
    public List<DateOnly> Done { get; init; } = new();
    /// <summary>Done / Not done / Due / Not applicable</summary>
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
}

/// <summary>
/// Footplate rules and instructions from the Indian Railways Permanent Way Manual 2024 (corrected up to ACS 16, 30.09.2026)
/// and the WCR General & Subsidiary Rules 2016 (updated copy), as quoted from the user's copies; the inspection schedule check.
/// </summary>
public static class Rules
{
    public const string Irpwm = "IRPWM 2024 (up to ACS 16)";
    public const string Gsr = "WCR G&SR 2016 (updated)";

    public const string RoleInCharge = "SSE/P.Way (In-charge)";
    public const string RoleSectional = "JE/SSE/P.Way (Sectional)";
    public const string RoleAden = "ADEN";
    public static readonly string[] Roles = { RoleInCharge, RoleSectional, RoleAden };

    /// <summary>Items to check on a night footplate inspection (IRPWM 2024 Table-1A Sl. 13, Table-1B Sl. 14).</summary>
    public static readonly string[] NightFootplateChecks =
    {
        "Alertness of Gatemen",
        "Alertness of Station staff",
        "Alertness of Patrolmen",
        "Alertness of Stationary watchmen",
        "Observance of speed limits by Loco Pilots",
        "Visibility of signals / engineering fixed signals / hectometre posts",
        "Riding quality"
    };

    public const string ChecksRef = "IRPWM 2024 Table-1B Sl. 14";

    public static readonly RuleItem[] All =
    {
        // ------------------------------------------------ IRPWM 2024: schedules
        new(Irpwm, "Table-1B Sl. 14", "Night foot plate inspection (SSE/P.Way In-charge and JE/SSE/P.Way Sectional)",
            "Once in a month by SSE/P.Way (In-charge) and JE/SSE/P.Way (Sectional) - to check alertness of Gateman / Station staff, Patrolmen, Stationary watchmen, observance of speed limits by Loco Pilots, visibility of signals/engineering fixed signals / hectometre posts, riding quality etc. Inspection should preferably be done between 00.00 hrs. to 04.00 hrs.",
            "Schedule"),
        new(Irpwm, "Table-1B Sl. 3", "Loco / Brake van / Rear window inspection",
            "SSE/P.Way (In-charge): Once in a fortnight. JE/SSE/P.Way (Sectional): Once in a month.", "Schedule"),
        new(Irpwm, "Table-1B Sl. 11", "Night Inspection", "Once in a month by SSE/P.Way (In-charge).", "Schedule"),
        new(Irpwm, "Table-1B Sl. 12", "Monsoon Patrolling (checking)",
            "SSE/P.Way (In-charge) - once in a month. JE/SSE/P.Way (Sectional) - once in fortnight by train and inspection by trolley - as per the schedule laid down by administration.", "Schedule"),
        new(Irpwm, "Table-1B Sl. 13", "Hot Weather Patrolling (checking)",
            "SSE/P.Way (In-charge): When introduced; should check the work of Patrolmen during day time (preferably between 12:00 to 16:00 hrs.) once in a month either by Train/Push trolley/Motor Trolley.", "Schedule"),
        new(Irpwm, "Table-1B Sl. 23", "Work of other organisations (RVNL, DFCCIL, Construction etc.)",
            "As much as possible during Foot plate/Trolley Inspection to check quality of on-going work and safety of the running trains on adjacent lines by SSE/P.Way (In-charge) and JE/SSE/P.Way (Sectional) both.", "Schedule"),
        new(Irpwm, "Table-1A Sl. 13", "Night foot plate inspection (ADEN)",
            "Once in a month - To check alertness of Gateman/ Station staff, Patrolmen, Stationary watchmen, observance of speed limits by Loco Pilots, visibility of signals/engineering fixed signals / hectometre posts, riding quality etc. (Inspection should preferably be done between 00.00 hrs. to 04.00 hrs.)",
            "Schedule"),
        new(Irpwm, "Table-1A Sl. 3", "Fast Train Inspection (ADEN)",
            "Once in a month - Entire sub-division to be covered by either Engine or Rear Window of fast train.", "Schedule"),
        new(Irpwm, "Table-1A Sl. 11, 12", "Monsoon / Hot weather patrolling (ADEN)",
            "Monsoon Patrolling: When introduced; should check the work of Patrolmen at night once in a month either by Train/Push trolley/Motor Trolley. Hot Weather Patrolling: When introduced; should check the work of Patrolmen during day time (preferably between 12:00 to 16:00 hrs.) once in a month either by Train/Push trolley/Motor Trolley.",
            "Schedule"),
        // ------------------------------------------------ IRPWM 2024: duties
        new(Irpwm, "Para 106(1)(c), (d)", "Duties of SSE/P.Way (In-charge) - footplate",
            "(c) Observe the behaviour of track under passing trains to detect inadequate packing during routine inspections. (d) Travel on the footplate of Engine / Rear brake-van/last Vehicle of fast trains, take down notes of bad running locations, and get them rectified.",
            "Duties"),
        new(Irpwm, "Para 109(1)(d)", "Duties of JE/SSE/P.Way (Sectional) - footplate",
            "Travel on the footplate of Engine /Rear brake-van/last Vehicle of fast trains, take down notes of bad running locations, and get them rectified.", "Duties"),
        new(Irpwm, "Para 602(a)", "Post monsoon attention",
            "Immediately after cessation of monsoon, attention to run down lengths, condition based maintenance or works incidental to requirement of urgent maintenance as projected by OMS/TRC recording or identified during footplate inspections should be taken up in the entire gang beat to restore section to good shape.",
            "Maintenance"),
        new(Irpwm, "Para 603(2)", "Maintenance planning",
            "Every SSE/P.Way (In-charge) should prepare a perspective maintenance plan of his section in advance based on various track recording results and exception reports from TMS. This should also take account of trolley and footplate inspections and inspection of higher officials so that optimum utilization of various resources, track machines, traffic blocks, and labour etc. is possible.",
            "Maintenance"),
        new(Irpwm, "Para 636(3) Table III; Para 708 Tables II(A)-(C)", "Speed raising after deep screening / renewals",
            "Footplate / last vehicle inspection and speed raising to 130 Kmph and then 160 Kmph is the last stage of the schedule of work and speed restrictions after deep screening by BCM with DTS (Para 636(3)) and after track renewals (Para 708).",
            "Maintenance"),
        new(Irpwm, "Para 644", "OHE Mast / Hectometre Post Numbers",
            "(1) Kilometerage should be indicated on OHE Mast/Hectometer Post either by painting or providing number plates thereon. (3) In deep-cuttings or tunnels, OHE Mast/Hectometer post numbers should be repeated at cess level. (5) On electrified sections, the kilometerage is indicated on the structure posts. The responsibility of providing number plates or painting kilometerage on the Electric structure devolves on the Electrical Department.",
            "Maintenance"),
        new(Irpwm, "Para 1307(h)", "Registers - footplate inspection",
            "Push Trolley / Footplate / Rear window inspection Registers. (Footplate inspection booklet of JE/SSE/P.Way/ ADEN / DEN / Sr. DEN is to be maintained but entry to be made in TMS by respective officials.)",
            "Records"),
        // ------------------------------------------------ IRPWM 2024: patrolling
        new(Irpwm, "Para 1002", "Keyman's Daily Patrol",
            "Every portion of the permanent way shall be inspected daily on foot by the keyman of the beat in which the portion of the track falls. The Keyman shall preferably be provided with a GPS tracking device, to monitor his movements so as to ensure effective patrolling.",
            "Patrolling"),
        new(Irpwm, "Para 1004(2)", "Preparation of Patrol Charts",
            "The length of each patrol beat should not normally exceed 5 km. The walking speed of a Patrolman may be taken as 3 Kmph. The maximum distance covered by a Patrolman should not normally exceed 20 km in a day. A period of at least half an hour rest is desirable between consecutive beats. Patrol charts should show all vulnerable locations where stationary watchmen are posted.",
            "Patrolling"),
        new(Irpwm, "Para 1004(3)", "Distribution of Patrol Charts - Loco Pilots to look out",
            "The Loco inspectors will acquaint Loco pilot of passenger trains, when they may expect to pass patrolmen, if running to time. By inviting Loco pilots to lookout for patrolling, an immediate and practical means of supervising the Patrolmen is introduced. The task of dispatching Patrolmen at the right time and signing their patrol book when they arrive at or depart from a station devolves upon the Station Master/Block Hut-in-charge.",
            "Patrolling"),
        new(Irpwm, "Para 1004(4)", "Patrol Books and Systematic Patrolling",
            "Patrolmen shall be on duty at the time specified in the patrol chart. If a Patrolman who is due to arrive at a station does not turn up in time or does not turn up at all, the Station Master/Block hut-in-charge will advise Station Master/Block hut-in-charge at the other end of the block section about the absence of the Patrolman and both will issue caution orders to all trains entering the section until the Patrolman from the other end of the patrol-section arrives at the station and reports that all is well.",
            "Patrolling"),
        new(Irpwm, "Para 1004(5)", "Equipment of Monsoon Patrolmen",
            "Staff (preferably foldable); number plate 15 cm square; 10 detonators in a tin case; warning signals (red flashing rechargeable LED torch/hand signal lamp at night or red flag during day) - three on double/multiple lines, Ghat, suburban and automatic block territories and two on single line; protective clothing including industrial safety shoe / gum boots, safety jacket, rain coat, helmet with head light; one match box; two red flags and one green flag (day patrol only); patrol book in a tin case; one three cell electric torch; whistle thunderer; one haversack.",
            "Patrolling"),
        new(Irpwm, "Para 1004(9)(e)", "Check over Patrolling at Nights",
            "Schedule of inspection of night patrolling by train: JE/SSE/P.Way (Sectional) - once a fortnight; SSE/P.Way (In-charge) - once a month. ADEN shall cover his entire sub-division once in a month by train in the night and check the Patrolmen.",
            "Patrolling"),
        new(Irpwm, "Para 1004(9)(a), (d)", "Patrol books and equipment",
            "JE/SSE/P.Way must examine the patrol books, initial the entries each time he inspects and take corrective measures for irregularities noticed. JE/SSE/P.Way should check the equipment of all patrolmen and watchmen once a month, record the results in patrol book and take steps to recoup deficiencies.",
            "Patrolling"),
        new(Irpwm, "Para 1005(3), (4)", "Cold Weather Patrolling",
            "Period and section shall be laid down by Sr. DEN (Co) based on rail/weld fracture analysis, rail corrosion, overdue rail renewals and traffic density; compulsorily started when rail temperature goes below (td - 30 C). Beat: single line (or one road of double line with LWR/CWR) - one patrolman for four kilometre; double line with LWR/CWR on both roads - one patrolman for two km of UP and DN road. Equipment includes 10 fog signals, two tri-colour hand signal lamps/LED torch, two red flags and one green flag, three-cell torch, staff, number plate, spanner and protective clothing including safety jacket and helmet with head light.",
            "Patrolling"),
        new(Irpwm, "Para 1005(5)", "Cold Weather Patrolman - duties",
            "He shall patrol the track during the coldest part of the night and look out for weld/rail fractures and excessive gaps at SEJ. He will walk over his beat slowly along one rail in one direction and on the other rail in the return direction. In case he notices a rail/weld failure or gap at SEJ more than the designed maximum gap, he will take immediate action to suspend the traffic and protect the line as per Para 812. The patrolman shall preferably be provided with a GPS tracking device.",
            "Patrolling"),
        new(Irpwm, "Para 1006", "Watchmen at vulnerable locations",
            "Stationary Watchman should be posted round the clock at every nominated location during the monsoon period, as required. He should keep a watch on vulnerable location and in case he apprehends danger he should take action to protect the line in accordance with Para 1004(8). A list of Vulnerable Locations should be maintained by each ADEN/DEN in a register form.",
            "Patrolling"),
        // ------------------------------------------------ G&SR
        new(Gsr, "SR 4.22.1", "Riding on engine for foot plate inspection",
            "Officers and Sr. Supervisors of Electrical, Mechanical, S&T, Engineering, Operating and Safety departments provided with duty passes are allowed to ride on engine for the purpose of foot plate inspection. Number of such officials should not exceed three on the engine at a time, except in emergency or when permitted by Operating/Power Officer.",
            "Footplate"),
        new(Gsr, "GR 4.40; SR 4.40.1", "Look out and calling out of signal aspects",
            "Every Loco Pilot shall keep a good look-out while the train is in motion, and every Assistant Loco Pilot shall also do so when he is not necessarily otherwise engaged. The Asstt. Loco Pilot shall assist the Loco Pilot in sighting the signal aspects. He shall call out the aspects displayed by the signals from the place from where the signals can be sighted. The Loco Pilot, after personally verifying the correct aspect of the signal shall repeat the same (colour light: Distant - Green/Double Yellow/Yellow; Stop signals - Green/Double Yellow/Yellow/Red).",
            "Loco crew"),
        new(Gsr, "GR 4.41; SR 4.41.1", "Looking back",
            "The Loco Pilot and the Assistant Loco Pilot shall look back frequently during the journey to see whether the train is following in a safe and proper manner. When a train passes a gang working on the track or a manned level crossing gate, the Loco Pilot or Asstt. Loco Pilot shall look back to ascertain that every thing is all right with the train.",
            "Loco crew"),
        new(Gsr, "GR 4.42(2)", "All right signals while running through a station",
            "The Loco Pilot and the Guard of a train shall, while running through a station, look out and, except under special instructions, acknowledge the 'all right' signals which the Station Master and such other staff at the station as may be specified by special instructions shall give if the train is proceeding in a safe and proper manner.",
            "Station staff"),
        new(Gsr, "SR 4.42.1", "All right signals between Guard and Loco Pilot",
            "All right signal must be exchanged between the Guard and the Loco Pilot when a train starts after stopping at a station, starts after stopping between stations, and runs through a station - when the engine has passed the Advanced Starter or the outermost points. If the Loco Pilot does not get the signal from the Guard, he shall whistle and if there is no response, he shall stop the train and ascertain the cause.",
            "Loco crew"),
        new(Gsr, "SR 4.42.2", "Signals between Guard, Loco Pilot and Station staff",
            "When a train is running through a station, a green hand signal shall be exhibited by the SM on duty from the platform or from where it can be best seen by the Loco Pilot and the Guard. During night time, while exchanging all right signals with the stopping train, the cabin staff must not display the green hand signal light towards the approaching train; the green light should be at right angle to the track.",
            "Station staff"),
        new(Gsr, "SR 4.09.1(1)", "Caution orders - when issued",
            "A Caution Order on form T/409 shall be issued to the Loco Pilot and the Guard by the SM on duty, among other cases: after emergency repair of rail fracture/weld failure; when rough running or lurch is reported by the Loco Pilot; when Patrolman does not report within 15 minutes of his scheduled arrival at the station; when water level rises over the danger level marked at bridges.",
            "Loco crew"),
        new(Gsr, "GR 15.05; SR 15.05.1(1)-(2)", "Patrolling of lines - monsoon",
            "The line is to be patrolled at night from the commencement until the close of the monsoon, ordinarily from June 1st until October 31st; exact dates decided by the ADEN. SMs record the timings of arrival and departure of Patrolmen in the patrol books and the station diary and see that the Patrolmen come on duty in sober condition.",
            "Patrolling"),
        new(Gsr, "SR 15.05.1(3)", "Patrolman not turning up",
            "If a Patrolman does not turn up within 15 minutes of his scheduled arrival, the SM on duty shall immediately stop all trains proceeding into the block section, advise the SM at the other end and the Controller, and issue a Caution Order on Form T/409 advising the Loco Pilot to be alert and proceed at a speed not exceeding 40 KMPH during the day when visibility is clear, and 15 KMPH during night or when visibility is not clear, till the Patrolman has arrived and reported that the line is safe.",
            "Patrolling"),
        new(Gsr, "GR 16.04; SR 16.04.1", "Gateman to observe passing trains",
            "The Gateman shall observe all passing trains and be prepared to take such action as may be necessary to ensure safety of trains. During passage of trains, gatemen shall stand attentively in front of the gate lodge facing the approaching train; in day time hold red and green flags furled up; in night time hold lighted hand signal lamp with white light facing the track; keep the whistle slung around his neck.",
            "Gateman"),
        new(Gsr, "SR 16.04.2", "Gateman - unusual occurrence on train",
            "The Gateman shall watch all passing trains and keep a sharp look out for any unusual condition like hot axle, loose/hanging part of any vehicle, fire in train, load shifted, spring broken etc., warn the Loco Pilot/Guard by showing danger hand signal and whistling, and if the crew fail to take notice, immediately inform the Station Master to stop the train.",
            "Gateman"),
        new(Gsr, "SR 16.04.3", "Gateman - tail lamp / tail board",
            "Gateman will see that Tail Lamp/Tail Board/other equipment is affixed on the last vehicle of each train passing his level crossing; if not, immediately inform the SM, and not open the gate for road traffic until it is safe.",
            "Gateman"),
    };

    /// <summary>Shortcomings in the usual words with their rule, offered in the report form.</summary>
    public static readonly StandardShortcoming[] StandardShortcomings =
    {
        new("Gateman at LC __ not alert / not standing in front of the gate lodge facing the approaching train with lighted hand signal lamp.", "G&SR SR 16.04.1"),
        new("SM / pointsman at __ did not exhibit all right signal to the passing train.", "G&SR GR 4.42(2), SR 4.42.2"),
        new("ALP did not call out the signal aspects / LP did not repeat them at __.", "G&SR SR 4.40.1"),
        new("Monsoon patrolman at KM __ found without safety jacket / incomplete equipment.", "IRPWM 2024 Para 1004(5)"),
        new("Cold weather patrolman at KM __ found without safety jacket / incomplete equipment.", "IRPWM 2024 Para 1005(4)"),
        new("Patrolman at KM __ found sleeping / not on his beat as per patrol chart.", "IRPWM 2024 Para 1004(4)"),
        new("Stationary watchman not found at the vulnerable location at KM __.", "IRPWM 2024 Para 1006(3)"),
        new("Speed restriction at KM __ not observed by the Loco Pilot.", "G&SR SR 4.09.1"),
        new("OHE mast / hectometre post number at KM __ not legible.", "IRPWM 2024 Para 644"),
        new("Lurch / bad running felt at KM __ - to be attended.", "IRPWM 2024 Para 106(1)(d)"),
    };

    public static IEnumerable<RuleItem> Search(string? text, string? book = null)
    {
        var q = (text ?? "").Trim();
        return All.Where(r => (book is null || r.Book == book) &&
                              (q.Length == 0 || r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Text.Contains(q, StringComparison.OrdinalIgnoreCase)
                               || r.Ref.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Topic.Contains(q, StringComparison.OrdinalIgnoreCase)));
    }

    // ------------------------------------------------------------------ inspection schedule

    /// <summary>True when the run on the footplate touches 00.00 - 04.00 hrs (preferred time of the night footplate).</summary>
    public static bool InPreferredWindow(InspectionReport r)
    {
        if (r.Departure is not TimeOnly d) return false;
        var a = r.Arrival ?? d;
        double s = Patrol.NightMinutes(d), e = Patrol.NightMinutes(a);
        if (e < s) e += 1440;
        double w0 = Patrol.NightMinutes(new TimeOnly(0, 0)), w1 = Patrol.NightMinutes(new TimeOnly(4, 0));
        return s <= w1 && e >= w0;
    }

    public static bool IsNight(InspectionReport r) =>
        r.Kind.Contains("Night", StringComparison.OrdinalIgnoreCase) ||
        (r.Departure is TimeOnly t && (t.Hour >= 22 || t.Hour < 5));

    public static bool ChecksMonsoon(InspectionReport r) =>
        r.Purposes.Any(p => p.Contains("Monsoon", StringComparison.OrdinalIgnoreCase)) ||
        r.Sightings.Any(s => s.Category.Contains("Monsoon", StringComparison.OrdinalIgnoreCase));

    public static bool ChecksHotWeather(InspectionReport r) =>
        r.Purposes.Any(p => p.Contains("Summer", StringComparison.OrdinalIgnoreCase) || p.Contains("Hot", StringComparison.OrdinalIgnoreCase)) ||
        r.Sightings.Any(s => s.Category.Contains("Hot Weather", StringComparison.OrdinalIgnoreCase));

    /// <summary>Monsoon patrolling period, ordinarily 1 June to 31 October (SR 15.05.1(1)(a)).</summary>
    public static bool InMonsoon(DateOnly d) => d.Month is >= 6 and <= 10;

    /// <summary>What the schedule of the role requires in a month, and what the saved reports show was done.</summary>
    public static List<ScheduleStatus> Schedule(IEnumerable<InspectionReport> reports, string role, int year, int month, DateOnly? today = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var inMonth = reports.Where(r => r.Date >= first && r.Date <= last).OrderBy(r => r.Date).ToList();
        var list = new List<ScheduleStatus>();
        bool current = now >= first && now <= last;

        ScheduleStatus Item(string item, string reference, string required, string period, IEnumerable<InspectionReport> done, DateOnly from, DateOnly to, string note = "")
        {
            var dates = done.Where(r => r.Date >= from && r.Date <= to).Select(r => r.Date).Distinct().ToList();
            var st = new ScheduleStatus { Item = item, Ref = reference, Required = required, Period = period, Done = dates, Note = note };
            st.Status = dates.Count > 0 ? "Done" : now < from ? "Not yet due" : now <= to ? "Due" : "Not done";
            return st;
        }

        var night = inMonth.Where(IsNight).ToList();
        string windowNote = night.Count == 0 ? "" : night.Any(InPreferredWindow)
            ? "Done between 00.00 and 04.00 hrs as preferred."
            : "None of the night footplates touched 00.00 - 04.00 hrs (preferred time).";
        var mid = new DateOnly(year, month, 15);
        var monthName = first.ToString("MMM yyyy", Fmt.Inv);

        switch (role)
        {
            case RoleSectional:
                list.Add(Item("Night foot plate inspection", "Table-1B Sl. 14", "Once in a month (preferably 00.00 - 04.00 hrs)", monthName, night, first, last, windowNote));
                list.Add(Item("Loco / Brake van / Rear window inspection", "Table-1B Sl. 3", "Once in a month", monthName, inMonth, first, last));
                if (InMonsoon(first))
                {
                    list.Add(Item("Monsoon patrolling check by train", "Table-1B Sl. 12; Para 1004(9)(e)", "Once in a fortnight", $"1 - 15 {monthName}", inMonth.Where(ChecksMonsoon), first, mid));
                    list.Add(Item("Monsoon patrolling check by train", "Table-1B Sl. 12; Para 1004(9)(e)", "Once in a fortnight", $"16 - {last.Day} {monthName}", inMonth.Where(ChecksMonsoon), mid.AddDays(1), last));
                }
                break;
            case RoleAden:
                list.Add(Item("Night foot plate inspection", "Table-1A Sl. 13", "Once in a month (preferably 00.00 - 04.00 hrs)", monthName, night, first, last, windowNote));
                list.Add(Item("Fast train inspection (engine / rear window)", "Table-1A Sl. 3", "Once in a month - entire sub-division", monthName, inMonth, first, last));
                if (InMonsoon(first))
                    list.Add(Item("Monsoon patrolling check at night", "Table-1A Sl. 11; Para 1004(9)(e)", "Once in a month (when introduced)", monthName, inMonth.Where(r => ChecksMonsoon(r) && IsNight(r)), first, last));
                if (inMonth.Any(ChecksHotWeather))
                    list.Add(Item("Hot weather patrolling check (12.00 - 16.00 hrs)", "Table-1A Sl. 12", "Once in a month (when introduced)", monthName, inMonth.Where(ChecksHotWeather), first, last));
                break;
            default:
                list.Add(Item("Night foot plate inspection", "Table-1B Sl. 14", "Once in a month (preferably 00.00 - 04.00 hrs)", monthName, night, first, last, windowNote));
                list.Add(Item("Night inspection", "Table-1B Sl. 11", "Once in a month", monthName, night, first, last));
                list.Add(Item("Loco / Brake van / Rear window inspection", "Table-1B Sl. 3", "Once in a fortnight", $"1 - 15 {monthName}", inMonth, first, mid));
                list.Add(Item("Loco / Brake van / Rear window inspection", "Table-1B Sl. 3", "Once in a fortnight", $"16 - {last.Day} {monthName}", inMonth, mid.AddDays(1), last));
                if (InMonsoon(first))
                    list.Add(Item("Monsoon patrolling check by train", "Table-1B Sl. 12; Para 1004(9)(e)", "Once in a month", monthName, inMonth.Where(ChecksMonsoon), first, last));
                if (inMonth.Any(ChecksHotWeather))
                    list.Add(Item("Hot weather patrolling check (12.00 - 16.00 hrs)", "Table-1B Sl. 13", "Once in a month (when introduced)", monthName, inMonth.Where(ChecksHotWeather), first, last));
                break;
        }
        if (!current && now < first) foreach (var s in list) s.Status = "Not yet due";
        return list;
    }

    /// <summary>Advice on one report against the rules (shown before saving, never blocks).</summary>
    public static List<string> Advice(InspectionReport r)
    {
        var w = new List<string>();
        if (r.Kind.Contains("Night", StringComparison.OrdinalIgnoreCase) && r.Departure is not null && !InPreferredWindow(r))
            w.Add("Night foot plate inspection should preferably be done between 00.00 and 04.00 hrs (IRPWM 2024 Table-1B Sl. 14).");
        foreach (var s in r.Sightings.Where(s => s.Jacket == "Not worn" && s.Category.Contains("Patrol", StringComparison.OrdinalIgnoreCase)))
            w.Add($"{s.Category} at {ReportLayout.KmDisplay(s.Location)} without safety jacket - part of the patrolman's equipment (IRPWM 2024 Para {(s.Category.Contains("Cold") ? "1005(4)" : "1004(5)")}).");
        return w;
    }

    /// <summary>The schedule check as a printable document.</summary>
    public static DocModel ScheduleDocument(AppData data, string role, int year, int month)
    {
        var p = data.Profile;
        var rows = Schedule(data.Reports, role, year, month);
        var monthName = new DateOnly(year, month, 1).ToString("MMMM yyyy", Fmt.Inv);
        var doc = new DocModel { Title = $"Footplate inspection schedule - {monthName}", Footer = $"Inspection schedule as per {Irpwm} - {role} - {monthName}" };
        doc.Add(new TitleBlock { Text = p.Railway.ToUpperInvariant() + " - " + p.Division.ToUpperInvariant(), Size = 12 })
           .Add(new TitleBlock { Text = "FOOTPLATE / NIGHT INSPECTION SCHEDULE", Size = 13, Color = "000000" })
           .Add(new TitleBlock { Text = $"{p.Name}, {p.Designation} ({role})  |  {monthName}  |  as per {Irpwm}", Size = 9.5f, Bold = false, Color = Theme.Grey })
           .Add(new RuleBlock());
        doc.Add(new TableBlock
        {
            Headers = new[] { "Inspection", "IRPWM reference", "Required", "Period", "Done on", "Status" },
            Widths = new[] { 0.24f, 0.17f, 0.17f, 0.13f, 0.17f, 0.12f },
            Rows = rows.Select(x => new[] { x.Item + (x.Note.Length > 0 ? "\n" + x.Note : ""), x.Ref, x.Required, x.Period, string.Join(", ", x.Done.Select(Fmt.Date)), x.Status }).ToList(),
            HighlightRows = rows.Select((x, i) => (x, i)).Where(t => t.x.Status == "Not done").Select(t => t.i).ToHashSet(),
            FontSize = 8.5f
        });
        doc.Add(new SpacerBlock { Height = 16 });
        doc.Add(new SignatureBlock { Lines = new[] { $"({p.Name})", p.Designation } });
        return doc;
    }
}
