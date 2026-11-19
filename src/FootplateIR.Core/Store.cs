using System.Text;
using System.Text.Json;

namespace FootplateIR.Core;

public sealed class MergeSummary
{
    public int ReportsAdded { get; set; }
    public int ReportsUpdated { get; set; }
    public int ReportsDeleted { get; set; }
    public int RostersAdded { get; set; }
    public int ChartsAdded { get; set; }
    public override string ToString() =>
        $"{ReportsAdded} reports added, {ReportsUpdated} updated, {ReportsDeleted} removed, {RostersAdded} duty lists added" +
        (ChartsAdded > 0 ? $", {ChartsAdded} patrolling charts added" : "") + ".";
}

/// <summary>Saving, loading, backup and merging of the app data (Windows and Android keep each other up to date this way).</summary>
public static class Store
{
    public static AppData Load(string path)
    {
        if (!File.Exists(path)) return New();
        var json = File.ReadAllText(path, Encoding.UTF8);
        var data = JsonSerializer.Deserialize(json, FirJsonContext.Default.AppData) ?? New();
        Upgrade(data);
        return data;
    }

    public static AppData New()
    {
        var d = new AppData();
        Upgrade(d);
        return d;
    }

    /// <summary>Fills lists added in later versions.</summary>
    public static void Upgrade(AppData d)
    {
        d.Masters ??= new Masters();
        d.Settings ??= new Settings();
        d.Trains ??= new List<TrainSchedule>();
        if (d.Masters.Chainage.Count == 0) d.Masters.Chainage = Patrol.DefaultChainage();
        if (d.Masters.StandardObservations.Count == 0) d.Masters.StandardObservations = new List<string>(Defaults.StandardObservations);
    }

    /// <summary>Writes safely: to a temporary file first, keeping the previous copy as .bak.</summary>
    public static void Save(AppData data, string path)
    {
        var json = JsonSerializer.Serialize(data, FirJsonContext.Default.AppData);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json, Encoding.UTF8);
        if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);
        File.Move(tmp, path, overwrite: true);
    }

    public static string ToJson(AppData data) => JsonSerializer.Serialize(data, FirJsonContext.Default.AppData);

    /// <summary>Brings changes from another copy (the other device, a backup) into <paramref name="into"/>; newer edits win.</summary>
    public static MergeSummary Merge(AppData into, AppData from)
    {
        var s = new MergeSummary();
        foreach (var t in from.Deleted)
        {
            if (into.Deleted.All(x => x.Id != t.Id)) into.Deleted.Add(t);
            var r = into.Reports.FirstOrDefault(x => x.Id == t.Id);
            if (r is not null && r.ModifiedUtc <= t.DeletedUtc) { into.Reports.Remove(r); s.ReportsDeleted++; }
        }
        foreach (var r in from.Reports)
        {
            if (into.Deleted.Any(t => t.Id == r.Id && t.DeletedUtc >= r.ModifiedUtc)) continue;
            var mine = into.Reports.FirstOrDefault(x => x.Id == r.Id);
            if (mine is null) { into.Reports.Add(r); s.ReportsAdded++; }
            else if (r.ModifiedUtc > mine.ModifiedUtc)
            {
                into.Reports[into.Reports.IndexOf(mine)] = r;
                s.ReportsUpdated++;
            }
        }
        foreach (var r in from.Rosters)
        {
            var mine = into.Rosters.FirstOrDefault(x => x.Id == r.Id);
            if (mine is null) { into.Rosters.Add(r); s.RostersAdded++; }
            else if (r.ModifiedUtc > mine.ModifiedUtc) into.Rosters[into.Rosters.IndexOf(mine)] = r;
        }
        foreach (var t in from.Trains)
        {
            var mine = into.Trains.FirstOrDefault(x => x.TrainNo == t.TrainNo);
            if (mine is null) into.Trains.Add(t);
            else if (t.ModifiedUtc > mine.ModifiedUtc) into.Trains[into.Trains.IndexOf(mine)] = t;
        }
        foreach (var c in from.Charts.Where(c => into.Charts.All(x => x.Id != c.Id && x.Name != c.Name)))
        {
            into.Charts.Add(c);
            s.ChartsAdded++;
        }
        var m = into.Masters;
        void Union(List<string> a, IEnumerable<string> b) { foreach (var x in b) if (!a.Contains(x, StringComparer.OrdinalIgnoreCase)) a.Add(x); }
        Union(m.LocoPilots, from.Masters.LocoPilots);
        Union(m.AssistantLocoPilots, from.Masters.AssistantLocoPilots);
        Union(m.TrainManagers, from.Masters.TrainManagers);
        Union(m.LocoTypes, from.Masters.LocoTypes);
        Union(m.Sheds, from.Masters.Sheds);
        foreach (var st in from.Masters.ExtraStations.Where(x => m.ExtraStations.All(y => !string.Equals(y.Code, x.Code, StringComparison.OrdinalIgnoreCase))))
            m.ExtraStations.Add(st);
        return s;
    }

    /// <summary>Adds imported reports, skipping ones already present (same id, or same date, train and route).</summary>
    public static (int Added, int Skipped) AddReports(AppData data, IEnumerable<InspectionReport> reports)
    {
        int added = 0, skipped = 0;
        foreach (var r in reports)
        {
            bool dup = data.Reports.Any(x => x.Id == r.Id) ||
                       data.Reports.Any(x => x.Date == r.Date && x.TrainNo == r.TrainNo && x.FromStation == r.FromStation && x.ToStation == r.ToStation && r.TrainNo.Length > 0);
            if (dup) { skipped++; continue; }
            r.ModifiedUtc = DateTime.UtcNow;
            data.Reports.Add(r);
            Learn(data, r);
            added++;
        }
        return (added, skipped);
    }

    /// <summary>Remembers crew names, loco types and sheds for the pickers.</summary>
    public static void Learn(AppData data, InspectionReport r)
    {
        var m = data.Masters;
        void Add(List<string> list, string v)
        {
            v = v.Trim();
            if (v.Length > 1 && !list.Contains(v, StringComparer.OrdinalIgnoreCase)) list.Add(v);
        }
        Add(m.LocoPilots, r.LocoPilot);
        Add(m.AssistantLocoPilots, r.AssistantLocoPilot);
        Add(m.TrainManagers, r.TrainManager);
        Add(m.LocoTypes, r.LocoType);
        Add(m.Sheds, r.Shed);
    }

    public static void Delete(AppData data, InspectionReport r)
    {
        data.Reports.Remove(r);
        data.Deleted.Add(new Tombstone { Id = r.Id, DeletedUtc = DateTime.UtcNow });
    }
}
