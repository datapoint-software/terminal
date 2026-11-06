using System.Text;

namespace Datapoint.Terminal.Controls.Input;

public static class PasteEncoder
{
    // Any end marker inside pasted text is removed, so it can't end the bracketed paste early and run as typed commands.
    public static byte[] Encode(string text, bool bracketed)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');
        if (!bracketed)
        {
            return Encoding.UTF8.GetBytes(normalized);
        }

        var safe = normalized.Replace("\e[201~", string.Empty, StringComparison.Ordinal).Replace("\e[200~", string.Empty, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes("\e[200~" + safe + "\e[201~");
    }
}
