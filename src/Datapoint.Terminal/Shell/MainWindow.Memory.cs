using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decisions 29 and 88: memory is plain files the user can read, edit and clear, and every change is shown in the chat.
public sealed partial class MainWindow
{
    // A read-only window would race the window that holds the project's lock, so only the global memory is written from it.
    private AgentMemory CreateMemory() => new(GlobalMemoryPath, Project.IsReadOnly ? null : ProjectMemoryPath);

    private void AddMemoryItems(NativeMenu file)
    {
        file.Add(Item(Strings.MenuOpenGlobalMemory, null, () => OpenMemory(GlobalMemoryPath)));
        if (ProjectMemoryPath is not { } project)
        {
            return;
        }

        file.Add(Item(Strings.MenuOpenProjectMemory, null, () => OpenMemory(project)));
        var clear = Item(Strings.MenuClearProjectMemory, null, () => _ = ClearMemoryAsync(project));
        clear.IsEnabled = !Project.IsReadOnly;
        file.Add(clear);
    }

    private void OpenMemory(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                MemoryFile.Create(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeMemoryUnwritable, ProjectFolders.Display(path), exception.Message));
            return;
        }

        _ = Launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    private async Task ClearMemoryAsync(string path)
    {
        var answer = await ChoiceWindow.AskAsync(
            this,
            Strings.ClearMemoryTitle,
            Strings.ClearMemoryMessage,
            details: null,
            hasDefault: false,
            Strings.Cancel,
            Strings.ClearMemoryConfirm).ConfigureAwait(true);
        if (answer != 1)
        {
            return;
        }

        try
        {
            MemoryFile.Clear(path);
            _chat.AddNotice(Strings.NoticeMemoryCleared);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeMemoryUnwritable, ProjectFolders.Display(path), exception.Message));
        }
    }

    private void OnMemoryEvent(object? sender, AgentEventArgs e)
    {
        if (e.Kind != AgentEventKind.MemoryChanged || e.Path is not { } path)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _chat.AddNotice(e.Text ?? string.Empty, Strings.MemoryOpen, () => OpenMemory(path)));
    }
}
