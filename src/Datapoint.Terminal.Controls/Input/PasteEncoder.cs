using System.Text;

namespace Datapoint.Terminal.Controls.Input;

/// <summary>Prepares pasted text for the shell.</summary>
public static class PasteEncoder
{
    /// <summary>
    /// Line breaks become carriage returns, as typed. With bracketed paste the text is wrapped in its markers, and any end marker
    /// inside it is removed, so pasted text can't end the paste early and run as typed commands.
    /// </summary>
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
