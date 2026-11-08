using System.Text.Json;

namespace Datapoint.Terminal.Settings;

// Writes go to a temporary file first and replace the old one in one step, so a crash never leaves half a file;
// on macOS and Linux the file is readable only by the user.
public sealed class SettingsStore(string path)
{
    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Datapoint Terminal",
        "settings.json");

    public string Path { get; } = path;

    // Set when the file was written by a newer version; saving is then refused so its settings aren't lost.
    public bool IsReadOnly { get; private set; }

    // A file that couldn't be read is kept aside and defaults are used, so nothing in it is lost.
    public string? Problem { get; private set; }

    public AppSettings Load()
    {
        if (!File.Exists(Path))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize(File.ReadAllBytes(Path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            IsReadOnly = settings.Version > AppSettings.CurrentVersion;
            return settings;
        }
        catch (JsonException exception)
        {
            var aside = $"{Path}.unreadable-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Move(Path, aside);
            Problem = Resources.Strings.Format(Resources.Strings.SettingsUnreadable, exception.Message, aside);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (IsReadOnly)
        {
            throw new InvalidOperationException("A newer version of Datapoint Terminal wrote these settings; this version won't overwrite them.");
        }

        var folder = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(folder);
        var temporary = System.IO.Path.Combine(folder, $".settings-{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        }

        File.Move(temporary, Path, overwrite: true);
    }
}
