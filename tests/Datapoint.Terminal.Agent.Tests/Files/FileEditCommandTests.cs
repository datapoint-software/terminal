using System.Diagnostics;
using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class FileEditCommandTests : IDisposable
{
    private static readonly DateTimeOffset s_time = new(2026, 11, 7, 15, 30, 12, TimeSpan.Zero);

    private readonly string _home = Directory.CreateTempSubdirectory("dpt-home-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public void Backs_up_then_writes_in_place()
    {
        var command = FileEditCommand.Build("/etc/nginx/nginx.conf", "~/.datapoint-terminal/staging/abc", "~/.datapoint-terminal/backups/etc/nginx/nginx.conf.20261107-153012", useSudo: false);

        Assert.Equal(
            "mkdir -p ~/'.datapoint-terminal/backups/etc/nginx' && cp -p -- '/etc/nginx/nginx.conf' ~/'.datapoint-terminal/backups/etc/nginx/nginx.conf.20261107-153012'"
            + " && cat -- ~/'.datapoint-terminal/staging/abc' > '/etc/nginx/nginx.conf' && rm -- ~/'.datapoint-terminal/staging/abc'",
            command);
    }

    [Fact]
    public void Uses_sudo_for_a_file_the_user_cannot_write()
    {
        var command = FileEditCommand.Build("/etc/hosts", "~/s", backup: null, useSudo: true);

        Assert.Equal("sudo tee -- '/etc/hosts' < ~/'s' > /dev/null && rm -- ~/'s'", command);
        Assert.Equal("sudo rm -- '/etc/hosts'", FileEditCommand.Undo("/etc/hosts", backup: null, useSudo: true));
    }

    [Theory]
    [InlineData("/etc/nginx/nginx.conf", "~/.datapoint-terminal/backups/etc/nginx/nginx.conf.20261107-153012")]
    [InlineData("~/.bashrc", "~/.datapoint-terminal/backups/home/.bashrc.20261107-153012")]
    public void Keeps_backups_by_full_path_and_time(string path, string expected)
    {
        Assert.Equal(expected, FileEditCommand.BackupPath(path, s_time));
    }

    [Fact]
    public async Task Applies_and_undoes_an_edit_in_a_real_shell()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var target = Path.Combine(_home, "app's conf", "app.conf");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(target, "port 80\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var files = new LocalHostFiles(_home);
        var staged = await files.StageAsync("port 443\n"u8.ToArray(), TestContext.Current.CancellationToken);
        var backup = FileEditCommand.BackupPath(target, s_time);

        Assert.Equal(0, Run(FileEditCommand.Build(target, staged, backup, useSudo: false)));

        Assert.Equal("port 443\n", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        var copy = Path.Combine(_home, ".datapoint-terminal", "backups", target.TrimStart('/') + ".20261107-153012");
        Assert.Equal("port 80\n", await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(copy));
        Assert.Empty(Directory.GetFiles(Path.Combine(_home, ".datapoint-terminal", "staging")));

        Assert.Equal(0, Run(FileEditCommand.Undo(target, backup, useSudo: false)));
        Assert.Equal("port 80\n", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(target));
    }

    private int Run(string command)
    {
        var start = new ProcessStartInfo("/bin/sh", ["-c", command]) { UseShellExecute = false };
        start.Environment["HOME"] = _home;
        using var process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
