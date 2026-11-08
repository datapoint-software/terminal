using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// One command on the Commands page, with the one thing that can be done to it there: remove it, or teach it.
public sealed partial class CommandEntryViewModel(string text, bool isFirst, Action<string> act)
{
    public string Text { get; } = text;

    // The rows' hairlines go between them, not above the first.
    public bool IsFirst { get; } = isFirst;

    public string RemoveName => Strings.Format(Strings.CommandsRemoveName, Text);

    public string TeachName => Strings.Format(Strings.CommandsTeachName, Text);

    [RelayCommand]
    private void Act() => act(Text);
}
