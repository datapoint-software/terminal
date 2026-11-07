using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Themes;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Settings;

// The 16 colours, text, background, cursor and selection of a theme, as hexadecimal, with a terminal showing them as typed.
// A built-in theme is saved as a new one, so the built-in themes stay as they are.
public sealed partial class ThemeEditorWindow : Window
{
    private const string Sample =
        "\e[32m~/app\e[0m $ ls -l\r\n" +
        "\e[34msrc\e[0m  \e[31mdeploy.sh\e[0m  README.md  \e[36mlogs\e[0m\r\n" +
        "\e[32m~/app\e[0m $ git status\r\n" +
        "\e[32mmodified:\e[0m   app.py\r\n\e[31mdeleted:\e[0m    old.py\r\n" +
        "\e[33mwarning:\e[0m disk 82% full   \e[35mnote:\e[0m backup at 02:00\r\n" +
        "\e[90mbright black\e[0m \e[91mred\e[0m \e[92mgreen\e[0m \e[93myellow\e[0m \e[94mblue\e[0m \e[95mmagenta\e[0m \e[96mcyan\e[0m \e[97mwhite\e[0m\r\n" +
        "\e[7m selected text \e[0m\r\n" +
        "\e[32m~/app\e[0m $ ";

    private readonly List<TextBox> _boxes = [];
    private readonly EmulatorTerminal _terminal = new(60, 12);
    private ColorTheme _original = BuiltInThemes.Dark;

    public ThemeEditorWindow()
    {
        InitializeComponent();
        _terminal.Feed(Sample);
        Preview.Terminal = _terminal;
        Preview.FontFamily = (FontFamily?)Avalonia.Application.Current?.FindResource("MonoFont") ?? FontFamily.Default;
        CancelButton.Click += (_, _) => Close(null);
        SaveButton.Click += (_, _) => Save();
    }

    public static Task<ColorTheme?> EditAsync(Window owner, ColorTheme theme)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(theme);
        var window = new ThemeEditorWindow();
        window.Load(theme);
        return window.ShowDialog<ColorTheme?>(owner);
    }

    private static IEnumerable<string> Labels() =>
        [Strings.ThemeColorText, Strings.ThemeColorBackground, Strings.ThemeColorCursor, Strings.ThemeColorSelection, .. Strings.ThemeColorNames.Split(',')];

    private void Load(ColorTheme theme)
    {
        _original = theme;
        NameBox.Text = theme.Source is null && IsBuiltIn(theme) ? Strings.Format(Strings.ThemeEditorCopyName, theme.Name) : theme.Name;
        int?[] values = [theme.Foreground, theme.Background, theme.Cursor, theme.Selection, .. theme.Ansi.Select(color => (int?)color)];
        var labels = Labels().ToList();
        for (var index = 0; index < values.Length; index++)
        {
            Rows.Children.Add(Row(labels[index], values[index]));
        }

        Update();
    }

    private Grid Row(string label, int? value)
    {
        var swatch = new Border { Width = 22, Height = 22, CornerRadius = new Avalonia.CornerRadius(4), BorderThickness = new Avalonia.Thickness(0.5) };
        swatch.Bind(Border.BorderBrushProperty, swatch.GetResourceObservable("SeparatorBrush"));
        var box = new TextBox { Text = value is { } color ? AppearanceViewModel.Hex(color) : string.Empty, FontFamily = Preview.FontFamily, Width = 100 };
        Avalonia.Automation.AutomationProperties.SetName(box, label);
        box.TextChanged += (_, _) =>
        {
            swatch.Background = ThemeImport.Hex(box.Text) is { } parsed ? new SolidColorBrush(Color.FromRgb((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed)) : null;
            Update();
        };
        _boxes.Add(box);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Height = 32 };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(swatch, 1);
        swatch.Margin = new Avalonia.Thickness(0, 0, 8, 0);
        row.Children.Add(swatch);
        Grid.SetColumn(box, 2);
        row.Children.Add(box);
        return row;
    }

    // Null while a colour isn't valid; the selection may be left empty.
    private ColorTheme? Read()
    {
        var colors = _boxes.Select(box => ThemeImport.Hex(box.Text)).ToList();
        if (colors.Where((color, index) => index != 3 && color is null).Any() || (colors[3] is null && !string.IsNullOrWhiteSpace(_boxes[3].Text)))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? _original.Name : NameBox.Text.Trim();
        return new ColorTheme
        {
            Id = IsBuiltIn(_original) ? Guid.NewGuid().ToString("N") : _original.Id,
            Name = name,
            Foreground = colors[0]!.Value,
            Background = colors[1]!.Value,
            Cursor = colors[2]!.Value,
            Selection = colors[3],
            Ansi = [.. colors.Skip(4).Select(color => color!.Value)],
            Source = IsBuiltIn(_original) ? null : _original.Source,
        };
    }

    private void Update()
    {
        if (_boxes.Count < 20 || Read() is not { } theme)
        {
            return;
        }

        lock (_terminal.SyncRoot)
        {
            TerminalThemes.Apply(_terminal.Palette, theme);
        }

        Preview.InvalidateVisual();
    }

    private void Save()
    {
        if (Read() is not { } theme)
        {
            Problem.Text = Strings.ThemeEditorInvalid;
            return;
        }

        Close(theme);
    }

    private static bool IsBuiltIn(ColorTheme theme) => theme == BuiltInThemes.Dark || theme == BuiltInThemes.Light;
}
