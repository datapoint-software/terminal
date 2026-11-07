using Avalonia;
using Avalonia.Controls.Primitives;

namespace Datapoint.Terminal.Components;

// A short word in a rounded box: a host's environment, a label, a tag. Its look comes from its classes (danger for
// production, accent, warning, outline), so the same thing reads the same wherever it's shown.
public sealed class Badge : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<Badge, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
