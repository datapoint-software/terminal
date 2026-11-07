using Datapoint.Terminal.Emulator.ShellIntegration;

namespace Datapoint.Terminal.Controls;

public sealed class CommandMarkEventArgs(CommandRecord command) : EventArgs
{
    public CommandRecord Command { get; } = command;
}
