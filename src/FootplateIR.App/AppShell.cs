using FootplateIR.App.Pages;
using FootplateIR.App.Services;

namespace FootplateIR.App;

public class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = DeviceInfo.Idiom == DeviceIdiom.Phone ? FlyoutBehavior.Flyout : FlyoutBehavior.Locked;
        Shell.SetBackgroundColor(this, Ui.Primary);
        Shell.SetForegroundColor(this, Colors.White);
        Shell.SetTitleColor(this, Colors.White);
        FlyoutHeader = new VerticalStackLayout
        {
            Padding = new Thickness(16, 20),
            BackgroundColor = Ui.Primary,
            Children =
            {
                new Label { Text = "Footplate Inspection Reports", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = "Night / day footplate notes, field staff, patrolling chart", FontSize = 12, TextColor = Colors.White }
            }
        };
        Add("Reports", typeof(HomePage));
        Add("Import (text, photo, Word, PDF, Excel)", typeof(ImportPage));
        Add("Patrolmen & keymen on duty - call", typeof(DutyPage));
        Add("Locations to be attended", typeof(LocationsPage));
        Add("Patrolling chart & section KM", typeof(ChartPage));
        Add("Profile, lists & settings", typeof(ProfilePage));
        Add("Backup & sync (Windows - Android)", typeof(BackupPage));
        Add("About", typeof(AboutPage));
    }

    private void Add(string title, Type page) =>
        Items.Add(new FlyoutItem
        {
            Title = title,
            Route = page.Name,
            Items = { new ShellContent { Title = title, ContentTemplate = new DataTemplate(page) } }
        });

    public static async Task GoAsync<T>() where T : Page => await Current.GoToAsync("//" + typeof(T).Name);
}
