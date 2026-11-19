using FootplateIR.Core;

namespace FootplateIR.App.Services;

/// <summary>Holds the app data in memory and keeps it saved in the app's private folder.</summary>
public sealed class DataService
{
    public static DataService Current { get; } = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AppData Data { get; private set; } = Store.New();
    public bool IsLoaded { get; private set; }
    public string? LoadError { get; private set; }

    public string DataFile => Path.Combine(FileSystem.AppDataDirectory, "footplate-data.json");

    public void Load()
    {
        if (IsLoaded) return;
        try
        {
            Data = Store.Load(DataFile);
        }
        catch (Exception ex)
        {
            // Never overwrite an unreadable file: keep it aside, then try the previous copy.
            LoadError = ex.Message;
            if (File.Exists(DataFile)) File.Copy(DataFile, DataFile + $".unreadable-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            try { Data = File.Exists(DataFile + ".bak") ? Store.Load(DataFile + ".bak") : Store.New(); }
            catch { Data = Store.New(); }
        }
        IsLoaded = true;
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await Task.Run(() => Store.Save(Data, DataFile));
        }
        finally { _lock.Release(); }
        if (Data.Settings.SyncFolder.Trim().Length > 0) _ = SyncService.TrySyncAsync(quiet: true);
    }

    public async Task SaveReportAsync(InspectionReport edited)
    {
        edited.ModifiedUtc = DateTime.UtcNow;
        var i = Data.Reports.FindIndex(r => r.Id == edited.Id);
        if (i >= 0) Data.Reports[i] = edited;
        else Data.Reports.Add(edited);
        Store.Learn(Data, edited);
        await SaveAsync();
    }

    public void Replace(AppData data)
    {
        Store.Upgrade(data);
        Data = data;
    }
}

/// <summary>Keeps Windows and Android in step through a shared folder (OneDrive / Google Drive / USB) or a backup file.</summary>
public static class SyncService
{
    public const string SyncFileName = "Footplate-Inspection-Reports-sync.json";
    private static int _busy;

    public static async Task<string> TrySyncAsync(bool quiet)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return "Sync already running.";
        try
        {
            var ds = DataService.Current;
            var folder = ds.Data.Settings.SyncFolder.Trim();
            if (folder.Length == 0 || !Directory.Exists(folder)) return "Sync folder not found.";
            var file = Path.Combine(folder, SyncFileName);
            MergeSummary? s = null;
            if (File.Exists(file))
            {
                var other = await Task.Run(() => Store.Load(file));
                s = Store.Merge(ds.Data, other);
            }
            ds.Data.Settings.LastSyncUtc = DateTime.UtcNow;
            await Task.Run(() =>
            {
                Store.Save(ds.Data, ds.DataFile);
                Store.Save(ds.Data, file);
            });
            return s is null ? "Sync file created in the folder." : "Synchronised: " + s;
        }
        catch (Exception ex)
        {
            if (!quiet) throw;
            return ex.Message;
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }
}
