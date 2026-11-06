using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Controls;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;

namespace Datapoint.Terminal.Shell;

public sealed partial class TerminalTab : ObservableObject
{
    private readonly Action<TerminalTab> _close;

    public TerminalTab(string name, TerminalView view, Action<TerminalTab> close)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(close);
        Name = name;
        View = view;
        _close = close;
    }

    public string Name { get; }

    public TerminalView View { get; }

    public TerminalConnection? Connection { get; set; }

    public AgentTerminal? AgentTerminal { get; set; }

    public string? Environment => AgentTerminal?.Environment;

    public bool HasEnvironment => Environment is not null;

    [ObservableProperty]
    public partial string ShellTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAgentActing), nameof(AgentNeedsUser), nameof(AccessibleName))]
    public partial TabActivity Activity { get; set; }

    public bool IsAgentActing => Activity == TabActivity.AgentActing;

    public bool AgentNeedsUser => Activity == TabActivity.AgentNeedsUser;

    public string AccessibleName => Activity switch
    {
        TabActivity.AgentActing => Strings.Format(Strings.TabAgentActing, Label),
        TabActivity.AgentNeedsUser => Strings.Format(Strings.TabAgentNeedsUser, Label),
        _ => Label,
    };

    public string Label => Environment is null ? Name : $"{Name}, {Environment}";

    public string CloseName => Strings.Format(Strings.TabClose, Name);

    public string ToolTip => string.IsNullOrWhiteSpace(ShellTitle) ? Name : ShellTitle;

    partial void OnShellTitleChanged(string value) => OnPropertyChanged(nameof(ToolTip));

    [RelayCommand]
    private void Close() => _close(this);
}
