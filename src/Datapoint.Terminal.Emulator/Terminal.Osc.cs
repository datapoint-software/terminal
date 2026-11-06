using System.Buffers.Text;
using System.Text;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    public const int MaxHyperlinks = 4096;
    public const int MaxHyperlinkLength = 2048;
    public const int MaxClipboardBytes = 1024 * 1024;

    private readonly List<string> _hyperlinks = [];

    public string? GetHyperlink(int id) => id > 0 && id <= _hyperlinks.Count ? _hyperlinks[id - 1] : null;

    public void OscDispatch(ReadOnlySpan<byte> data)
    {
        var separator = data.IndexOf((byte)';');
        var command = separator < 0 ? data : data[..separator];
        var argument = separator < 0 ? [] : data[(separator + 1)..];
        switch (Encoding.ASCII.GetString(command))
        {
            case "0" or "2":
                SetTitle(argument);
                break;
            case "7":
                SetWorkingDirectory(Encoding.UTF8.GetString(argument));
                break;
            case "8":
                SetHyperlink(argument);
                break;
            case "52":
                WriteClipboard(argument);
                break;
            case "133" or "633":
                HandleShellIntegration(Encoding.UTF8.GetString(argument));
                break;
            default:
                HandleColorQuery(command, argument);
                break;
        }
    }

    private void SetTitle(ReadOnlySpan<byte> argument)
    {
        var title = Encoding.UTF8.GetString(argument);
        Title = title.Length > MaxTitleLength ? title[..MaxTitleLength] : title;
        _host.TitleChanged(Title);
    }

    // The host is the shell's own claim: a hint, never a basis for a security decision.
    private void SetWorkingDirectory(string uri)
    {
        if (uri.Length > MaxHyperlinkLength || !TryParseFileUri(uri, out var host, out var path))
        {
            return;
        }

        WorkingDirectory = path;
        WorkingDirectoryHost = host;
        _host.WorkingDirectoryChanged(path);
    }

    internal static bool TryParseFileUri(string uri, out string host, out string path)
    {
        const string scheme = "file://";
        host = string.Empty;
        path = string.Empty;
        if (!uri.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var slash = uri.IndexOf('/', scheme.Length);
        if (slash < 0)
        {
            return false;
        }

        host = uri[scheme.Length..slash];
        try
        {
            path = Uri.UnescapeDataString(uri[slash..]);
        }
        catch (UriFormatException)
        {
            return false;
        }

        var isWindowsDrive = path.Length >= 3 && path[0] == '/' && char.IsAsciiLetter(path[1]) && path[2] == ':';
        path = isWindowsDrive ? path[1..] : path;
        return !path.Any(char.IsControl);
    }

    private void SetHyperlink(ReadOnlySpan<byte> argument)
    {
        var separator = argument.IndexOf((byte)';');
        var uri = separator < 0 ? string.Empty : Encoding.UTF8.GetString(argument[(separator + 1)..]);
        if (uri.Length == 0 || uri.Length > MaxHyperlinkLength || _hyperlinks.Count >= MaxHyperlinks)
        {
            Cursor.HyperlinkId = 0;
            return;
        }

        _hyperlinks.Add(uri);
        Cursor.HyperlinkId = _hyperlinks.Count;
    }

    private void WriteClipboard(ReadOnlySpan<byte> argument)
    {
        var separator = argument.IndexOf((byte)';');
        var encoded = separator < 0 ? [] : argument[(separator + 1)..];
        if (encoded.IsEmpty || encoded.Length > MaxClipboardBytes || encoded.SequenceEqual("?"u8))
        {
            return;
        }

        var decoded = new byte[Base64.GetMaxDecodedFromUtf8Length(encoded.Length)];
        if (Base64.DecodeFromUtf8(encoded, decoded, out _, out var written) != System.Buffers.OperationStatus.Done)
        {
            return;
        }

        _host.ClipboardWriteRequested(Encoding.UTF8.GetString(decoded, 0, written));
    }
}
