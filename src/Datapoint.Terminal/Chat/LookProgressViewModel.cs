using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// Product decision 111: what the agent does while it looks, gathered by host on one card instead of a line per command, so
// the user can see at a glance what it read where. It ends when the agent proposes its plan or the task ends.
public sealed partial class LookProgressViewModel(AgentLook look) : LocalizedObject
{
    public IReadOnlyList<LookProgressHostViewModel> Hosts { get; } = [.. look.Hosts.Select((host, index) => new LookProgressHostViewModel(host, index == 0))];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsOver { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial int CommandCount { get; private set; }

    public string Title => IsOver
        ? Strings.Format(CommandCount == 1 ? Strings.LookDoneOne : Strings.LookDone, HostCount(), CommandCount)
        : Strings.Format(Strings.LookRunning, HostCount());

    // False when the command ran somewhere the look doesn't cover, which the chat then shows on its own.
    public bool Start(string terminal, string command)
    {
        if (IsOver || Hosts.FirstOrDefault(host => host.Owns(terminal)) is not { } host)
        {
            return false;
        }

        host.State = null;
        host.Commands.Add(new LookCommandViewModel(command));
        CommandCount++;
        return true;
    }

    // False when the look is over or doesn't cover the host, so the chat says it on a line of its own.
    public bool Note(string host, string? state)
    {
        if (IsOver || Hosts.FirstOrDefault(item => item.Owns(host)) is not { } item)
        {
            return false;
        }

        item.State = state;
        return true;
    }

    public bool Covers(string host) => !IsOver && Hosts.Any(item => item.Owns(host));

    public bool Finish(string terminal, string command, int? exitCode)
    {
        if (Hosts.FirstOrDefault(host => host.Owns(terminal)) is not { } host
            || host.Commands.LastOrDefault(item => item.IsRunning && string.Equals(item.Command, command, StringComparison.Ordinal)) is not { } item)
        {
            return false;
        }

        item.IsFailed = exitCode is not null and not 0;
        item.IsRunning = false;
        return true;
    }

    public void End()
    {
        foreach (var item in Hosts.SelectMany(host => host.Commands).Where(item => item.IsRunning))
        {
            item.IsRunning = false;
        }

        foreach (var host in Hosts)
        {
            host.State = null;
        }

        IsOver = true;
    }

    private string HostCount() => Hosts.Count == 1 ? Strings.LookOneHost : Strings.Format(Strings.LookHosts, Hosts.Count);
}
