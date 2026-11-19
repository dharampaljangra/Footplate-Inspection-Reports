using System.Globalization;

namespace FootplateIR.App.Services;

/// <summary>Small helpers so every page has the same clean, official look.</summary>
public static class Ui
{
    public static readonly Color Primary = Color.FromArgb("#1F3864");
    public static readonly Color Accent = Color.FromArgb("#C8102E");
    public static readonly Color Soft = Color.FromArgb("#E8EEF7");
    public static readonly Color Ok = Color.FromArgb("#2E7D32");
    public static readonly Color Warn = Color.FromArgb("#9A3412");
    public static readonly Color WarnBack = Color.FromArgb("#FDECEC");
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static Label Title(string text) => new()
    {
        Text = text, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Primary, Margin = new Thickness(0, 4, 0, 8)
    };

    public static Label Heading(string text) => new()
    {
        Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Primary, Margin = new Thickness(0, 6, 0, 2)
    };

    public static Label Text(string text, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = text, FontSize = size, TextColor = color ?? Colors.Black, LineBreakMode = LineBreakMode.WordWrap,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
    };

    public static Label Muted(string text) => Text(text, 12, Colors.DimGray);

    public static View Field(string label, View input) => new VerticalStackLayout
    {
        Spacing = 2,
        Children = { new Label { Text = label, FontSize = 12, TextColor = Colors.DimGray }, input }
    };

    public static Entry Entry(string text = "", string placeholder = "", Keyboard? keyboard = null) => new()
    {
        Text = text, Placeholder = placeholder, Keyboard = keyboard ?? Keyboard.Default, ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };

    public static Editor Editor(string text = "", string placeholder = "", double height = 90) => new()
    {
        Text = text, Placeholder = placeholder, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = height,
        Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeSentence | KeyboardFlags.Spellcheck)
    };

    public static Picker Picker(IEnumerable<string> items, string selected = "")
    {
        var p = new Picker();
        foreach (var i in items) p.Items.Add(i);
        if (selected.Length > 0 && !p.Items.Contains(selected)) p.Items.Add(selected);
        p.SelectedItem = selected.Length > 0 ? selected : p.Items.FirstOrDefault();
        return p;
    }

    public static string Value(Picker p) => p.SelectedItem as string ?? "";

    /// <summary>An entry with a "choose" button offering earlier values (crew names, loco types ...).</summary>
    public static View Suggest(Entry entry, Func<IEnumerable<string>> items, string title)
    {
        var b = new Button { Text = "▾", WidthRequest = 44, BackgroundColor = Soft, TextColor = Primary, Padding = 0 };
        b.Clicked += async (_, _) =>
        {
            var list = items().Where(s => s.Trim().Length > 0).Distinct().OrderBy(s => s).Take(60).ToArray();
            if (list.Length == 0) { await Alert(title, "Nothing saved yet - names and values are remembered as you save reports."); return; }
            var page = CurrentPage;
            if (page is null) return;
            var pick = await page.DisplayActionSheet(title, "Cancel", null, list);
            if (pick is not null && pick != "Cancel") entry.Text = pick;
        };
        return Row(entry, b);
    }

    public static Button Button(string text, Func<Task> action, bool primary = true)
    {
        var b = new Button
        {
            Text = text,
            BackgroundColor = primary ? Primary : Soft,
            TextColor = primary ? Colors.White : Primary,
            CornerRadius = 8,
            LineBreakMode = LineBreakMode.WordWrap,
            Padding = new Thickness(14, 8),
            Margin = new Thickness(0, 2)
        };
        b.Clicked += async (_, _) =>
        {
            b.IsEnabled = false;
            try { await action(); }
            catch (Exception ex) { await Alert("Error", ex.Message); }
            finally { b.IsEnabled = true; }
        };
        return b;
    }

    public static Button Small(string text, Func<Task> action, Color? back = null, Color? fore = null)
    {
        var b = new Button
        {
            Text = text, FontSize = 13, Padding = new Thickness(10, 4), CornerRadius = 6, MinimumHeightRequest = 34,
            BackgroundColor = back ?? Soft, TextColor = fore ?? Primary
        };
        b.Clicked += async (_, _) =>
        {
            try { await action(); }
            catch (Exception ex) { await Alert("Error", ex.Message); }
        };
        return b;
    }

    public static Border Card(View content, Color? bg = null) => new()
    {
        Content = content,
        Padding = new Thickness(12),
        Margin = new Thickness(0, 4),
        BackgroundColor = bg ?? Colors.White,
        Stroke = Color.FromArgb("#D0D7E5"),
        StrokeThickness = 1,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }
    };

    public static VerticalStackLayout Stack(params View[] children)
    {
        var s = new VerticalStackLayout { Spacing = 6 };
        foreach (var c in children) s.Children.Add(c);
        return s;
    }

    public static ScrollView Page(params View[] children)
    {
        var stack = new VerticalStackLayout { Padding = IsPhone ? new Thickness(10, 10, 10, 40) : new Thickness(16), Spacing = 6, MaximumWidthRequest = 1000 };
        foreach (var c in children) stack.Children.Add(c);
        return new ScrollView { Content = stack, BackgroundColor = Color.FromArgb("#F6F8FC") };
    }

    public static bool IsPhone => DeviceInfo.Current.Idiom == DeviceIdiom.Phone;

    /// <summary>Side-by-side views; inputs share the width, buttons / check boxes take what they need.</summary>
    public static Grid Row(params View[] children)
    {
        var g = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < children.Length; i++)
        {
            var c = children[i];
            bool fixedWidth = c is Microsoft.Maui.Controls.Button or CheckBox or Switch;
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = fixedWidth ? GridLength.Auto : GridLength.Star });
            if (c is Label) c.VerticalOptions = LayoutOptions.Center;
            g.Add(c, i, 0);
        }
        return g;
    }

    /// <summary>Two (or more) fields side by side on Windows, one below the other on phones.</summary>
    public static Layout Cols(params View[] children)
    {
        if (IsPhone) return Stack(children);
        var g = new Grid { ColumnSpacing = 10 };
        for (int i = 0; i < children.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            g.Add(children[i], i, 0);
        }
        return g;
    }

    /// <summary>A group of buttons: full width on phones, wrapping side by side on Windows.</summary>
    public static Layout Wrap(params View[] children)
    {
        if (IsPhone)
        {
            var v = new VerticalStackLayout { Spacing = 6, Margin = new Thickness(0, 2) };
            foreach (var c in children) { c.HorizontalOptions = LayoutOptions.Fill; v.Children.Add(c); }
            return v;
        }
        var f = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        foreach (var c in children) { c.Margin = new Thickness(0, 2, 8, 2); f.Children.Add(c); }
        return f;
    }

    /// <summary>Check boxes with labels that wrap across the width.</summary>
    public static FlexLayout Flow() => new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };

    public static View Check(CheckBox box, string text)
    {
        var l = new Label { Text = text, VerticalOptions = LayoutOptions.Center, FontSize = 14, Margin = new Thickness(0, 0, 10, 0) };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => box.IsChecked = !box.IsChecked;
        l.GestureRecognizers.Add(tap);
        return new HorizontalStackLayout { Spacing = 0, Children = { box, l } };
    }

    public static Page? CurrentPage
    {
        get
        {
            var root = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
            if (root is null) return null;
            var nav = (root as Shell)?.CurrentPage ?? root;
            return nav.Navigation.ModalStack.LastOrDefault() ?? nav;
        }
    }

    public static async Task Alert(string title, string message)
    {
        var p = CurrentPage;
        if (p is not null) await p.DisplayAlert(title, message, "OK");
    }

    public static async Task<bool> Confirm(string title, string message, string yes = "Yes", string no = "No")
    {
        var p = CurrentPage;
        return p is not null && await p.DisplayAlert(title, message, yes, no);
    }

    public static async Task<string?> Choose(string title, params string[] options)
    {
        var p = CurrentPage;
        if (p is null) return null;
        var r = await p.DisplayActionSheet(title, "Cancel", null, options);
        return r is null || r == "Cancel" ? null : r;
    }

    public static async Task<string?> Prompt(string title, string message, string initial = "", Keyboard? keyboard = null, int maxLength = 200)
    {
        var p = CurrentPage;
        if (p is null) return null;
        return await p.DisplayPromptAsync(title, message, "OK", "Cancel", initialValue: initial, maxLength: maxLength, keyboard: keyboard ?? Keyboard.Default);
    }

    public static DateTime DateOf(DatePicker p) => p.Date is DateTime d ? d : DateTime.Today;

    public static List<string> Lines(string? text) =>
        (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
}

/// <summary>Month and year chooser with previous / next buttons, plus "whole year" and "all".</summary>
public sealed class MonthSelector : HorizontalStackLayout
{
    private readonly Picker _month = new() { WidthRequest = 130 };
    private readonly Picker _year = new() { WidthRequest = 90 };
    public event EventHandler? Changed;
    private const int FirstYear = 2015;

    public MonthSelector(int year, int month)
    {
        Spacing = 6;
        _month.Items.Add("All months");
        foreach (var m in Ui.Inv.DateTimeFormat.MonthNames.Take(12)) _month.Items.Add(m);
        for (int y = FirstYear; y <= DateTime.Today.Year + 1; y++) _year.Items.Add(y.ToString(Ui.Inv));
        Set(year, month);
        _month.SelectedIndexChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _year.SelectedIndexChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        var prev = new Button { Text = "<", WidthRequest = 44, BackgroundColor = Ui.Soft, TextColor = Ui.Primary };
        var next = new Button { Text = ">", WidthRequest = 44, BackgroundColor = Ui.Soft, TextColor = Ui.Primary };
        prev.Clicked += (_, _) => Shift(-1);
        next.Clicked += (_, _) => Shift(1);
        Children.Add(prev);
        Children.Add(_month);
        Children.Add(_year);
        Children.Add(next);
    }

    public int Year => _year.SelectedIndex >= 0 ? FirstYear + _year.SelectedIndex : DateTime.Today.Year;
    /// <summary>0 = whole year.</summary>
    public int Month => Math.Max(0, _month.SelectedIndex);

    public string Period => Month == 0 ? Year.ToString(Ui.Inv) : new DateTime(Year, Month, 1).ToString("MMMM yyyy", Ui.Inv);

    public bool Includes(DateOnly d) => d.Year == Year && (Month == 0 || d.Month == Month);

    public void Set(int year, int month)
    {
        _year.SelectedIndex = Math.Clamp(year - FirstYear, 0, _year.Items.Count - 1);
        _month.SelectedIndex = Math.Clamp(month, 0, 12);
    }

    private void Shift(int by)
    {
        if (Month == 0) { Set(Year + by, 0); return; }
        var d = new DateTime(Year, Month, 1).AddMonths(by);
        if (d.Year < FirstYear || d.Year > DateTime.Today.Year + 1) return;
        Set(d.Year, d.Month);
    }
}
