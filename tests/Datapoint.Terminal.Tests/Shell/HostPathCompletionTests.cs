using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class HostPathCompletionTests
{
    private static readonly HostEntry[] Nginx =
    [
        new("nginx.conf", false),
        new("conf.d", true),
        new(".hidden", false),
        new("mime.types", false),
        new("sites-enabled", true),
    ];

    [Theory]
    [InlineData("/etc/nginx/ngi", "/etc/nginx/", "ngi")]
    [InlineData("/etc/nginx/", "/etc/nginx/", "")]
    [InlineData("~", "~/", "")]
    [InlineData("~/.pro", "~/", ".pro")]
    [InlineData("nginx", "", "nginx")]
    public void Splits_the_folder_from_the_name_being_typed(string path, string folder, string prefix) =>
        Assert.Equal((folder, prefix), HostPathCompletion.Split(path));

    [Fact]
    public void Lists_folders_first_and_hides_dot_files_until_a_dot_is_typed()
    {
        Assert.Equal(["conf.d", "sites-enabled", "mime.types", "nginx.conf"], HostPathCompletion.Matches(Nginx, string.Empty).Select(entry => entry.Name).ToList());
        Assert.Equal([".hidden"], HostPathCompletion.Matches(Nginx, ".").Select(entry => entry.Name).ToList());
        Assert.Equal(["nginx.conf"], HostPathCompletion.Matches(Nginx, "NG").Select(entry => entry.Name).ToList());
    }

    [Fact]
    public void Goes_into_folders_and_starts_where_the_terminal_is()
    {
        Assert.Equal("/etc/nginx/conf.d/", HostPathCompletion.Choose("/etc/nginx/", new HostEntry("conf.d", true)));
        Assert.Equal("/etc/nginx/nginx.conf", HostPathCompletion.Choose("/etc/nginx/", new HostEntry("nginx.conf", false)));
        Assert.Equal("/var/www/", HostPathCompletion.Start("/var/www"));
        Assert.Equal("~/", HostPathCompletion.Start(null));
    }
}
