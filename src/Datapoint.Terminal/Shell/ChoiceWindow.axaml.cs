using Avalonia.Controls;
using Avalonia.Input;

namespace Datapoint.Terminal.Shell;

public sealed partial class ChoiceWindow : Window
{
    public ChoiceWindow() => InitializeComponent();

    // Choices are listed from the least to the most committing; the last is the primary one. Escape or closing the window
    // answers null.
    public static Task<int?> AskAsync(Window owner, string heading, string message, params string[] choices) =>
        AskAsync(owner, heading, message, details: null, hasDefault: true, choices);

    // Without a default, Enter answers nothing: used where a choice must be deliberate, such as trusting a host key.
    public static Task<int?> AskAsync(Window owner, string heading, string message, string? details, bool hasDefault, params string[] choices)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(choices);
        var window = new ChoiceWindow { Title = heading };
        window.Heading.Text = heading;
        window.Message.Text = message;
        window.Details.Text = details;
        window.DetailsBox.IsVisible = details is not null;
        for (var index = 0; index < choices.Length; index++)
        {
            var answer = index;
            var isLast = index == choices.Length - 1;
            var button = new Button { Content = choices[index], IsDefault = hasDefault && isLast };
            if (isLast)
            {
                button.Classes.Add("primary");
            }

            button.Click += (_, _) => window.Close(answer);
            window.Buttons.Children.Add(button);
        }

        window.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            window.Close(null);
        };
        window.Opened += (_, _) => window.Buttons.Children[hasDefault ? ^1 : 0].Focus();
        return window.ShowDialog<int?>(owner);
    }
}
