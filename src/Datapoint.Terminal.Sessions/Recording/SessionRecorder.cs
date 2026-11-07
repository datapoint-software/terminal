using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Datapoint.Terminal.Sessions.Recording;

// Writes a session as asciicast v2 (https://docs.asciinema.org/manual/asciicast/v2/): a JSON header, then one JSON array per
// event with the seconds since the start. Output is written a line at a time so secrets are redacted whole; markers ("m")
// say who typed each command and what the agent did beside the terminal (product decisions 45 and 58). What the user types is
// never recorded: their input is seen as the terminal echoes it, so passwords typed at a prompt stay out.
public sealed class SessionRecorder : ITerminalOutputObserver, IDisposable
{
    // A partial line longer than this is written anyway, e.g. a progress bar redrawn without line breaks.
    private const int MaxPending = 8 * 1024;

    private static readonly TimeSpan s_flushInterval = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly BufferedStream _stream;
    private readonly Func<string, string> _redact;
    private readonly TimeProvider _time;
    private readonly long _start;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _pending = new();
    private long _lastFlush;

    // When the output waiting for the end of its line arrived; it's written with that time, not the time its line ended.
    private double _pendingSince;
    private bool _isDisposed;

    public SessionRecorder(Stream stream, int columns, int rows, string title, Func<string, string> redact, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(redact);
        _stream = new BufferedStream(stream);
        _redact = redact;
        _time = time ?? TimeProvider.System;
        _start = _time.GetTimestamp();
        _lastFlush = _start;
        var started = _time.GetUtcNow().ToUnixTimeSeconds();
        Line(json =>
        {
            json.WriteStartObject();
            json.WriteNumber("version", 2);
            json.WriteNumber("width", columns);
            json.WriteNumber("height", rows);
            json.WriteNumber("timestamp", started);
            json.WriteString("title", title);
            json.WriteStartObject("env");
            json.WriteString("TERM", "xterm-256color");
            json.WriteEndObject();
            json.WriteEndObject();
        });
        _stream.Flush();
    }

    public void Observe(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            var chars = new char[_decoder.GetCharCount(data, flush: false)];
            _decoder.GetChars(data, chars, flush: false);
            if (_pending.Length == 0)
            {
                _pendingSince = Now;
            }

            _pending.Append(chars);
            var text = _pending.ToString();
            var end = text.Length > MaxPending ? text.Length : text.LastIndexOf('\n') + 1;
            if (end > 0)
            {
                Write("o", _redact(text[..end]), _pendingSince);
                _pending.Remove(0, end);
                _pendingSince = Now;
            }

            FlushNow(force: false);
        }
    }

    public void Resized(int columns, int rows) => Event("r", string.Create(CultureInfo.InvariantCulture, $"{columns}x{rows}"));

    public void Mark(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        Event("m", _redact(label));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            WritePending();
            _isDisposed = true;
            _stream.Dispose();
        }
    }

    // Output so far goes first, so the event lands after what led to it.
    private void Event(string type, string data)
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            WritePending();
            Write(type, data, Now);
            FlushNow(force: true);
        }
    }

    private void WritePending()
    {
        if (_pending.Length == 0)
        {
            return;
        }

        Write("o", _redact(_pending.ToString()), _pendingSince);
        _pending.Clear();
    }

    private double Now => Math.Round(_time.GetElapsedTime(_start).TotalSeconds, 6);

    private void Write(string type, string data, double seconds) =>
        Line(json =>
        {
            json.WriteStartArray();
            json.WriteNumberValue(seconds);
            json.WriteStringValue(type);
            json.WriteStringValue(data);
            json.WriteEndArray();
        });

    private void Line(Action<Utf8JsonWriter> write)
    {
        using (var json = new Utf8JsonWriter(_stream))
        {
            write(json);
        }

        _stream.WriteByte((byte)'\n');
    }

    private void FlushNow(bool force)
    {
        if (!force && _time.GetElapsedTime(_lastFlush) < s_flushInterval)
        {
            return;
        }

        _stream.Flush();
        _lastFlush = _time.GetTimestamp();
    }
}
