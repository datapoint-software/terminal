using Datapoint.Terminal.Emulator.Parsing;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    private void SelectGraphicRendition(CsiParameters parameters)
    {
        if (parameters.Count == 0)
        {
            Cursor.Style = CellStyle.Default;
            return;
        }

        var index = 0;
        while (index < parameters.Count)
        {
            index = ApplyRendition(parameters, index);
        }
    }

    private int ApplyRendition(CsiParameters parameters, int index)
    {
        var value = parameters[index];
        if (value is 38 or 48 or 58)
        {
            return ApplyExtendedColor(parameters, index);
        }

        if (value == 4 && parameters.HasSubParameterAfter(index))
        {
            Cursor.Style = Cursor.Style with { Underline = UnderlineFromSubParameter(parameters[index + 1]) };
            return SkipSubParameters(parameters, index);
        }

        Cursor.Style = ApplySimpleRendition(Cursor.Style, value);
        return SkipSubParameters(parameters, index);
    }

    private static CellStyle ApplySimpleRendition(CellStyle style, int value) => value switch
    {
        0 => CellStyle.Default,
        >= 30 and <= 37 => style with { Foreground = TerminalColor.Indexed(value - 30) },
        >= 40 and <= 47 => style with { Background = TerminalColor.Indexed(value - 40) },
        >= 90 and <= 97 => style with { Foreground = TerminalColor.Indexed(value - 90 + 8) },
        >= 100 and <= 107 => style with { Background = TerminalColor.Indexed(value - 100 + 8) },
        39 => style with { Foreground = TerminalColor.Default },
        49 => style with { Background = TerminalColor.Default },
        59 => style with { UnderlineColor = TerminalColor.Default },
        _ => ApplyAttribute(style, value),
    };

    private static CellStyle ApplyAttribute(CellStyle style, int value) => value switch
    {
        1 => style.With(CellAttributes.Bold),
        2 => style.With(CellAttributes.Faint),
        3 => style.With(CellAttributes.Italic),
        4 => style with { Underline = UnderlineStyle.Line },
        5 or 6 => style.With(CellAttributes.Blink),
        7 => style.With(CellAttributes.Inverse),
        8 => style.With(CellAttributes.Invisible),
        9 => style.With(CellAttributes.Strikethrough),
        21 => style with { Underline = UnderlineStyle.DoubleLine },
        _ => RemoveAttribute(style, value),
    };

    private static CellStyle RemoveAttribute(CellStyle style, int value) => value switch
    {
        22 => style.Without(CellAttributes.Bold | CellAttributes.Faint),
        23 => style.Without(CellAttributes.Italic),
        24 => style with { Underline = UnderlineStyle.None },
        25 => style.Without(CellAttributes.Blink),
        27 => style.Without(CellAttributes.Inverse),
        28 => style.Without(CellAttributes.Invisible),
        29 => style.Without(CellAttributes.Strikethrough),
        53 => style.With(CellAttributes.Overline),
        55 => style.Without(CellAttributes.Overline),
        _ => style,
    };

    private static UnderlineStyle UnderlineFromSubParameter(int value) => value switch
    {
        0 => UnderlineStyle.None,
        1 => UnderlineStyle.Line,
        2 => UnderlineStyle.DoubleLine,
        3 => UnderlineStyle.Curly,
        4 => UnderlineStyle.Dotted,
        5 => UnderlineStyle.Dashed,
        _ => UnderlineStyle.Line,
    };

    private int ApplyExtendedColor(CsiParameters parameters, int index)
    {
        var target = parameters[index];
        var (color, next) = parameters.HasSubParameterAfter(index)
            ? ReadColonColor(parameters, index)
            : ReadSemicolonColor(parameters, index);
        if (color is { } value)
        {
            Cursor.Style = target switch
            {
                38 => Cursor.Style with { Foreground = value },
                48 => Cursor.Style with { Background = value },
                _ => Cursor.Style with { UnderlineColor = value },
            };
        }

        return next;
    }

    private static (TerminalColor? Color, int Next) ReadColonColor(CsiParameters parameters, int index)
    {
        var end = SkipSubParameters(parameters, index);
        var groupLength = end - index - 1;
        var kind = parameters[index + 1];
        if (kind == 5 && groupLength >= 2)
        {
            return (IndexedColor(parameters[index + 2]), end);
        }

        if (kind == 2 && groupLength >= 4)
        {
            return (RgbColor(parameters[end - 3], parameters[end - 2], parameters[end - 1]), end);
        }

        return (null, end);
    }

    private static (TerminalColor? Color, int Next) ReadSemicolonColor(CsiParameters parameters, int index)
    {
        var kind = parameters.GetOrDefault(index + 1, 0);
        if (kind == 5 && index + 2 < parameters.Count)
        {
            return (IndexedColor(parameters[index + 2]), index + 3);
        }

        if (kind == 2 && index + 4 < parameters.Count)
        {
            return (RgbColor(parameters[index + 2], parameters[index + 3], parameters[index + 4]), index + 5);
        }

        return (null, parameters.Count);
    }

    private static TerminalColor? IndexedColor(int value) =>
        value is >= 0 and <= 255 ? TerminalColor.Indexed(value) : null;

    private static TerminalColor? RgbColor(int red, int green, int blue)
    {
        var isValid = red <= 255 && green <= 255 && blue <= 255;
        return isValid ? TerminalColor.Rgb((byte)red, (byte)green, (byte)blue) : null;
    }

    private static int SkipSubParameters(CsiParameters parameters, int index)
    {
        var next = index;
        while (parameters.HasSubParameterAfter(next))
        {
            next++;
        }

        return next + 1;
    }
}
