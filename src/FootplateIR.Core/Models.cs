using System.Text.Json.Serialization;

namespace FootplateIR.Core;

/// <summary>Everything the app keeps: inspector profile, reports, master lists.</summary>
public sealed class AppData
{
    public int Version { get; set; } = 1;
    public Profile Profile { get; set; } = new();
    public List<InspectionReport> Reports { get; set; } = new();
    public Masters Masters { get; set; } = new();
    /// <summary>Reports deleted on this device (so a merge with the other device does not bring them back).</summary>
    public List<Tombstone> Deleted { get; set; } = new();
    /// <summary>Patrolling charts (time - distance diagrams) of the section.</summary>
    public List<PatrolChart> Charts { get; set; } = new() { Patrol.WinterChartKotaCor2026() };
    /// <summary>Night duty lists of patrolmen / keymen with mobile numbers.</summary>
    public List<DutyRoster> Rosters { get; set; } = new();
    /// <summary>Train schedules (timetable) kept in the app.</summary>
    public List<TrainSchedule> Trains { get; set; } = new();
    public Settings Settings { get; set; } = new();
}

public sealed class Settings
{
    /// <summary>Print the patrolling chart check in the inspection note.</summary>
    public bool PrintChartCheck { get; set; } = true;
    /// <summary>Distance (km) within which a patrol pair counts as "as per chart".</summary>
    public double ChartToleranceKm { get; set; } = 1.0;
    /// <summary>Windows: folder (OneDrive / Google Drive) used to keep Windows and Android in step.</summary>
    public string SyncFolder { get; set; } = "";
    public DateTime? LastSyncUtc { get; set; }
    public int SchemaVersion { get; set; } = 1;
}

public sealed class Tombstone
{
    public Guid Id { get; set; }
    public DateTime DeletedUtc { get; set; }
}

/// <summary>The inspecting official and the default heading / addressee of the reports.</summary>
public sealed class Profile
{
    public string Name { get; set; } = "Dharam Pal Jangid";
    public string Designation { get; set; } = "SSE(P.Way) BUDI";
    public string Mobile { get; set; } = "7976273600";
    public string Section { get; set; } = "Kota-COR";
    public string HqStation { get; set; } = "BUDI";
    public string Railway { get; set; } = "West Central Railway";
    public string Division { get; set; } = "Kota Division";
    public string Department { get; set; } = "Engineering Department";
    /// <summary>Third heading line of the inspection note.</summary>
    public string Office { get; set; } = "Office of the Senior Section Engineer (P.Way), Bundi";
    public string Salutation { get; set; } = "Sir,";
    /// <summary>Addressee printed on the report ("To, Sr. DEN/Co-ord/KOTA"); blank = none.</summary>
    public string Addressee { get; set; } = "";
    public List<string> CopyTo { get; set; } = new();
    /// <summary>Use "Shri" before names of the crew in the reports.</summary>
    public bool UseShri { get; set; } = true;
}

public sealed class InspectionReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Where the report came from (typed, WhatsApp text, photo, Word, PDF, Excel).</summary>
    public string Source { get; set; } = "Typed";

    // ---- Particulars ----
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string InspectorName { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Section { get; set; } = "";
    /// <summary>Night Footplate Inspection / Day Footplate Inspection / ...</summary>
    public string Kind { get; set; } = "Night Footplate Inspection";
    /// <summary>Safety, Monsoon, Fog ... (printed as "(Safety+Monsoon)").</summary>
    public List<string> Purposes { get; set; } = new() { "Safety" };
    public string FromStation { get; set; } = "";
    public string ToStation { get; set; } = "";
    public string TrainNo { get; set; } = "";
    public string TrainName { get; set; } = "";
    public TimeOnly? Departure { get; set; }
    public TimeOnly? Arrival { get; set; }

    // ---- Locomotive and crew ----
    public string LocoNo { get; set; } = "";
    public string LocoType { get; set; } = "";
    public string Cab { get; set; } = "";
    public string Shed { get; set; } = "";
    public string LocoPilot { get; set; } = "";
    /// <summary>Crew headquarters printed after the name ("Shri D.K. Mandal, Kota").</summary>
    public string LocoPilotHq { get; set; } = "";
    public string AlpDesignation { get; set; } = "Sr. ALP";
    public string AssistantLocoPilot { get; set; } = "";
    public string AlpHq { get; set; } = "";
    public string TmDesignation { get; set; } = "TM";
    public string TrainManager { get; set; } = "";
    public string TmHq { get; set; } = "";

    // ---- Observations ----
    /// <summary>Lead-in for the field staff item ("En-route Gate Keepers, ... were checked as under:").</summary>
    public string FieldStaffNote { get; set; } = "";
    public List<StaffSighting> Sightings { get; set; } = new();
    /// <summary>General observations in order (standard texts ticked plus any others).</summary>
    public List<string> Observations { get; set; } = new();
    public string Visibility { get; set; } = "Clear throughout";
    public string Weather { get; set; } = "";

    // ---- Shortcomings and follow-up ----
    public List<string> Shortcomings { get; set; } = new();
    public List<AttentionLocation> Locations { get; set; } = new();
    /// <summary>Action taken / advised, printed after the locations.</summary>
    public string ActionTaken { get; set; } = "";
    public string Remarks { get; set; } = "";

    [JsonIgnore] public string Route => $"{FromStation} - {ToStation}";

    /// <summary>"Night Footplate Inspection (Safety+Monsoon) ex BUDI to CNA by 01910"</summary>
    [JsonIgnore]
    public string InspectionLine
    {
        get
        {
            var p = Purposes.Count > 0 ? $" ({string.Join("+", Purposes)})" : "";
            var route = FromStation.Length + ToStation.Length > 0 ? $" ex {FromStation} to {ToStation}" : "";
            var by = TrainNo.Length > 0 ? $" by {TrainNo}" + (TrainName.Length > 0 ? $" ({TrainName})" : "") : "";
            return $"{Kind}{p}{route}{by}";
        }
    }

    /// <summary>"22228/WAP4/Cab2" or "30522/WAP7/Cab1/SDAH"</summary>
    [JsonIgnore]
    public string LocoLine => string.Join("/", new[] { LocoNo, LocoType, Cab, Shed }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>Journey time on the footplate (arrival may be after midnight).</summary>
    [JsonIgnore]
    public TimeSpan? Duration
    {
        get
        {
            if (Departure is not TimeOnly d || Arrival is not TimeOnly a) return null;
            var t = a.ToTimeSpan() - d.ToTimeSpan();
            if (t < TimeSpan.Zero) t += TimeSpan.FromDays(1);
            return t;
        }
    }

    public InspectionReport Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this, FirJsonContext.Default.InspectionReport);
        return System.Text.Json.JsonSerializer.Deserialize(json, FirJsonContext.Default.InspectionReport)!;
    }
}

/// <summary>A gate keeper / watchman / patrolman seen en-route.</summary>
public sealed class StaffSighting
{
    public string Category { get; set; } = "Monsoon Patrolman";
    /// <summary>KM 48/17, LC 66, JNRI ...</summary>
    public string Location { get; set; } = "";
    public int Count { get; set; } = 1;
    /// <summary>Alert / Not alert / Sleeping / Inside cabin / Absent</summary>
    public string Condition { get; set; } = "Alert";
    /// <summary>Worn / Not worn / blank (not noted)</summary>
    public string Jacket { get; set; } = "";
    public string Remarks { get; set; } = "";

    [JsonIgnore]
    public bool IsDeficient =>
        Condition is "Not alert" or "Sleeping" or "Absent" or "Inside cabin" || Jacket == "Not worn";
}

/// <summary>A track location felt during the run that needs attention for better running.</summary>
public sealed class AttentionLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>"35/10-15" (KM/TP) or "JNRI S3 to S4"</summary>
    public string Location { get; set; } = "";
    /// <summary>SEJ, point, LC, bridge ... (printed in brackets)</summary>
    public string Feature { get; set; } = "";
    public string Status { get; set; } = LocationStatus.Pending;
    public DateOnly? AttendedOn { get; set; }
    public string ActionTaken { get; set; } = "";

    [JsonIgnore] public bool IsKm => Locations.IsKm(Location);
    /// <summary>Printed form: "KM 35/10-15", "61/12 (SEJ)", "JNRI S3 to S4".</summary>
    [JsonIgnore] public string Display => Location + (Feature.Length > 0 ? $" ({Feature})" : "");
    /// <summary>Key used to find the same spot reported again (KM and first telegraph post, or yard text).</summary>
    [JsonIgnore] public string Key => Locations.KeyOf(Location);
}

public static class LocationStatus
{
    public const string Pending = "Pending";
    public const string Attended = "Attended";
    public const string Watch = "Under watch";
    public static readonly string[] All = { Pending, Attended, Watch };
}

/// <summary>Editable lists used by the pickers.</summary>
public sealed class Masters
{
    public List<string> Kinds { get; set; } = new()
    {
        "Night Footplate Inspection", "Day Footplate Inspection", "Footplate Inspection", "Surprise Footplate Inspection"
    };
    public List<string> Purposes { get; set; } = new()
    {
        "Safety", "Monsoon", "Fog", "Winter", "Summer", "Track", "Signal sighting", "Speed trial", "Special drive"
    };
    public List<string> StandardObservations { get; set; } = new(Defaults.StandardObservations);
    public List<string> SightingCategories { get; set; } = new()
    {
        "Gate Keeper", "Stationary Watchman", "Monsoon Patrolman", "Hot Weather Patrolman", "Cold Weather Patrolman",
        "Keyman", "Night Patrolman", "Bridge Watchman", "Trackman"
    };
    public List<string> Conditions { get; set; } = new() { "Alert", "Not alert", "Sleeping", "Inside cabin", "Absent" };
    public List<string> JacketOptions { get; set; } = new() { "", "Worn", "Not worn" };
    public List<string> Visibility { get; set; } = new()
    {
        "Clear throughout", "Clear", "Moderate", "Poor (fog)", "Dense fog", "Hazy", "Poor (rain)"
    };
    public List<string> LocoTypes { get; set; } = new()
    {
        "WAP4", "WAP5", "WAP7", "WAG7", "WAG9", "WAG9H", "WAG12B", "WDM3A", "WDP4", "WDG4", "MEMU", "EMU", "DEMU", "Vande Bharat"
    };
    public List<string> Sheds { get; set; } = new() { "TKD", "GZB", "BRC", "SDAH", "LDH", "ET", "NKJ", "BSL", "JHS", "ABR", "AJJ", "KYN", "VSKP", "RTM", "HWH", "GMO", "BNDM" };
    public List<string> LocoPilots { get; set; } = new();
    public List<string> AssistantLocoPilots { get; set; } = new();
    public List<string> TrainManagers { get; set; } = new();
    /// <summary>Stations added by the user (code = name), in addition to the built-in network.</summary>
    public List<StationEntry> ExtraStations { get; set; } = new();
    /// <summary>Running KM of the stations on the section (used for the patrolling chart check and "who is on duty here").</summary>
    public List<StationKm> Chainage { get; set; } = Patrol.DefaultChainage();
    /// <summary>OHE masts added in the app (stretches not in the built-in GQL - SMPA register).</summary>
    public List<OheMast> ExtraMasts { get; set; } = new();
    public List<string> DutyCategories { get; set; } = new()
    {
        "Cold Weather Patrolman", "Monsoon Patrolman", "Hot Weather Patrolman", "Night Patrolman", "Keyman", "Stationary Watchman"
    };
}

public sealed class StationEntry
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public static class Defaults
{
    public static readonly string[] StandardObservations =
    {
        "SMs and pointsmen were found alert and they exchanged alright signals with LP & ALP.",
        "LP and ALP were also found alert and they exchanged alright signals with on duty station staff en-route and called out to each other the aspects of signals and indicators pertaining to CDs, PSRs etc.",
        "Train Manager also reminded the LP of the forthcoming CDs.",
        "The section was found normal."
    };

    public const string FieldStaffNote = "En-route Gate Keepers, Stationary Watchmen and Monsoon Patrolmen were checked as under:";

    /// <summary>Exact author credit printed on every document.</summary>
    public const string AuthorCredit = "DHARAM PAL JANGID, Senior Section Engineer (P.Way) Kota Division of WCRly";

    public static InspectionReport NewReport(Profile p, Masters m) => new()
    {
        InspectorName = p.Name,
        Designation = p.Designation,
        Mobile = p.Mobile,
        Section = p.Section,
        FieldStaffNote = FieldStaffNote,
        Observations = new List<string>(m.StandardObservations),
        Kind = DateTime.Now.Hour is >= 6 and < 18 ? "Day Footplate Inspection" : "Night Footplate Inspection"
    };
}

[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(AppData))]
[JsonSerializable(typeof(InspectionReport))]
[JsonSerializable(typeof(List<InspectionReport>))]
[JsonSerializable(typeof(List<DutyRoster>))]
[JsonSerializable(typeof(PatrolChart))]
[JsonSerializable(typeof(List<TrainSchedule>))]
public partial class FirJsonContext : JsonSerializerContext { }
