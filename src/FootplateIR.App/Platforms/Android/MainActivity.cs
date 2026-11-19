using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using AInsets = AndroidX.Core.Graphics.Insets;

namespace FootplateIR.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Screenshots are allowed (reports are often shared as screenshots).

        // Android 15+ draws apps edge-to-edge: without this the last entries and buttons of a page
        // sit under the navigation bar / gesture area (and under the keyboard while typing).
        var content = FindViewById(Android.Resource.Id.Content);
        if (content is not null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(content, new KeepClearOfSystemBars());
            ViewCompat.RequestApplyInsets(content);
        }
    }

    private sealed class KeepClearOfSystemBars : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View v, WindowInsetsCompat insets)
        {
            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
            var ime = insets.GetInsets(WindowInsetsCompat.Type.Ime());
            v.SetPadding(bars.Left, 0, bars.Right, Math.Max(bars.Bottom, ime.Bottom));
            // The top (status bar) is still handled by the app bar; bottom / sides are now taken care of here.
            var top = insets.GetInsets(WindowInsetsCompat.Type.SystemBars()).Top;
            var cutTop = insets.GetInsets(WindowInsetsCompat.Type.DisplayCutout()).Top;
            return new WindowInsetsCompat.Builder(insets)
                .SetInsets(WindowInsetsCompat.Type.SystemBars(), AInsets.Of(0, top, 0, 0))!
                .SetInsets(WindowInsetsCompat.Type.DisplayCutout(), AInsets.Of(0, cutTop, 0, 0))!
                .SetInsets(WindowInsetsCompat.Type.Ime(), AInsets.None)!
                .Build();
        }
    }
}
