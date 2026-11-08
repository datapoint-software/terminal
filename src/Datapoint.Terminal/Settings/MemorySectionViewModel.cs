using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// One memory file: the notes kept everywhere, or in one workspace.
public sealed partial class MemorySectionViewModel(Func<string> title, string path, bool canWrite, Func<string> clearMessage) : LocalizedObject
{
    public string Title => title();

    public string Path { get; } = path;

    public bool CanWrite { get; } = canWrite;

    public string ClearName => Strings.Format(Strings.MemoryClearSection, Title);

    public ObservableCollection<MemoryNoteViewModel> Notes { get; } = [];

    public bool HasNotes => Notes.Count > 0;

    public bool CanClear => CanWrite && HasNotes;

    [ObservableProperty]
    public partial string? Problem { get; set; }

    // Asks before clearing, with the window's own dialog.
    public Func<string, string, Task<bool>>? Confirm { get; set; }

    public void Reload()
    {
        Notes.Clear();
        foreach (var note in MemoryFile.Notes(Path))
        {
            Notes.Add(new MemoryNoteViewModel(note, this));
        }

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(CanClear));
    }

    // An empty replacement deletes the note.
    public void Replace(MemoryNoteViewModel note, string replacement)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(replacement);
        if (!CanWrite)
        {
            return;
        }

        Write(() => MemoryFile.Replace(Path, note.Text, replacement));
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!CanWrite || (Confirm is { } confirm && !await confirm(Strings.Format(Strings.MemoryClearTitle, Title), clearMessage()).ConfigureAwait(true)))
        {
            return;
        }

        Write(() =>
        {
            MemoryFile.Clear(Path);
            return WriteResult.Changed;
        });
    }

    private void Write(Func<WriteResult> change)
    {
        try
        {
            Problem = change() == WriteResult.Full ? Strings.MemoryFull : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Problem = exception.Message;
        }

        Reload();
    }
}
