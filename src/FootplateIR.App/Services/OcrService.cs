using System.Text;

namespace FootplateIR.App.Services;

/// <summary>
/// Reads text from a photo of a report / duty list. Works offline:
/// Windows uses the OCR built into Windows 10/11; Android uses Google ML Kit (on-device model).
/// </summary>
public static class OcrService
{
    public static async Task<string> RecognizeAsync(string imagePath)
    {
#if WINDOWS
        return await RecognizeWindowsAsync(imagePath);
#elif ANDROID
        return await RecognizeAndroidAsync(imagePath);
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Reading text from photos is not available on this device.");
#endif
    }

    /// <summary>Lets the user take a photo (phones) or choose an image, and returns its path.</summary>
    public static async Task<string?> GetPhotoAsync(bool camera)
    {
        FileResult? photo;
        if (camera)
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Ui.Alert("Camera", "No camera available here - choose a saved photo / screenshot instead.");
                return null;
            }
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted) return null;
#pragma warning disable CS0618
            photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = "Photograph the report" });
#pragma warning restore CS0618
        }
        else
        {
#pragma warning disable CS0618
            photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Choose a photo / screenshot" });
#pragma warning restore CS0618
        }
        return photo is null ? null : await FileService.ToCacheAsync(photo);
    }

#if WINDOWS
    private static async Task<string> RecognizeWindowsAsync(string path)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        var max = Windows.Media.Ocr.OcrEngine.MaxImageDimension;
        Windows.Graphics.Imaging.SoftwareBitmap bmp;
        if (decoder.PixelWidth > max || decoder.PixelHeight > max)
        {
            var scale = (double)max / Math.Max(decoder.PixelWidth, decoder.PixelHeight);
            var transform = new Windows.Graphics.Imaging.BitmapTransform
            {
                ScaledWidth = (uint)(decoder.PixelWidth * scale),
                ScaledHeight = (uint)(decoder.PixelHeight * scale)
            };
            bmp = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                transform, Windows.Graphics.Imaging.ExifOrientationMode.RespectExifOrientation, Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);
        }
        else
            bmp = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

        var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
                     ?? Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw new InvalidOperationException("Windows text recognition (OCR) for English is not installed. Add it in Settings > Time & language > Language > English > Optional features.");
        var result = await engine.RecognizeAsync(bmp);
        var sb = new StringBuilder();
        foreach (var line in result.Lines) sb.AppendLine(line.Text);
        return sb.ToString();
    }
#endif

#if ANDROID
    private static async Task<string> RecognizeAndroidAsync(string path)
    {
        var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeFile(path, bounds);
        int sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > 3200) sample *= 2;
        var bitmap = Android.Graphics.BitmapFactory.DecodeFile(path, new Android.Graphics.BitmapFactory.Options { InSampleSize = sample })
                     ?? throw new InvalidOperationException("The photo could not be opened.");
        int rotation = 0;
        try
        {
#pragma warning disable CA1422
            var exif = new Android.Media.ExifInterface(path);
            rotation = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1) switch { 6 => 90, 3 => 180, 8 => 270, _ => 0 };
#pragma warning restore CA1422
        }
        catch { }

        var image = Xamarin.Google.MLKit.Vision.Common.InputImage.FromBitmap(bitmap, rotation);
        var recognizer = Xamarin.Google.MLKit.Vision.Text.TextRecognition.GetClient(
            new Xamarin.Google.MLKit.Vision.Text.Latin.TextRecognizerOptions.Builder().Build());
        var tcs = new TaskCompletionSource<Java.Lang.Object?>();
        var listener = new TaskListener(tcs);
        recognizer.Process(image).AddOnSuccessListener(listener).AddOnFailureListener(listener);
        var result = await tcs.Task;
        if (result is not Xamarin.Google.MLKit.Vision.Text.Text text) return "";
        var sb = new StringBuilder();
        foreach (var block in text.TextBlocks)
        {
            foreach (var line in block.Lines) sb.AppendLine(line.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private sealed class TaskListener : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener, Android.Gms.Tasks.IOnFailureListener
    {
        private readonly TaskCompletionSource<Java.Lang.Object?> _tcs;
        public TaskListener(TaskCompletionSource<Java.Lang.Object?> tcs) => _tcs = tcs;
        public void OnSuccess(Java.Lang.Object? result) => _tcs.TrySetResult(result);
        public void OnFailure(Java.Lang.Exception e) => _tcs.TrySetException(new InvalidOperationException("Text recognition failed: " + e.Message));
    }
#endif
}
