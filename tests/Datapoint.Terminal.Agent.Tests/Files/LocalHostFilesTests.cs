using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class LocalHostFilesTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("dpt-home-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public async Task Reads_a_file_under_home_and_says_it_can_be_written()
    {
        await File.WriteAllTextAsync(Path.Combine(_home, ".profile"), "export A=1\n", Token);

        var file = await new LocalHostFiles(_home).ReadAsync("~/.profile", 100, Token);

        Assert.Equal((true, "export A=1\n", true, false), (file.Exists, System.Text.Encoding.UTF8.GetString(file.Content), file.IsWritable, file.IsTruncated));
    }

    [Fact]
    public async Task Stops_at_the_limit()
    {
        await File.WriteAllTextAsync(Path.Combine(_home, "big"), new string('x', 50), Token);

        var file = await new LocalHostFiles(_home).ReadAsync(Path.Combine(_home, "big"), 10, Token);

        Assert.Equal((10, true), (file.Content.Length, file.IsTruncated));
    }

    [Fact]
    public async Task Reports_a_missing_file_and_a_missing_folder()
    {
        var files = new LocalHostFiles(_home);

        var missing = await files.ReadAsync(Path.Combine(_home, "new.conf"), 10, Token);

        Assert.Equal((false, true), (missing.Exists, missing.IsWritable));
        await Assert.ThrowsAsync<IOException>(() => files.ReadAsync(Path.Combine(_home, "nowhere", "x"), 10, Token));
        await Assert.ThrowsAsync<IOException>(() => files.ReadAsync(_home, 10, Token));
    }

    [Fact]
    public async Task Says_when_a_file_needs_sudo()
    {
        if (OperatingSystem.IsWindows() || string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            return;
        }

        var path = Path.Combine(_home, "locked");
        await File.WriteAllTextAsync(path, "x", Token);
        File.SetUnixFileMode(path, UnixFileMode.UserRead);

        Assert.False((await new LocalHostFiles(_home).ReadAsync(path, 10, Token)).IsWritable);
    }

    [Fact]
    public async Task Stages_content_only_the_user_can_read()
    {
        var staged = await new LocalHostFiles(_home).StageAsync("new"u8.ToArray(), Token);

        Assert.StartsWith("~/.datapoint-terminal/staging/", staged, StringComparison.Ordinal);
        var path = Path.Combine(_home, staged[2..]);
        Assert.Equal("new", await File.ReadAllTextAsync(path, Token));
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
