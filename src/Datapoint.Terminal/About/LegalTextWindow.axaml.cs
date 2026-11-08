using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace Datapoint.Terminal.About;

// The license or the third-party notices, as plain text the user can select and copy.
public sealed partial class LegalTextWindow : Window
{
    public LegalTextWindow()
        : this(string.Empty, string.Empty)
    {
    }

    public LegalTextWindow(string title, string text)
    {
        InitializeComponent();
        Title = title;
        Body.Text = text;
        AutomationProperties.SetName(Body, title);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        Close();
        e.Handled = true;
    }
}
