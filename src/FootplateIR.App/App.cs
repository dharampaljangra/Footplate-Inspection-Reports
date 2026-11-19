using FootplateIR.App.Services;

namespace FootplateIR.App;

public class App : Application
{
    public App()
    {
        UserAppTheme = AppTheme.Light;
        Resources.Add(new Style(typeof(Button))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = Button.LineBreakModeProperty, Value = LineBreakMode.WordWrap } }
        });
        DataService.Current.Load();
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new AppShell()) { Title = "Footplate Inspection Reports" };
}
