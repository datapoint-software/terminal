using System.Collections.Concurrent;
using System.Text;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private const int MaxShownMatches = 20;
    private const int MaxShownLine = 160;

    // Choices for files as they were when the user decided, so an unchanged file isn't asked about at every conversation. Only
    // "not a secret" outlives the window.
    private readonly ConcurrentDictionary<(string Path, string Text), SecretChoice> _secretChoices = new();

    // Product decision 92: a file that looks like it holds a secret is sent masked, as written or not at all, as the user
    // chooses. Every file comes back reviewed, so the agent sends its text as it is.
    private async Task<(List<InstructionFile> Reviewed, List<string> Skipped)> ReviewSecretsAsync(
        IReadOnlyList<InstructionFile> files, CancellationToken cancellationToken)
    {
        var reviewed = new List<InstructionFile>();
        var skipped = new List<string>();
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ReviewSecretsAsync(file).ConfigureAwait(false) is { } result)
            {
                reviewed.Add(result);
                continue;
            }

            skipped.Add(file.Path);
        }

        return (reviewed, skipped);
    }

    private async Task<InstructionFile?> ReviewSecretsAsync(InstructionFile file)
    {
        var matches = _redactor.Find(file.Text).Where(match => !_settings.IsNotSecret(match.Value)).ToList();
        if (matches.Count == 0)
        {
            return file with { IsReviewed = true };
        }

        var key = (file.Path, SecretRedactor.Fingerprint(file.Text));
        if (!_secretChoices.TryGetValue(key, out var choice))
        {
            choice = await Dispatcher.UIThread.InvokeAsync(() => AskAboutSecretsAsync(file, matches)).ConfigureAwait(false);
            _secretChoices[key] = choice;
        }

        return choice switch
        {
            SecretChoice.Skip => null,
            SecretChoice.AsWritten or SecretChoice.NotSecret => file with { IsReviewed = true },
            _ => file with { Text = _redactor.Redact(file.Text, _settings.IsNotSecret), IsReviewed = true },
        };
    }

    private async Task<SecretChoice> AskAboutSecretsAsync(InstructionFile file, IReadOnlyList<SecretMatch> matches)
    {
        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.SecretTitle,
            Strings.Format(Strings.SecretMessage, ProjectFolders.Display(file.Path), SecretRedactor.Mask),
            DescribeMatches(file.Text, matches),
            hasDefault: true,
            Strings.SecretSkip,
            Strings.SecretAsWritten,
            Strings.SecretNotSecret,
            Strings.SecretMasked).ConfigureAwait(true);
        var choice = answer is { } index ? (SecretChoice)index : SecretChoice.Masked;
        if (choice == SecretChoice.NotSecret)
        {
            _settings.MarkNotSecret(matches.Select(match => match.Value));
        }

        return choice;
    }

    // Each matching line in full, so the user can judge it; it's shown only here, on their own screen.
    private static string DescribeMatches(string text, IReadOnlyList<SecretMatch> matches)
    {
        var lines = text.Split('\n');
        var shown = matches.Select(match => match.Line).Distinct().ToList();
        var details = new StringBuilder();
        foreach (var number in shown.Take(MaxShownMatches))
        {
            var line = lines[number - 1].Trim();
            line = line.Length > MaxShownLine ? line[..MaxShownLine] + "…" : line;
            details.AppendLine(Strings.Format(Strings.SecretLine, number, line));
        }

        if (shown.Count > MaxShownMatches)
        {
            details.AppendLine(Strings.Format(Strings.SecretMore, shown.Count - MaxShownMatches));
        }

        return details.ToString().TrimEnd();
    }
}
