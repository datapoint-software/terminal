using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// Settings › Memory (product decision 102): what the agent remembers about the user, everywhere and in the workspace in front.
public sealed partial class MemoryPageViewModel : ObservableObject
{
    public ObservableCollection<MemorySectionViewModel> Sections { get; } = [];

    public Func<string, string, Task<bool>>? Confirm { get; set; }

    public void Load(MemoryPlaces? places)
    {
        Sections.Clear();
        if (places is null)
        {
            return;
        }

        Add(new MemorySectionViewModel(Strings.MemoryEverywhere, places.GlobalPath, canWrite: true, Strings.MemoryClearEverywhere));
        if (places is not { WorkspacePath: { } path, WorkspaceName: { } name })
        {
            return;
        }

        Add(new MemorySectionViewModel(Strings.Format(Strings.MemoryInWorkspace, name), path, places.CanWriteWorkspace, Strings.ClearMemoryMessage));
    }

    // Notes the agent added while the page was open.
    public void Reload()
    {
        foreach (var section in Sections)
        {
            section.Reload();
        }
    }

    private void Add(MemorySectionViewModel section)
    {
        section.Confirm = (title, message) => Confirm?.Invoke(title, message) ?? Task.FromResult(false);
        section.Reload();
        Sections.Add(section);
    }
}
