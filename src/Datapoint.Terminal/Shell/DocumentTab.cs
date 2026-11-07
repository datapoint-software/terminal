using Avalonia.Threading;
using Datapoint.Terminal.Editor;

namespace Datapoint.Terminal.Shell;

// An open document: its view, a save shortly after the user stops typing, and a watch on the file for changes made elsewhere,
// e.g. by the agent.
public sealed class DocumentTab : IDisposable
{
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private FileSystemWatcher? _watcher;

    public DocumentTab(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;
        View = new DocumentView { DataContext = document };
        document.SaveDue = () =>
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        };
        _saveTimer.Tick += (_, _) => SaveNow();
        Watch();
    }

    public DocumentViewModel Document { get; }

    public DocumentView View { get; }

    // False when the save couldn't happen, e.g. the file changed on disk and the user hasn't chosen a version yet.
    public bool SaveNow()
    {
        _saveTimer.Stop();
        var saved = Document.Save();
        if (saved && _watcher is null)
        {
            // A new file's folder may have only now been made.
            Watch();
        }

        return saved;
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _watcher?.Dispose();
        _watcher = null;
    }

    private void Watch()
    {
        var folder = Path.GetDirectoryName(Document.Path);
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(folder, Path.GetFileName(Document.Path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            FileSystemEventHandler changed = (_, _) => Dispatcher.UIThread.Post(Document.OnChangedOnDisk);
            _watcher.Changed += changed;
            _watcher.Created += changed;
            _watcher.Deleted += changed;
            _watcher.Renamed += (_, _) => Dispatcher.UIThread.Post(Document.OnChangedOnDisk);
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Without a watcher, changes made elsewhere still show as a conflict when the user next saves.
            _watcher?.Dispose();
            _watcher = null;
        }
    }
}
