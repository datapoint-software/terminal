using System.Globalization;
using System.Text;

namespace Datapoint.Terminal.Sessions.Ssh;

public static class ServerText
{
    public const int DefaultMaxLength = 4096;

    public static string Sanitize(string text, int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var rune in text.EnumerateRunes())
        {
            if (clean.Length >= maxLength)
            {
                break;
            }

            AppendIfSafe(clean, rune);
        }

        return clean.ToString();
    }

    private static void AppendIfSafe(StringBuilder clean, Rune rune)
    {
        if (rune.Value is '\n' or '\t')
        {
            clean.Append((char)rune.Value);
            return;
        }

        var category = Rune.GetUnicodeCategory(rune);
        var isUnsafe = category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse;
        if (isUnsafe)
        {
            return;
        }

        clean.Append(rune.ToString());
    }
}
