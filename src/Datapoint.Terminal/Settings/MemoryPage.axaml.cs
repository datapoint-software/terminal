using Avalonia.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Settings;

public sealed partial class MemoryPage : UserControl
{
    public MemoryPage()
    {
        InitializeComponent();
        Model.Confirm = ConfirmAsync;
        DataContext = Model;
    }

    public MemoryPageViewModel Model { get; } = new();

    private async Task<bool> ConfirmAsync(string title, string message) =>
        TopLevel.GetTopLevel(this) is Window owner
        && await ChoiceWindow.AskAsync(owner, title, message, details: null, hasDefault: false, Strings.Cancel, Strings.MemoryClearConfirm).ConfigureAwait(true) == 1;
}
