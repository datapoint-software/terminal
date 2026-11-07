using System.Globalization;
using System.Text.Json;

namespace Datapoint.Terminal.Sessions.Recording;

// A recording read back for playing. Pauses longer than MaxIdle are shortened to it, as asciinema's idle_time_limit does, so a
// session left open over lunch plays in seconds.
public sealed class SessionRecording
{
    public const double MaxIdle = 2;

    private SessionRecording(int columns, int rows, string title, DateTimeOffset? started, IReadOnlyList<RecordingEvent> events)
    {
        Columns = columns;
        Rows = rows;
        Title = title;
        Started = started;
        Events = events;
    }

    public int Columns { get; }

    public int Rows { get; }

    public string Title { get; }

    public DateTimeOffset? Started { get; }

    public IReadOnlyList<RecordingEvent> Events { get; }

    public double Duration => Events.Count == 0 ? 0 : Events[^1].Time;

    public IEnumerable<RecordingEvent> Markers => Events.Where(item => string.Equals(item.Type, "m", StringComparison.Ordinal));

    // A file cut short, e.g. by a crash, plays up to its last whole line.
    public static SessionRecording Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        using var header = JsonDocument.Parse(reader.ReadLine() ?? throw new FormatException("The recording is empty."));
        var root = header.RootElement;
        if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 2)
        {
            throw new FormatException("Only asciicast version 2 recordings can be played.");
        }

        var started = root.TryGetProperty("timestamp", out var timestamp) && timestamp.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : (DateTimeOffset?)null;
        var title = root.TryGetProperty("title", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() ?? string.Empty : string.Empty;
        return new SessionRecording(root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), title, started, ReadEvents(reader));
    }

    private static List<RecordingEvent> ReadEvents(TextReader reader)
    {
        var events = new List<RecordingEvent>();
        double last = 0, shift = 0;
        while (reader.ReadLine() is { } line)
        {
            if (Read(line) is not { } item)
            {
                continue;
            }

            shift += Math.Max(0, item.Time - last - MaxIdle);
            last = item.Time;
            events.Add(item with { Time = item.Time - shift });
        }

        return events;
    }

    private static RecordingEvent? Read(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var array = document.RootElement;
            return array.ValueKind == JsonValueKind.Array && array.GetArrayLength() == 3
                ? new RecordingEvent(array[0].GetDouble(), array[1].GetString() ?? string.Empty, array[2].GetString() ?? string.Empty)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static (int Columns, int Rows)? ParseSize(string data)
    {
        var parts = data.Split('x');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var columns)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rows)
            && columns > 0 && rows > 0
            ? (columns, rows)
            : null;
    }
}
