using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Tests.Files;

public sealed class ShellTextTests
{
    [Theory]
    [InlineData("/etc/nginx/nginx.conf", "'/etc/nginx/nginx.conf'")]
    [InlineData("/srv/it's here", @"'/srv/it'\''s here'")]
    [InlineData("~/.bashrc", "~/'.bashrc'")]
    [InlineData("/tmp/$(reboot)", "'/tmp/$(reboot)'")]
    public void Quotes_paths_for_any_shell(string path, string expected)
    {
        Assert.Equal(expected, ShellText.QuotePath(path));
    }

    [Theory]
    [InlineData("etc/hosts")]
    [InlineData("~user/file")]
    [InlineData("/etc/a\nb")]
    [InlineData(@"/c\d")]
    [InlineData("/etc/")]
    [InlineData("")]
    public void Refuses_paths_the_file_tools_do_not_take(string path)
    {
        Assert.NotNull(ShellText.PathProblem(path));
    }

    [Fact]
    public void Quoted_text_reads_back_the_same_in_sh()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const string text = "it's a $HOME `test` \"here\"";
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/bin/sh", ["-c", "printf %s " + ShellText.Quote(text)]) { RedirectStandardOutput = true, UseShellExecute = false })!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(text, output);
    }
}
