using System.Globalization;
using Avalonia.Media;

namespace Datapoint.Terminal.Controls.Rendering;

public sealed record CellMetrics(double Width, double Height, double Baseline)
{
    public static CellMetrics Measure(Typeface typeface, double fontSize, double renderScaling)
    {
        var sample = new FormattedText("MMMMMMMMMM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.White);
        var height = Math.Ceiling(sample.Height * renderScaling) / renderScaling;
        return new CellMetrics(sample.WidthIncludingTrailingWhitespace / 10, height, sample.Baseline);
    }
}
