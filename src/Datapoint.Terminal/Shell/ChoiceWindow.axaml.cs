using Avalonia.Controls;
using Avalonia.Input;

namespace Datapoint.Terminal.Shell;

public sealed partial class ChoiceWindow : Window
{
    public ChoiceWindow() => InitializeComponent();

    // Choices are listed from the least to the most committing; the last is the primary one. Escape or closing the window
    // answers null.
    public static Task<int?> AskAsync(Window owner, string heading, string message, params string[] choices)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(choices);
        var window = new ChoiceWindow { Title = heading };
        window.Heading.Text = heading;
        window.Message.Text = message;
        for (var index = 0; index < choices.Length; index++)
        {
            var answer = index;
            var button = new Button { Content = choices[index], IsDefault = index == choices.Length - 1 };
            if (button.IsDefault)
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
        window.Opened += (_, _) => window.Buttons.Children[^1].Focus();
        return window.ShowDialog<int?>(owner);
    }
}
