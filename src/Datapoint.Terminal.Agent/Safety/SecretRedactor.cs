using System.Text;
using System.Text.RegularExpressions;
using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Agent.Safety;

// A first set of rules; engineering.md's full design adds gitleaks' rules and Microsoft's detectors.
public sealed partial class SecretRedactor
{
    public const string Mask = "[secret removed]";

    private readonly HashSet<string> _known = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    // Short values are ignored, since they would match ordinary text.
    public void AddKnownSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < 8)
        {
            return;
        }

        lock (_lock)
        {
            _known.Add(secret);
        }
    }

    public string Redact(string text) => Redact(text, isAllowed: null);

    // Values the user marked as not secret are left as they are.
    public string Redact(string text, Func<string, bool>? isAllowed)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new StringBuilder(text.Length);
        var end = 0;
        foreach (var match in Find(text).Where(match => isAllowed?.Invoke(match.Value) != true))
        {
            result.Append(text, end, match.Index - end).Append(Mask);
            end = match.Index + match.Length;
        }

        return result.Append(text, end, text.Length - end).ToString();
    }

    // In order and without overlaps. When the rules take too long, the whole text is one match: sending nothing is safer
    // than risking a secret.
    public IReadOnlyList<SecretMatch> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return [];
        }

        List<(int Index, int Length)> spans;
        try
        {
            spans = [.. KnownSpans(text), .. Spans(PrivateKey(), text), .. Spans(TokenFormats(), text), .. AssignmentSpans(text)];
        }
        catch (RegexMatchTimeoutException)
        {
            spans = [(0, text.Length)];
        }

        return Merge(spans).Select(span => new SecretMatch(span.Index, span.Length, text.Substring(span.Index, span.Length), LineOf(text, span.Index))).ToList();
    }

    // Remembering a value as "not a secret" keeps only this hash, so the application's settings never hold the value itself.
    public static string Fingerprint(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(Hashing.Hash(HashKind.Sha256, Encoding.UTF8.GetBytes(value)));
    }

    private List<(int Index, int Length)> KnownSpans(string text)
    {
        string[] known;
        lock (_lock)
        {
            known = [.. _known];
        }

        var spans = new List<(int, int)>();
        foreach (var secret in known)
        {
            for (var index = text.IndexOf(secret, StringComparison.Ordinal); index >= 0; index = text.IndexOf(secret, index + secret.Length, StringComparison.Ordinal))
            {
                spans.Add((index, secret.Length));
            }
        }

        return spans;
    }

    private static IEnumerable<(int Index, int Length)> Spans(Regex regex, string text) =>
        regex.Matches(text).Select(match => (match.Index, match.Length));

    // Only the value is a secret; its name stays so the text still reads.
    private static IEnumerable<(int Index, int Length)> AssignmentSpans(string text) =>
        Assignment().Matches(text).Select(match => match.Groups["value"]).Select(value => (value.Index, value.Length));

    private static List<(int Index, int Length)> Merge(List<(int Index, int Length)> spans)
    {
        var merged = new List<(int Index, int Length)>();
        foreach (var (index, length) in spans.OrderBy(span => span.Index))
        {
            if (merged.Count > 0 && index <= merged[^1].Index + merged[^1].Length)
            {
                var last = merged[^1];
                merged[^1] = (last.Index, Math.Max(last.Length, index + length - last.Index));
                continue;
            }

            merged.Add((index, length));
        }

        return merged;
    }

    private static int LineOf(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex PrivateKey();

    [GeneratedRegex(
        @"\b(sk-ant-[A-Za-z0-9_\-]{20,}|sk-(proj-)?[A-Za-z0-9_\-]{20,}|gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{40,}|xox[baprs]-[A-Za-z0-9\-]{10,}|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_\-]{35}|glpat-[A-Za-z0-9_\-]{20,}|eyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,})",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex TokenFormats();

    [GeneratedRegex(
        @"(?<name>\b[A-Za-z_]*(password|passwd|secret|token|api[_-]?key)[A-Za-z_]*)(?<separator>\s*[=:]\s*)(?<value>""[^""\n]{4,}""|'[^'\n]{4,}'|[^\s""']{4,})",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex Assignment();
}
