using Avalonia.Controls;
using Avalonia.Input;

namespace Datapoint.Terminal.Shell;

public sealed partial class PromptWindow : Window
{
    private readonly List<TextBox> _boxes = [];

    public PromptWindow() => InitializeComponent();

    // Text from the server is shown apart, labelled as the server's, so it can't pass itself off as the application's.
    // Masked fields are never shown, logged or sent anywhere but the server.
    public static Task<IReadOnlyList<string>?> AskAsync(Window owner, string heading, string message, string? serverText, IReadOnlyList<(string Label, bool IsSecret)> fields, IReadOnlyList<string>? values = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = Create(heading, message, serverText, fields, values);
        window.OkButton.Click += (_, _) => window.Close(window.Answers());
        return window.ShowDialog<IReadOnlyList<string>?>(owner);
    }

    // A password, with a box to keep it in the credential store when there is one (product decision 113).
    public static Task<PasswordAnswer?> AskPasswordAsync(Window owner, string heading, string message, string label, string? rememberLabel, bool remember)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = Create(heading, message, serverText: null, [(label, true)], values: null);
        window.RememberBox.IsVisible = rememberLabel is not null;
        window.RememberBox.Content = rememberLabel;
        window.RememberBox.IsChecked = remember && rememberLabel is not null;
        window.OkButton.Click += (_, _) => window.Close(new PasswordAnswer(window.Answers()[0], window.RememberBox.IsChecked == true));
        return window.ShowDialog<PasswordAnswer?>(owner);
    }

    private static PromptWindow Create(string heading, string message, string? serverText, IReadOnlyList<(string Label, bool IsSecret)> fields, IReadOnlyList<string>? values)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var window = new PromptWindow { Title = heading };
        window.Heading.Text = heading;
        window.Message.Text = message;
        window.ServerText.Text = serverText;
        window.ServerBox.IsVisible = !string.IsNullOrWhiteSpace(serverText);
        foreach (var (label, isSecret) in fields)
        {
            var box = new TextBox { PasswordChar = isSecret ? '•' : default, RevealPassword = false, Text = values?.ElementAtOrDefault(window._boxes.Count) };
            Avalonia.Automation.AutomationProperties.SetName(box, label);
            window.Fields.Children.Add(new TextBlock { Text = label, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            window.Fields.Children.Add(box);
            window._boxes.Add(box);
        }

        window.CancelButton.Click += (_, _) => window.Close(null);
        window.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            window.Close(null);
        };
        window.Opened += (_, _) => (window._boxes.Count > 0 ? window._boxes[0] : (Control)window.OkButton).Focus();
        window.Closed += (_, _) => window._boxes.ForEach(box => box.Text = string.Empty);
        return window;
    }

    private IReadOnlyList<string> Answers() => [.. _boxes.Select(box => box.Text ?? string.Empty)];
}
