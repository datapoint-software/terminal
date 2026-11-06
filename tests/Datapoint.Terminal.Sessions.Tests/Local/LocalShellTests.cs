using System.Text;
using Datapoint.Terminal.Emulator.ShellIntegration;
using Datapoint.Terminal.Sessions.Local;
using Datapoint.Terminal.Sessions.ShellIntegration;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Sessions.Tests.Local;

public sealed class LocalShellTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-home-").FullName;

    public static TheoryData<string> Shells => ["/bin/bash", "/bin/zsh", "/usr/bin/fish", "/opt/homebrew/bin/fish", "/usr/local/bin/pwsh", "/opt/homebrew/bin/pwsh"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private async Task<(LocalSession Session, EmulatorTerminal Terminal, Task Pump)> StartAsync(string shell)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(shell))
        {
            Assert.Skip($"{shell} isn't installed here.");
        }

        var session = await LocalSession.StartAsync(
            new LocalSessionOptions
            {
                Shell = shell,
                Login = false,
                WorkingDirectory = _home,
                Columns = 120,
                Rows = 30,
                Environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["HOME"] = _home,
                    ["DATAPOINT_USER_ZDOTDIR"] = _home,
                    ["PS1"] = "$ ",
                },
            },
            Token);
        var terminal = new EmulatorTerminal(120, 30) { ShellIntegrationNonce = session.ShellIntegrationNonce };
        var pump = Task.Run(
            async () =>
            {
                var buffer = new byte[16 * 1024];
                int count;
                while ((count = await session.ReadAsync(buffer, Token)) > 0)
                {
                    terminal.Feed(buffer.AsSpan(0, count));
                }
            },
            Token);
        return (session, terminal, pump);
    }

    private static async Task WaitForAsync(EmulatorTerminal terminal, Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            lock (terminal.SyncRoot)
            {
                if (condition())
                {
                    return;
                }
            }

            await Task.Delay(50, Token);
        }

        Assert.Fail($"Timed out waiting for {what}. Screen:\n{terminal.GetVisibleText()}");
    }

    private static CommandRecord? Finished(EmulatorTerminal terminal, string commandLine) =>
        terminal.Commands.LastOrDefault(command => command.IsFinished && string.Equals(command.CommandLine, commandLine, StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task Records_commands_with_exit_codes(string shell)
    {
        var (session, terminal, _) = await StartAsync(shell);
        await using (session)
        {
            await WaitForAsync(terminal, () => terminal.IsAtPrompt, "the first prompt");

            await session.WriteAsync(Encoding.UTF8.GetBytes("echo hello\r"), Token);
            await WaitForAsync(terminal, () => Finished(terminal, "echo hello") is not null, "echo to finish");
            await session.WriteAsync(Encoding.UTF8.GetBytes("ls /nonexistent-folder\r"), Token);
            await WaitForAsync(terminal, () => Finished(terminal, "ls /nonexistent-folder") is not null, "ls to finish");

            var echo = Finished(terminal, "echo hello")!;
            Assert.Equal(0, echo.ExitCode);
            Assert.True(echo.CommandLineIsTrusted);
            Assert.NotEqual(0, Finished(terminal, "ls /nonexistent-folder")!.ExitCode);
        }
    }

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task Reports_the_working_folder(string shell)
    {
        var (session, terminal, _) = await StartAsync(shell);
        await using (session)
        {
            await WaitForAsync(terminal, () => terminal.IsAtPrompt, "the first prompt");
            var folder = Directory.CreateDirectory(Path.Combine(_home, "with space")).FullName;

            await session.WriteAsync(Encoding.UTF8.GetBytes("cd 'with space'\r"), Token);

            await WaitForAsync(terminal, () => terminal.WorkingDirectory?.EndsWith("/with space", StringComparison.Ordinal) == true, "the new folder");
            Assert.EndsWith(Path.GetFileName(folder), terminal.WorkingDirectory, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Shells))]
    public async Task Keeps_a_command_line_with_separators_intact(string shell)
    {
        var (session, terminal, _) = await StartAsync(shell);
        await using (session)
        {
            await WaitForAsync(terminal, () => terminal.IsAtPrompt, "the first prompt");

            await session.WriteAsync(Encoding.UTF8.GetBytes(@"echo 'a;b' \\ c" + "\r"), Token);

            await WaitForAsync(terminal, () => Finished(terminal, @"echo 'a;b' \\ c") is not null, "the command to finish");
        }
    }

    [Fact]
    public async Task Hides_the_nonce_from_programs_the_shell_runs()
    {
        var (session, terminal, _) = await StartAsync("/bin/bash");
        await using (session)
        {
            await WaitForAsync(terminal, () => terminal.IsAtPrompt, "the first prompt");

            await session.WriteAsync(Encoding.UTF8.GetBytes("echo \"nonce=[$DATAPOINT_NONCE]\"\r"), Token);

            await WaitForAsync(terminal, () => terminal.GetVisibleText().Contains("nonce=[]", StringComparison.Ordinal), "the echo");
        }
    }

    [Fact]
    public void Recognizes_shells_by_name()
    {
        Assert.Equal(ShellKind.Bash, ShellIntegrationInstaller.KindOf("/usr/local/bin/bash"));
        Assert.Equal(ShellKind.PowerShell, ShellIntegrationInstaller.KindOf(@"C:\Program Files\PowerShell\7\pwsh.exe"));
        Assert.Equal(ShellKind.Other, ShellIntegrationInstaller.KindOf("/bin/sh"));
    }
}
