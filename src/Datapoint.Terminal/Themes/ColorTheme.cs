namespace Datapoint.Terminal.Themes;

// A terminal colour scheme: the 16 ANSI colours, text, background, cursor and, optionally, the selection. Colours are 0xRRGGBB.
public sealed record ColorTheme
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<int> Ansi { get; init => field = value is { Count: 16 } ? value : throw new ArgumentException("A theme has 16 ANSI colours.", nameof(value)); } = new int[16];

    public int Foreground { get; init; }

    public int Background { get; init; }

    public int Cursor { get; init; }

    public int? Selection { get; init; }

    // Where an imported theme came from, e.g. "iTerm2"; null for the built-in themes and ones made here.
    public string? Source { get; init; }

    // Relative luminance below the middle grey: used to pick light or dark window colours to go with it.
    public bool IsDark
    {
        get
        {
            static double Channel(int value)
            {
                var channel = value / 255.0;
                return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            }

            var luminance = (0.2126 * Channel((Background >> 16) & 0xFF)) + (0.7152 * Channel((Background >> 8) & 0xFF)) + (0.0722 * Channel(Background & 0xFF));
            return luminance < 0.18;
        }
    }
}
