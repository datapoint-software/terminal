using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// One note in memory, edited in place.
public sealed partial class MemoryNoteViewModel(string text, MemorySectionViewModel section) : ObservableObject
{
    public string Text { get; } = text;

    public bool CanWrite => section.CanWrite;

    public string EditName => Strings.Format(Strings.MemoryEditNote, Text);

    public string DeleteName => Strings.Format(Strings.MemoryDeleteNote, Text);

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string EditText { get; set; } = string.Empty;

    [RelayCommand]
    private void Edit()
    {
        EditText = Text;
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        IsEditing = false;
        section.Replace(this, EditText);
    }

    [RelayCommand]
    private void Cancel() => IsEditing = false;

    [RelayCommand]
    private void Delete() => section.Replace(this, string.Empty);
}
