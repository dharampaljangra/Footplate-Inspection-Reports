using FootplateIR.Core;

namespace FootplateIR.App.Services;

public static class FileService
{
    /// <summary>Folder where exported reports are written (Documents\Footplate Inspection Reports on Windows).</summary>
    public static string ExportFolder
    {
        get
        {
#if WINDOWS
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Footplate Inspection Reports");
#else
            var dir = Path.Combine(FileSystem.AppDataDirectory, "Exports");
#endif
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static async Task<string> WriteAsync(string fileName, byte[] bytes)
    {
        var path = Path.Combine(ExportFolder, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    /// <summary>Opens the file on Windows; offers the share sheet on Android (WhatsApp, e-mail, Drive, Files, print).</summary>
    public static async Task DeliverAsync(string path, string title)
    {
#if WINDOWS
        await Launcher.Default.OpenAsync(new OpenFileRequest(title, new ReadOnlyFile(path)));
#else
        await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });
#endif
    }

    public static async Task ShareFileAsync(string path, string title) =>
        await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });

    public static async Task ShareTextAsync(string text, string title) =>
        await Share.Default.RequestAsync(new ShareTextRequest { Text = text, Title = title });

    public static async Task<FileResult?> PickAsync(string title, string[] windowsExt, string[] androidMime)
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, windowsExt },
            { DevicePlatform.Android, androidMime }
        });
        return await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = title, FileTypes = types });
    }

    /// <summary>Copies a picked file to the cache so every reader can open it by path.</summary>
    public static async Task<string> ToCacheAsync(FileResult file)
    {
        var target = Path.Combine(FileSystem.CacheDirectory, "import-" + DateTime.Now.ToString("HHmmssfff") + Path.GetExtension(file.FileName).ToLowerInvariant());
        await using var src = await file.OpenReadAsync();
        await using var dst = File.Create(target);
        await src.CopyToAsync(dst);
        return target;
    }
}

/// <summary>Asks for a format, writes the file and opens / shares it.</summary>
public static class ExportService
{
    public static readonly (string Label, ExportFormat Format)[] All =
    {
        ("PDF (official note)", ExportFormat.Pdf), ("Word (.docx)", ExportFormat.Word), ("Excel (.xlsx)", ExportFormat.Excel),
        ("Web page / print (.html)", ExportFormat.Html), ("WhatsApp text (.txt)", ExportFormat.Text), ("CSV (.csv)", ExportFormat.Csv)
    };

    public static async Task<ExportFormat?> AskFormat(string title, params ExportFormat[] allowed)
    {
        var opts = All.Where(o => allowed.Length == 0 || allowed.Contains(o.Format)).ToList();
        var pick = await Ui.Choose(title, opts.Select(o => o.Label).ToArray());
        return pick is null ? null : opts.First(o => o.Label == pick).Format;
    }

    public static async Task ExportAsync(string title, string baseName, Func<ExportFormat, byte[]> make, params ExportFormat[] allowed)
    {
        var f = await AskFormat(title, allowed);
        if (f is null) return;
        var bytes = await Task.Run(() => make(f.Value));
        var path = await FileService.WriteAsync(Export.SafeFile(baseName) + Export.Extension(f.Value), bytes);
        await FileService.DeliverAsync(path, title);
#if WINDOWS
        await Ui.Alert("Saved", "Saved as\n" + path);
#endif
    }
}

/// <summary>Calls and WhatsApp messages to staff on duty.</summary>
public static class Dialer
{
    public static async Task CallAsync(string mobile)
    {
        var n = new string(mobile.Where(char.IsDigit).ToArray());
        if (n.Length == 0) return;
        try
        {
            if (PhoneDialer.Default.IsSupported) { PhoneDialer.Default.Open(n); return; }
        }
        catch (FeatureNotSupportedException) { }
        // Windows: Phone Link (or any app registered for tel:) places the call through the paired mobile.
        if (!await Launcher.Default.TryOpenAsync(new Uri("tel:" + n)))
            await Ui.Alert("Call", $"Calling is not available on this computer. Number: {n}");
    }

    public static async Task WhatsAppAsync(string mobile, string text = "")
    {
        var n = new string(mobile.Where(char.IsDigit).ToArray());
        if (n.Length == 10) n = "91" + n;
        await Launcher.Default.OpenAsync(new Uri($"https://wa.me/{n}" + (text.Length > 0 ? "?text=" + Uri.EscapeDataString(text) : "")));
    }
}
