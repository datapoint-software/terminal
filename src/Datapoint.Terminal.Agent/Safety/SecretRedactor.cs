using System.Text.RegularExpressions;

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

    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] known;
        lock (_lock)
        {
            known = [.. _known];
        }

        foreach (var secret in known)
        {
            text = text.Replace(secret, Mask, StringComparison.Ordinal);
        }

        try
        {
            text = PrivateKey().Replace(text, Mask);
            text = TokenFormats().Replace(text, Mask);
            return Assignment().Replace(text, match => match.Groups["name"].Value + match.Groups["separator"].Value + Mask);
        }
        catch (RegexMatchTimeoutException)
        {
            // Too slow to check safely: send nothing rather than risk a secret.
            return Mask;
        }
    }

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
