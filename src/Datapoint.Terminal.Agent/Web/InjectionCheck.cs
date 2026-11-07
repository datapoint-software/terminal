using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Web;

// Finds text on a page or in search results that speaks to an AI model rather than to people reading it: the usual openings
// of a prompt injection. It catches only the crude ones, so it warns the user and the agent and never stands in for the
// other defences; nothing is removed, so the user sees what the agent saw.
public static partial class InjectionCheck
{
    // The matched words, with some of what follows, for the user to recognise; null when nothing matches.
    public static string? Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var match = Pattern().Match(text);
            if (!match.Success)
            {
                return null;
            }

            var end = Math.Min(text.Length, match.Index + match.Length + 60);
            return string.Join(' ', text[match.Index..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).TrimEnd() + (end < text.Length ? "…" : string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    [GeneratedRegex(
        @"\b(?:ignore|disregard|forget|override)\s+(?:all\s+|any\s+|the\s+|your\s+)*(?:previous|prior|above|earlier|system|original)\s+(?:instructions|prompts?|rules|directions)"
        + @"|\b(?:new|updated|real|actual)\s+instructions\s*:"
        + @"|\bnote\s+to\s+(?:the\s+)?(?:ai|llm|assistant|agent|model)\b"
        + @"|\b(?:if\s+you\s+are|you\s+are\s+(?:now\s+)?)\s+an?\s+(?:ai|llm|language\s+model|ai\s+assistant|ai\s+agent)\b"
        + @"|\b(?:ai|llm)\s+(?:assistants?|agents?|models?)\s+(?:must|should|reading\s+this)\b"
        + @"|\bdo\s+not\s+(?:tell|inform|mention\s+(?:this\s+)?to)\s+the\s+user\b"
        + @"|\byour\s+system\s+prompt\b"
        + @"|<\|im_start\|>|\[INST\]|<<SYS>>|</?system>",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex Pattern();
}
