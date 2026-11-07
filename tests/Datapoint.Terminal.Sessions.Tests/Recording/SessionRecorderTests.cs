using System.Text;
using Datapoint.Terminal.Sessions.Recording;

namespace Datapoint.Terminal.Sessions.Tests.Recording;

public sealed class SessionRecorderTests : IDisposable
{
    private readonly SteppedTime _time = new();
    private readonly MemoryStream _stream = new();

    public void Dispose() => _stream.Dispose();

    private SessionRecorder Create() =>
        new(_stream, 80, 24, "web1 — shop", text => text.Replace("hunter2", "[secret removed]", StringComparison.Ordinal), _time);

    private string[] Lines() => Encoding.UTF8.GetString(_stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Writes_an_asciicast_header()
    {
        Create().Dispose();

        using var header = System.Text.Json.JsonDocument.Parse(Lines()[0]);
        var root = header.RootElement;
        Assert.Equal((2, 80, 24, "web1 — shop"), (root.GetProperty("version").GetInt32(), root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), root.GetProperty("title").GetString()));
        Assert.Equal(_time.GetUtcNow().ToUnixTimeSeconds(), root.GetProperty("timestamp").GetInt64());
    }

    [Fact]
    public void Writes_whole_lines_redacted_even_when_a_secret_is_split_across_reads()
    {
        using (var recorder = Create())
        {
            _time.Advance(1.5);
            recorder.Observe("password=hun"u8);
            recorder.Observe("ter2\n$ "u8);
            _time.Advance(0.25);
        }

        Assert.Equal(["[1.5,\"o\",\"password=[secret removed]\\n\"]", "[1.5,\"o\",\"$ \"]"], Lines()[1..]);
    }

    [Fact]
    public void Keeps_characters_split_across_reads()
    {
        var bytes = Encoding.UTF8.GetBytes("café\n");
        using (var recorder = Create())
        {
            recorder.Observe(bytes.AsSpan(0, 4));
            recorder.Observe(bytes.AsSpan(4));
        }

        Assert.Equal("[0,\"o\",\"caf\\u00E9\\n\"]", Lines()[1]);
    }

    [Fact]
    public void Writes_markers_and_resizes_after_the_output_before_them()
    {
        using (var recorder = Create())
        {
            recorder.Observe("$ "u8);
            _time.Advance(2);
            recorder.Mark("Agent: echo hunter2");
            recorder.Resized(120, 40);
        }

        Assert.Equal(["[0,\"o\",\"$ \"]", "[2,\"m\",\"Agent: echo [secret removed]\"]", "[2,\"r\",\"120x40\"]"], Lines()[1..]);
    }

    [Fact]
    public void Reads_back_what_it_wrote_with_long_pauses_shortened()
    {
        using (var recorder = Create())
        {
            recorder.Observe("one\n"u8);
            _time.Advance(600);
            recorder.Mark("User: ls");
            _time.Advance(1);
            recorder.Observe("two\n"u8);
        }

        var recording = SessionRecording.Parse(new StringReader(Encoding.UTF8.GetString(_stream.ToArray())));

        Assert.Equal((80, 24, "web1 — shop"), (recording.Columns, recording.Rows, recording.Title));
        Assert.Equal([0, SessionRecording.MaxIdle, SessionRecording.MaxIdle + 1], recording.Events.Select(item => item.Time));
        Assert.Equal("User: ls", Assert.Single(recording.Markers).Data);
    }

    [Fact]
    public void Plays_a_recording_cut_short_up_to_its_last_whole_line()
    {
        var text = "{\"version\":2,\"width\":20,\"height\":5}\n[0.1,\"o\",\"hello\\r\\n\"]\n[0.2,\"o\",\"wor";

        var recording = SessionRecording.Parse(new StringReader(text));

        Assert.Single(recording.Events);
    }

    [Fact]
    public void Replays_and_goes_back_from_the_start()
    {
        var text = "{\"version\":2,\"width\":20,\"height\":5}\n[1,\"o\",\"first\\r\\n\"]\n[2,\"o\",\"second\\r\\n\"]\n[3,\"r\",\"30x6\"]\n";
        var player = new RecordingPlayer(SessionRecording.Parse(new StringReader(text)));
        var replaced = 0;
        player.TerminalReplaced += (_, _) => replaced++;

        player.Seek(2.5);
        var both = player.Terminal.Screen[1].GetText();
        player.Seek(1);
        var first = player.Terminal.Screen[1].GetText();
        player.Seek(10);

        Assert.Equal(("second", string.Empty, 1), (both.TrimEnd(), first.TrimEnd(), replaced));
        Assert.Equal((30, 6, true), (player.Terminal.Columns, player.Terminal.Rows, player.IsAtEnd));
    }
}
