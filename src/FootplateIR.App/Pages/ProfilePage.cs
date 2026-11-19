using System.Globalization;
using FootplateIR.App.Services;
using FootplateIR.Core;

namespace FootplateIR.App.Pages;

/// <summary>Inspecting official, heading of the note, standard observations and other lists, settings.</summary>
public class ProfilePage : ContentPage
{
    private readonly Entry _name = Ui.Entry();
    private readonly Entry _desig = Ui.Entry();
    private readonly Entry _mobile = Ui.Entry("", "", Keyboard.Telephone);
    private readonly Entry _section = Ui.Entry();
    private readonly Entry _hq = Ui.Entry();
    private readonly Entry _railway = Ui.Entry();
    private readonly Entry _division = Ui.Entry();
    private readonly Entry _office = Ui.Entry();
    private readonly Entry _salutation = Ui.Entry();
    private readonly Editor _addressee = Ui.Editor("", "To, (optional) e.g. Sr. DEN/Co-ord/KOTA", 40);
    private readonly Editor _copyTo = Ui.Editor("", "Copy to - one per line (optional)", 50);
    private readonly CheckBox _shri = new() { Color = Ui.Primary };
    private readonly CheckBox _printChart = new() { Color = Ui.Primary };
    private readonly Entry _tolerance = Ui.Entry("", "", Keyboard.Numeric);
    private readonly Editor _std = Ui.Editor("", "", 120);
    private readonly Editor _kinds = Ui.Editor("", "", 60);
    private readonly Editor _purposes = Ui.Editor("", "", 60);
    private readonly Editor _cats = Ui.Editor("", "", 80);
    private readonly Editor _vis = Ui.Editor("", "", 60);
    private readonly Editor _names = Ui.Editor("", "", 80);

    public ProfilePage()
    {
        Title = "Profile & settings";
        Content = Ui.Page(
            Ui.Title("Profile, lists & settings"),
            Ui.Card(Ui.Stack(
                Ui.Heading("Inspecting official"),
                Ui.Cols(Ui.Field("Name", _name), Ui.Field("Designation", _desig)),
                Ui.Cols(Ui.Field("Mobile", _mobile), Ui.Field("Section", _section)),
                Ui.Field("Headquarters (station code)", _hq))),
            Ui.Card(Ui.Stack(
                Ui.Heading("Heading of the inspection note"),
                Ui.Cols(Ui.Field("Railway", _railway), Ui.Field("Division", _division)),
                Ui.Field("Office line", _office),
                Ui.Field("Salutation (WhatsApp text)", _salutation),
                Ui.Field("Addressed to", _addressee),
                Ui.Field("Copy to", _copyTo),
                Ui.Check(_shri, "Write \"Shri\" before the crew's names"))),
            Ui.Card(Ui.Stack(
                Ui.Heading("Patrolling chart check"),
                Ui.Check(_printChart, "Print the patrolling chart check in the inspection note (when a chart is in force)"),
                Ui.Field("Patrol pair counts as 'as per chart' within (km)", _tolerance))),
            Ui.Card(Ui.Stack(
                Ui.Heading("Lists used in the report form (one per line)"),
                Ui.Field("Standard observations (ticked by default in a new report)", _std),
                Ui.Field("Kinds of inspection", _kinds),
                Ui.Field("Purposes", _purposes),
                Ui.Field("Field staff categories", _cats),
                Ui.Field("Visibility", _vis),
                Ui.Field("Crew names remembered (LP / ALP / TM) - delete wrong ones", _names))),
            Ui.Button("Save", Save));
        Fill();
    }

    private void Fill()
    {
        var d = DataService.Current.Data;
        var p = d.Profile;
        _name.Text = p.Name; _desig.Text = p.Designation; _mobile.Text = p.Mobile; _section.Text = p.Section; _hq.Text = p.HqStation;
        _railway.Text = p.Railway; _division.Text = p.Division; _office.Text = p.Office; _salutation.Text = p.Salutation;
        _addressee.Text = p.Addressee; _copyTo.Text = string.Join("\n", p.CopyTo); _shri.IsChecked = p.UseShri;
        _printChart.IsChecked = d.Settings.PrintChartCheck;
        _tolerance.Text = d.Settings.ChartToleranceKm.ToString("0.##", CultureInfo.InvariantCulture);
        var m = d.Masters;
        _std.Text = string.Join("\n", m.StandardObservations);
        _kinds.Text = string.Join("\n", m.Kinds);
        _purposes.Text = string.Join("\n", m.Purposes);
        _cats.Text = string.Join("\n", m.SightingCategories);
        _vis.Text = string.Join("\n", m.Visibility);
        _names.Text = string.Join("\n", m.LocoPilots.Concat(m.AssistantLocoPilots).Concat(m.TrainManagers).Distinct().OrderBy(x => x));
    }

    private async Task Save()
    {
        var d = DataService.Current.Data;
        var p = d.Profile;
        p.Name = (_name.Text ?? "").Trim(); p.Designation = (_desig.Text ?? "").Trim(); p.Mobile = (_mobile.Text ?? "").Trim();
        p.Section = (_section.Text ?? "").Trim(); p.HqStation = (_hq.Text ?? "").Trim().ToUpperInvariant();
        p.Railway = (_railway.Text ?? "").Trim(); p.Division = (_division.Text ?? "").Trim(); p.Office = (_office.Text ?? "").Trim();
        p.Salutation = (_salutation.Text ?? "").Trim(); p.Addressee = (_addressee.Text ?? "").Trim(); p.CopyTo = Ui.Lines(_copyTo.Text);
        p.UseShri = _shri.IsChecked;
        d.Settings.PrintChartCheck = _printChart.IsChecked;
        if (double.TryParse(_tolerance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var tol) && tol > 0) d.Settings.ChartToleranceKm = tol;
        var m = d.Masters;
        // Keep the ticks of existing reports: a changed standard text simply shows as an "other observation" there.
        m.StandardObservations = Ui.Lines(_std.Text);
        m.Kinds = Ui.Lines(_kinds.Text);
        m.Purposes = Ui.Lines(_purposes.Text);
        m.SightingCategories = Ui.Lines(_cats.Text);
        m.Visibility = Ui.Lines(_vis.Text);
        var keep = Ui.Lines(_names.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        m.LocoPilots.RemoveAll(x => !keep.Contains(x));
        m.AssistantLocoPilots.RemoveAll(x => !keep.Contains(x));
        m.TrainManagers.RemoveAll(x => !keep.Contains(x));
        await DataService.Current.SaveAsync();
        await Ui.Alert("Saved", "Profile and lists saved.");
    }
}

/// <summary>Backup, restore and keeping Windows and Android in step.</summary>
public class BackupPage : ContentPage
{
    private readonly Entry _folder = Ui.Entry("", @"e.g. C:\Users\...\OneDrive\Footplate");
    private readonly Label _status = Ui.Muted("");

    public BackupPage()
    {
        Title = "Backup & sync";
        var d = DataService.Current.Data;
        _folder.Text = d.Settings.SyncFolder;
        _status.Text = d.Settings.LastSyncUtc is DateTime t ? $"Last synchronised {t.ToLocalTime():dd.MM.yyyy HH:mm}." : "";
        Content = Ui.Page(
            Ui.Title("Backup & sync"),
            Ui.Card(Ui.Stack(
                Ui.Heading("Backup file"),
                Ui.Text("Save everything (reports, duty lists, charts, lists) in one file - keep it on Drive / e-mail it to yourself. Open the same file on the other device with 'Import > Choose file' (or below) to bring both in step: reports are merged, newer edits win.", 13),
                Ui.Wrap(Ui.Button("Save / share backup file", Backup), Ui.Button("Merge a backup file", Merge, false)))),
            Ui.Card(Ui.Stack(
                Ui.Heading("Sync folder (Windows)"),
                Ui.Text("Choose a folder synchronised by OneDrive / Google Drive. Every save on this computer is merged with the sync file there automatically; on Android, share the backup file to the same Drive folder, or merge it here.", 13),
                Ui.Field("Folder", _folder),
                Ui.Wrap(Ui.Button("Save folder & sync now", SyncNow, false)),
                _status)));
    }

    private async Task Backup()
    {
        var json = Store.ToJson(DataService.Current.Data);
        var path = await FileService.WriteAsync($"Footplate_Reports_backup_{DateTime.Now:yyyy-MM-dd_HHmm}.json", System.Text.Encoding.UTF8.GetBytes(json));
        await FileService.ShareFileAsync(path, "Footplate reports backup");
    }

    private async Task Merge()
    {
        var f = await FileService.PickAsync("Choose a backup file", new[] { ".json", ".fir" }, new[] { "application/json", "application/octet-stream", "text/plain" });
        if (f is null) return;
        var path = await FileService.ToCacheAsync(f);
        var other = Store.Load(path);
        var s = Store.Merge(DataService.Current.Data, other);
        await DataService.Current.SaveAsync();
        await Ui.Alert("Merged", s.ToString());
    }

    private async Task SyncNow()
    {
        var folder = (_folder.Text ?? "").Trim();
        if (folder.Length > 0 && !Directory.Exists(folder)) { await Ui.Alert("Folder", "This folder does not exist."); return; }
        DataService.Current.Data.Settings.SyncFolder = folder;
        await DataService.Current.SaveAsync();
        if (folder.Length == 0) { _status.Text = "Sync folder removed."; return; }
        _status.Text = await SyncService.TrySyncAsync(quiet: false);
    }
}

public class AboutPage : ContentPage
{
    public AboutPage()
    {
        Title = "About";
        var v = AppInfo.Current.VersionString;
        Content = Ui.Page(
            Ui.Title("Footplate Inspection Reports"),
            Ui.Card(Ui.Stack(
                Ui.Text($"Version {v}", 13),
                Ui.Text(Defaults.AuthorCredit, 14, Ui.Primary, true),
                Ui.Text("Night / day footplate inspection notes in the official layout - particulars, crew on board, field staff seen en-route, observations, shortcomings, locations to be attended for better running, remarks - exported to PDF, Word, Excel, HTML and WhatsApp text.", 13),
                Ui.Text("Reports can be brought in from WhatsApp text, photos (read on the device), Word, PDF and Excel files. Night duty lists of patrolmen / keymen can be imported and the staff called from the app; the patrolling chart check shows where each patrol pair should have been met.", 13),
                Ui.Muted("Works offline. Data stays on this device unless you share a file or use the sync folder."))));
    }
}
