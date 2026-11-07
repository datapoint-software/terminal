using System.Text;
using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Runbooks;

// A runbook is Markdown people can read and edit: a title, what it does, its parameters as {{name}} placeholders, and the steps.
public static partial class RunbookDocument
{
    public const int MaxBytes = 32 * 1024;

    public const string ParametersHeading = "## Parameters";

    public static string Create(string title, string description, IReadOnlyList<RunbookParameter> parameters, string steps)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var text = new StringBuilder().Append("# ").Append(title.Trim()).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Append(description.Trim()).Append("\n\n");
        }

        if (parameters.Count > 0)
        {
            text.Append(ParametersHeading).Append("\n\n");
            foreach (var parameter in parameters)
            {
                text.Append("- `").Append(parameter.Name).Append("`: ").Append(parameter.Description.Trim()).Append('\n');
            }

            text.Append('\n');
        }

        return text.Append("## Steps\n\n").Append(steps.Trim()).Append('\n').ToString();
    }

    // Lowercase words joined by hyphens, so "Renew certificates" and "renew-certificates" are the same runbook.
    public static string FileNameFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var slug = new StringBuilder();
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            slug.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var text = Hyphens().Replace(slug.ToString(), "-").Trim('-');
        return (text.Length == 0 ? "runbook" : text) + ".md";
    }

    // Names from the Parameters section's list, and any {{name}} placeholders the steps use.
    public static IReadOnlyList<string> Parameters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. ListedParameter().Matches(text).Select(match => match.Groups["name"].Value)
            .Concat(Placeholder().Matches(text).Select(match => match.Groups["name"].Value))
            .Distinct(StringComparer.Ordinal)];
    }

    public static bool IsValidParameterName(string name) => ParameterName().IsMatch(name);

    [GeneratedRegex("-{2,}", RegexOptions.None, 1000)]
    private static partial Regex Hyphens();

    [GeneratedRegex(@"^- `(?<name>[A-Za-z][A-Za-z0-9_]{0,39})`:", RegexOptions.Multiline | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex ListedParameter();

    [GeneratedRegex(@"\{\{(?<name>[A-Za-z][A-Za-z0-9_]{0,39})\}\}", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Placeholder();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,39}$", RegexOptions.None, 1000)]
    private static partial Regex ParameterName();
}
