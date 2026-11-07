using System.Globalization;
using Avalonia.Media;

namespace Datapoint.Terminal.Shell;

public static class MonospaceFonts
{
    // Installed families whose narrow and wide letters are equally wide, the test a terminal grid needs. A family the system
    // substitutes another font for fails it unless the substitute is monospace too, which is harmless.
    public static IReadOnlyList<string> Installed() => [.. FontManager.Current.SystemFonts
        .Select(family => family.Name)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(IsMonospace)
        .Order(StringComparer.OrdinalIgnoreCase)];

    public static bool IsMonospace(string family)
    {
        var typeface = new Typeface(family);
        var narrow = Width("iiiiiiiiii", typeface);
        var wide = Width("WWWWWWWWWW", typeface);
        return narrow > 0 && Math.Abs(narrow - wide) < 0.5;
    }

    private static double Width(string text, Typeface typeface) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 20, Brushes.Black).WidthIncludingTrailingWhitespace;
}
