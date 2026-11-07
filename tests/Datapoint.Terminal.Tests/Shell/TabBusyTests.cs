using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class TabBusyTests
{
    private static TerminalState State(bool integration = true, bool atPrompt = true, bool fullScreen = false) =>
        new(integration, atPrompt, string.Empty, fullScreen, WorkingDirectory: null, NextOutputLine: 0);

    [Fact]
    public void An_idle_local_shell_closes_without_asking() =>
        Assert.Null(TabBusy.Reason(State(), isRemote: false, isAgentActing: false));

    [Fact]
    public void A_local_shell_without_integration_closes_without_asking() =>
        Assert.Null(TabBusy.Reason(State(integration: false, atPrompt: false), isRemote: false, isAgentActing: false));

    [Fact]
    public void A_running_command_asks() =>
        Assert.Equal(Strings.BusyCommand, TabBusy.Reason(State(atPrompt: false), isRemote: false, isAgentActing: false));

    [Fact]
    public void A_full_screen_program_asks_even_without_integration() =>
        Assert.Equal(Strings.BusyFullScreen, TabBusy.Reason(State(integration: false, fullScreen: true), isRemote: false, isAgentActing: false));

    [Fact]
    public void A_remote_tab_always_asks() =>
        Assert.Equal(Strings.BusyRemote, TabBusy.Reason(State(integration: false), isRemote: true, isAgentActing: false));

    [Fact]
    public void The_agent_at_work_is_named_first() =>
        Assert.Equal(Strings.BusyAgent, TabBusy.Reason(State(atPrompt: false, fullScreen: true), isRemote: true, isAgentActing: true));
}
