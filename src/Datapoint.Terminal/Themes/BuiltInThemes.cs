namespace Datapoint.Terminal.Themes;

// Our own palettes. Light keeps every colour readable on white, so "white" text is a light grey and yellow is a dark gold.
public static class BuiltInThemes
{
    public const string SystemId = "system";

    public static ColorTheme Dark { get; } = new()
    {
        Id = "dark",
        Name = "Default dark",
        Ansi =
        [
            0x000000, 0xCD3131, 0x0DBC79, 0xE5E510, 0x2472C8, 0xBC3FBC, 0x11A8CD, 0xE5E5E5,
            0x666666, 0xF14C4C, 0x23D18B, 0xF5F543, 0x3B8EEA, 0xD670D6, 0x29B8DB, 0xFFFFFF,
        ],
        Foreground = 0xCCCCCC,
        Background = 0x1E1E1E,
        Cursor = 0xCCCCCC,
    };

    public static ColorTheme Light { get; } = new()
    {
        Id = "light",
        Name = "Default light",
        Ansi =
        [
            0x1F1F1F, 0xB42318, 0x1E7B34, 0x8A6A00, 0x1F5FBF, 0x8E3BB0, 0x0E7490, 0xA8A8A8,
            0x5C5C5C, 0xD92D20, 0x2E9E4B, 0xA67C00, 0x2F80ED, 0xA855F7, 0x0891B2, 0xC8C8C8,
        ],
        Foreground = 0x1F1F1F,
        Background = 0xFFFFFF,
        Cursor = 0x1F1F1F,
    };
}
