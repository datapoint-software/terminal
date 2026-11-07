using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Datapoint.Terminal.Themes;

// Reads colour schemes other terminals use: iTerm2 (.itermcolors), Windows Terminal (a scheme's JSON), Ghostty and kitty
// (key and value per line). Only colours are read; nothing in a theme file can run or change anything else.
public static class ThemeImport
{
    private static readonly string[] s_windowsNames =
    [
        "black", "red", "green", "yellow", "blue", "purple", "cyan", "white",
        "brightBlack", "brightRed", "brightGreen", "brightYellow", "brightBlue", "brightPurple", "brightCyan", "brightWhite",
    ];

    // Null, with the reason, when the file isn't a theme this can read or lacks colours a theme needs.
    public static (ColorTheme? Theme, string? Problem) Read(string fileName, string text)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(text);
        var name = Path.GetFileNameWithoutExtension(fileName);
        var trimmed = text.TrimStart();
        try
        {
            var (colors, source) = trimmed.StartsWith('<') ? (ReadITerm(trimmed), "iTerm2")
                : trimmed.StartsWith('{') ? (ReadWindowsTerminal(trimmed, ref name), "Windows Terminal")
                : ReadKeyValues(text);
            return Build(name, colors, source);
        }
        catch (Exception exception) when (exception is XmlException or JsonException or FormatException or InvalidOperationException)
        {
            return (null, exception.Message);
        }
    }

    private static (ColorTheme?, string?) Build(string name, Dictionary<string, int> colors, string source)
    {
        var ansi = new int[16];
        for (var index = 0; index < 16; index++)
        {
            if (!colors.TryGetValue(index.ToString(CultureInfo.InvariantCulture), out ansi[index]))
            {
                return (null, $"colour {index} is missing");
            }
        }

        if (!colors.TryGetValue("foreground", out var foreground) || !colors.TryGetValue("background", out var background))
        {
            return (null, "the text or background colour is missing");
        }

        return (new ColorTheme
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Length == 0 ? source : name,
            Ansi = ansi,
            Foreground = foreground,
            Background = background,
            Cursor = colors.GetValueOrDefault("cursor", foreground),
            Selection = colors.TryGetValue("selection", out var selection) ? selection : null,
            Source = source,
        }, null);
    }

    // A property list: each colour is a dictionary of red, green and blue components from 0 to 1.
    private static Dictionary<string, int> ReadITerm(string text)
    {
        var document = XDocument.Parse(text, LoadOptions.None);
        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        var root = document.Root?.Element("dict") ?? throw new FormatException("not an iTerm2 colour file");
        foreach (var (key, value) in Pairs(root))
        {
            var components = Pairs(value).ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal);
            if (!components.TryGetValue("Red Component", out var red) || !components.TryGetValue("Green Component", out var green) || !components.TryGetValue("Blue Component", out var blue))
            {
                continue;
            }

            var color = (Component(red) << 16) | (Component(green) << 8) | Component(blue);
            if (ITermName(key) is not { } name)
            {
                continue;
            }

            colors[name] = color;
        }

        return colors;
    }

    private static IEnumerable<(string Key, XElement Value)> Pairs(XElement dict)
    {
        var elements = dict.Elements().ToList();
        for (var index = 0; index + 1 < elements.Count; index += 2)
        {
            if (elements[index].Name == "key")
            {
                yield return (elements[index].Value, elements[index + 1]);
            }
        }
    }

    private static int Component(string value) =>
        (int)Math.Round(Math.Clamp(double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture), 0, 1) * 255);

    private static string? ITermName(string key) => key switch
    {
        "Foreground Color" => "foreground",
        "Background Color" => "background",
        "Cursor Color" => "cursor",
        "Selection Color" => "selection",
        _ when key.StartsWith("Ansi ", StringComparison.Ordinal) && key.EndsWith(" Color", StringComparison.Ordinal) => key[5..^6],
        _ => null,
    };

    // One scheme, or a settings file whose "schemes" list holds it (the first is taken).
    private static Dictionary<string, int> ReadWindowsTerminal(string text, ref string name)
    {
        using var document = JsonDocument.Parse(text);
        var scheme = document.RootElement;
        if (scheme.TryGetProperty("schemes", out var schemes) && schemes.ValueKind == JsonValueKind.Array && schemes.GetArrayLength() > 0)
        {
            scheme = schemes[0];
        }

        if (scheme.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String)
        {
            name = named.GetString() ?? name;
        }

        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < s_windowsNames.Length; index++)
        {
            Take(scheme, s_windowsNames[index], index.ToString(CultureInfo.InvariantCulture), colors);
        }

        Take(scheme, "foreground", "foreground", colors);
        Take(scheme, "background", "background", colors);
        Take(scheme, "cursorColor", "cursor", colors);
        Take(scheme, "selectionBackground", "selection", colors);
        return colors;
    }

    private static void Take(JsonElement scheme, string property, string key, Dictionary<string, int> colors)
    {
        if (!scheme.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || Hex(value.GetString()) is not { } color)
        {
            return;
        }

        colors[key] = color;
    }

    // Ghostty ("palette = 0=#1d1f21", "background = #..."), or kitty ("color0 #1d1f21", "background #...").
    private static (Dictionary<string, int>, string) ReadKeyValues(string text)
    {
        var colors = new Dictionary<string, int>(StringComparer.Ordinal);
        var isGhostty = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            isGhostty |= equals > 0;
            var (key, value) = equals > 0 ? (line[..equals].Trim(), line[(equals + 1)..].Trim()) : Split(line);
            Add(colors, key, value);
        }

        return (colors, isGhostty ? "Ghostty" : "kitty");
    }

    private static (string, string) Split(string line)
    {
        var space = line.IndexOfAny([' ', '\t']);
        return space < 0 ? (line, string.Empty) : (line[..space], line[(space + 1)..].Trim());
    }

    private static void Add(Dictionary<string, int> colors, string key, string value)
    {
        if (string.Equals(key, "palette", StringComparison.Ordinal) && value.Split('=', 2) is [var number, var color])
        {
            (key, value) = (number.Trim(), color.Trim());
        }
        else if (key.StartsWith("color", StringComparison.Ordinal) && int.TryParse(key.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
        {
            key = index.ToString(CultureInfo.InvariantCulture);
        }

        var name = key switch
        {
            "cursor-color" or "cursor" => "cursor",
            "selection-background" or "selection_background" => "selection",
            _ => key,
        };
        if (Hex(value) is not { } parsed || !IsThemeKey(name))
        {
            return;
        }

        colors[name] = parsed;
    }

    private static bool IsThemeKey(string name) =>
        name is "foreground" or "background" or "cursor" or "selection"
        || (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < 16);

    public static int? Hex(string? value)
    {
        var digits = value?.Trim().TrimStart('#');
        return digits is { Length: 6 } && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color) ? color : null;
    }
}
