using Avalonia;
using Avalonia.Controls.Primitives;

namespace Datapoint.Terminal.Components;

// A workspace's initials on the accent colour, in the sidebar and in the workspace switcher.
public sealed class Avatar : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Avatar, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
