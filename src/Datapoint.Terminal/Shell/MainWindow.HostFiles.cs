using System.Text;
using Avalonia.Controls;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Files on the machine a terminal runs on, opened from that terminal and saved through it: the new content is staged beside
// the terminal, and the command that writes it, keeping a backup, is typed where the user sees it and sudo can ask for a
// password, as the agent's approved edits are (product decision 98's "later", entry 93 of the autopilot log).
public sealed partial class MainWindow
{
    private static readonly TimeSpan s_saveCheckInterval = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan s_saveCheckTimeout = TimeSpan.FromMinutes(10);

    private readonly Dictionary<ServerFileViewModel, (ServerFileView View, TerminalTab Tab)> _hostFiles = [];

    private ServerFileView? ShownHostFile => DocumentTabs.SelectedItem is ServerFileViewModel file ? _hostFiles.GetValueOrDefault(file).View : null;

    private IEnumerable<ServerFileViewModel> UnsavedHostFiles => _hostFiles.Keys.Where(file => file.IsDirty);

    private async Task OpenHostFileAsync()
    {
        if (SelectedTab is not { AgentTerminal.Files: { } files } tab)
        {
            _chat.AddNotice(Strings.HostFileNoFiles);
            return;
        }

        var host = tab.ConnectionName ?? tab.Name;
        var start = HostPathCompletion.Start(tab.Connection?.Terminal.WorkingDirectory);
        var path = await OpenHostFileWindow.AskAsync(this, files, host, tab.Environment, start).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        // Names listed by the host end up in the command typed to save the file, quoted in a way that holds only for paths
        // without backslashes or line breaks.
        if (ShellText.PathProblem(path) is { } problem)
        {
            _chat.AddNotice(Strings.Format(Strings.HostFileCantOpen, path, problem));
            return;
        }

        if (_hostFiles.FirstOrDefault(item => ReferenceEquals(item.Value.Tab, tab) && string.Equals(item.Key.Path, path, StringComparison.Ordinal)).Key is { } open)
        {
            DocumentTabs.SelectedItem = open;
            return;
        }

        if (await ReadHostFileAsync(files, path).ConfigureAwait(true) is not { } file)
        {
            return;
        }

        var document = new ServerFileViewModel(host, tab.Environment, path, file.Content, needsSudo: !file.IsWritable)
        {
            CloseRequested = CloseHostFile,
            SaveRequested = saving => _ = SaveHostFileAsync(saving),
        };
        _hostFiles[document] = (new ServerFileView { DataContext = document }, tab);
        _documents.Add(document);
        DocumentTabs.SelectedItem = document;
    }

    // Null, with the reason in the chat, when the file can't be edited here.
    private async Task<HostFile?> ReadHostFileAsync(IHostFiles files, string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        try
        {
            var file = await files.ReadAsync(path, ServerFileViewModel.MaxBytes, CancellationToken.None).ConfigureAwait(true);
            var problem = file.Exists ? ServerFileViewModel.ProblemWith(file.Content, file.IsTruncated) : Strings.HostFileMissing;
            if (problem is null)
            {
                return file;
            }

            _chat.AddNotice(Strings.Format(Strings.HostFileCantOpen, name, problem));
        }
        catch (Exception exception) when (IsHostFileFailure(exception))
        {
            _chat.AddNotice(Strings.Format(Strings.HostFileCantOpen, name, exception.Message));
        }

        return null;
    }

    private static bool IsHostFileFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException
            or Sessions.Ssh.Channels.SshChannelException or Sessions.Ssh.SshDisconnectedException;

    private async Task SaveHostFileAsync(ServerFileViewModel file)
    {
        if (TerminalFor(file) is not { } tab)
        {
            return;
        }

        var files = tab.AgentTerminal!.Files!;
        HostFile now;
        try
        {
            now = await files.ReadAsync(file.Path, ServerFileViewModel.MaxBytes, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (IsHostFileFailure(exception))
        {
            file.Problem = exception.Message;
            return;
        }

        if ((!now.Exists || !now.Content.AsSpan().SequenceEqual(file.Content)) && !await KeepMineAsync(file, now).ConfigureAwait(true))
        {
            return;
        }

        var text = file.Text;
        var backup = now.Exists ? FileEditCommand.BackupPath(file.Path, DateTimeOffset.Now) : null;
        var useSudo = !now.IsWritable;
        var preview = FileEditCommand.Build(file.Path, HostFolders.Staging + "/…", backup, useSudo);
        var diff = TextDiff.Unified(file.Path, file.Saved, text);
        if (!await SaveHostFileWindow.AskAsync(this, file, diff, preview, backup).ConfigureAwait(true) || TerminalFor(file) is null)
        {
            return;
        }

        await WriteHostFileAsync(file, tab, text, backup, useSudo).ConfigureAwait(true);
    }

    // The terminal the file was opened from, when it's still open and at a prompt; otherwise the reason shows under the editor.
    private TerminalTab? TerminalFor(ServerFileViewModel file)
    {
        if (!_hostFiles.TryGetValue(file, out var entry) || !_tabs.Contains(entry.Tab) || entry.Tab is not { AgentTerminal.Files: not null, Connection: not null })
        {
            file.Problem = Strings.Format(Strings.HostFileNoTerminal, file.Host);
            return null;
        }

        var tab = entry.Tab;
        if (TabBusy.ForTyping(tab.AgentTerminal.Terminal.GetState(), tab.IsAgentActing) is { } busy)
        {
            file.Problem = Strings.Format(Strings.HostFileBusy, tab.Label, busy);
            return null;
        }

        return tab;
    }

    // The file changed on the host since it was opened: the user chooses whose version to keep.
    private async Task<bool> KeepMineAsync(ServerFileViewModel file, HostFile now)
    {
        var choice = await ChoiceWindow.AskAsync(
            this,
            Strings.Format(Strings.HostFileChangedTitle, file.FileName, file.Host),
            Strings.HostFileChangedMessage,
            Strings.Cancel,
            Strings.HostFileReload,
            Strings.HostFileSaveAnyway).ConfigureAwait(true);
        if (choice != 1)
        {
            return choice == 2;
        }

        if (!now.Exists || ServerFileViewModel.ProblemWith(now.Content, now.IsTruncated) is not null)
        {
            file.Problem = Strings.Format(Strings.HostFileCantOpen, file.FileName, now.Exists ? Strings.HostFileNotUtf8 : Strings.HostFileMissing);
            return false;
        }

        file.Reload(now.Content, needsSudo: !now.IsWritable);
        return false;
    }

    private async Task WriteHostFileAsync(ServerFileViewModel file, TerminalTab tab, string text, string? backup, bool useSudo)
    {
        var content = file.Encode(text);
        string staged;
        try
        {
            staged = await tab.AgentTerminal!.Files!.StageAsync(content, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (IsHostFileFailure(exception))
        {
            file.Problem = exception.Message;
            return;
        }

        file.IsSaving = true;
        tab.Connection!.Send(Encoding.UTF8.GetBytes(FileEditCommand.Build(file.Path, staged, backup, useSudo) + "\r"));
        if (await WaitUntilSavedAsync(file, tab, content).ConfigureAwait(true) is not { } saved)
        {
            file.IsSaving = false;
            file.Problem = Strings.Format(Strings.HostFileNotConfirmed, tab.Label);
            return;
        }

        file.MarkSaved(text, content, needsSudo: !saved.IsWritable);
        var undo = FileEditCommand.Undo(file.Path, backup, useSudo);
        _chat.AddNotice(backup is null
            ? Strings.Format(Strings.HostFileCreatedNotice, file.Path, file.Host)
            : Strings.Format(Strings.HostFileSavedNotice, file.Path, file.Host, backup, undo));
    }

    // The command runs in the terminal, maybe after sudo's password, so the file is read until it holds the new content. With
    // shell integration, a prompt after the command means it's done, saved or not; without it, only the time limit ends the wait.
    private async Task<HostFile?> WaitUntilSavedAsync(ServerFileViewModel file, TerminalTab tab, byte[] content)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < s_saveCheckTimeout && _hostFiles.ContainsKey(file) && _tabs.Contains(tab))
        {
            await Task.Delay(s_saveCheckInterval).ConfigureAwait(true);
            var state = tab.AgentTerminal?.Terminal.GetState();
            HostFile now;
            try
            {
                now = await tab.AgentTerminal!.Files!.ReadAsync(file.Path, ServerFileViewModel.MaxBytes, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception) when (IsHostFileFailure(exception))
            {
                return null;
            }

            if (now.Exists && now.Content.AsSpan().SequenceEqual(content))
            {
                return now;
            }

            if (DateTimeOffset.UtcNow - started > TimeSpan.FromSeconds(2) && state is { HasShellIntegration: true, IsAtPrompt: true })
            {
                return null;
            }
        }

        return null;
    }

    private void CloseHostFile(ServerFileViewModel file) => _ = CloseHostFileAsync(file);

    private async Task CloseHostFileAsync(ServerFileViewModel file)
    {
        if (file.IsDirty)
        {
            DocumentTabs.SelectedItem = file;
            var choice = await ChoiceWindow.AskAsync(
                this,
                Strings.Format(Strings.HostFileUnsavedTitle, file.FileName),
                Strings.Format(Strings.HostFileUnsavedMessage, file.Path, file.Host),
                Strings.Cancel,
                Strings.HostFileDiscard).ConfigureAwait(true);
            if (choice != 1)
            {
                return;
            }
        }

        _hostFiles.Remove(file);
        RemoveFromStrip(file);
    }
}
