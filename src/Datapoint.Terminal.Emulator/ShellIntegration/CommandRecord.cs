namespace Datapoint.Terminal.Emulator.ShellIntegration;

public sealed class CommandRecord
{
    public long PromptLine { get; internal set; }

    public long? InputLine { get; internal set; }

    public int InputColumn { get; internal set; }

    public long? OutputLine { get; internal set; }

    public long? EndLine { get; internal set; }

    public string? CommandLine { get; internal set; }

    public bool CommandLineIsTrusted { get; internal set; }

    public int? ExitCode { get; internal set; }

    public string? WorkingDirectory { get; internal set; }

    public DateTimeOffset? StartedAt { get; internal set; }

    public DateTimeOffset? FinishedAt { get; internal set; }

    public CommandSource Source { get; internal set; }

    public bool IsRunning => OutputLine is not null && EndLine is null;

    public bool IsFinished => EndLine is not null;
}
