using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Markdown;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// The change to a file on a host, the command that will save it and where the old version goes, before anything is written.
public sealed partial class SaveHostFileWindow : Window
{
    public SaveHostFileWindow() => InitializeComponent();

    public static Task<bool> AskAsync(Window owner, ServerFileViewModel file, string diff, string command, string? backup)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = new SaveHostFileWindow();
        window.Fill(file, diff, command, backup);
        return window.ShowDialog<bool>(owner);
    }

    internal void Fill(ServerFileViewModel file, string diff, string command, string? backup)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(diff);
        Title = Strings.Format(Strings.HostFileSaveTitle, file.FileName, file.Host);
        Heading.Text = Title;
        Badge.Text = file.Environment;
        Badge.IsVisible = file.HasEnvironment;
        Badge.Classes.Set("danger", file.IsProduction);
        var changed = ChangedLines(diff);
        Summary.Text = changed == 1 ? Strings.Format(Strings.HostFileLineChanged, file.Path) : Strings.Format(Strings.HostFileLinesChanged, changed, file.Path);
        ShowDiff(diff);
        ProductionNote.IsVisible = file.IsProduction;
        ProductionText.Text = Strings.Format(Strings.HostFileProductionNote, file.Host);
        CommandNote.Text = Strings.Format(Strings.HostFileCommandNote, file.Host) + (file.NeedsSudo ? " " + Strings.HostFileSudoNote : string.Empty);
        Command.Text = command;
        BackupNote.Text = backup is null ? string.Empty : Strings.Format(Strings.HostFileBackupNote, backup);
        SaveButton.Content = Strings.Format(Strings.HostFileSaveOn, file.Host);
        SaveButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            Close(false);
        };
    }

    // Lines changed, counting a line replaced by another once.
    internal static int ChangedLines(string diff)
    {
        var lines = diff.Split('\n');
        var added = lines.Count(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal));
        var removed = lines.Count(line => line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal));
        return Math.Max(added, removed);
    }

    // The hunks without the file names above them, which the heading already gives.
    private void ShowDiff(string diff)
    {
        var body = string.Join('\n', diff.Split('\n').SkipWhile(line => line.StartsWith("---", StringComparison.Ordinal) || line.StartsWith("+++", StringComparison.Ordinal))).TrimEnd('\n');
        foreach (var (text, kind) in SyntaxHighlighter.Highlight(body, "diff", SyntaxHighlighter.MaxFileLength))
        {
            var run = new Run(text);
            if (kind != SyntaxKind.Plain && this.TryFindResource($"Syntax{kind}Brush", ActualThemeVariant, out var brush) && brush is IBrush foreground)
            {
                run.Foreground = foreground;
            }

            Diff.Inlines!.Add(run);
        }
    }
}
