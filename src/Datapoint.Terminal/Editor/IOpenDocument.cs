using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Datapoint.Terminal.Editor;

// What the document tab strip shows for each open file, a workspace document or a file on a host.
public interface IOpenDocument : INotifyPropertyChanged
{
    string Name { get; }

    string ToolTip { get; }

    string AccessibleName { get; }

    string CloseName { get; }

    // Shown as a dot in the tab: only files that wait for the user to save them, not ones saved as the user types.
    bool IsUnsaved { get; }

    IRelayCommand CloseCommand { get; }
}
