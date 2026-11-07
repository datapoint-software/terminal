using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Sidebar;

namespace Datapoint.Terminal.Tests.Sidebar;

public sealed class SidebarTests : IDisposable
{
    private readonly string _project = Path.Combine(Path.GetTempPath(), "sidebar-" + Guid.NewGuid().ToString("N"));

    public SidebarTests()
    {
        foreach (var folder in new[] { "hosts", "runbooks", ".terminal", ".git", "src/.git" })
        {
            Directory.CreateDirectory(Path.Combine(_project, folder));
        }

        foreach (var file in new[] { "hosts/web1.md", "hosts/db1.md", "runbooks/renew.md", "notes.md", "CLAUDE.md", ".gitignore", ".terminal.json", "deploy.sh", ".terminal/MEMORY.md", "src/.terminal" })
        {
            File.WriteAllText(Path.Combine(_project, file), string.Empty);
        }
    }

    private string HostsFolder => Path.Combine(_project, "hosts");

    public void Dispose() => Directory.Delete(_project, recursive: true);

    [Fact]
    public void Lists_folders_first_and_hides_only_the_personal_folder_and_git()
    {
        var top = ProjectListing.List(_project, _project);
        var inside = ProjectListing.List(Path.Combine(_project, "src"), _project);

        Assert.Equal<string>(["hosts", "runbooks", "src", ".gitignore", ".terminal.json", "CLAUDE.md", "deploy.sh", "notes.md"], [.. top.Select(entry => entry.Name)]);
        Assert.True(top.Single(entry => string.Equals(entry.Name, ".gitignore", StringComparison.Ordinal)).IsQuiet);
        Assert.Equal<string>([".terminal"], [.. inside.Select(entry => entry.Name)]);
    }

    [Fact]
    public void Lists_a_folder_once_opened_and_keeps_what_was_open_on_refresh()
    {
        var sidebar = new SidebarViewModel(_project, HostsFolder);
        sidebar.RefreshFiles();
        var hosts = sidebar.Files.Single(node => string.Equals(node.Name, "hosts", StringComparison.Ordinal));
        Assert.Single(hosts.Children);
        Assert.Equal(string.Empty, hosts.Children[0].Name);

        hosts.IsExpanded = true;
        Assert.Equal<string>(["db1.md", "web1.md"], [.. hosts.Children.Select(node => node.Name)]);
        Assert.All(hosts.Children, node => Assert.True(node.IsHostDocument));
        Assert.True(sidebar.Files.Single(node => string.Equals(node.Name, "notes.md", StringComparison.Ordinal)).IsDocument);
        Assert.True(sidebar.Files.Single(node => string.Equals(node.Name, "deploy.sh", StringComparison.Ordinal)).IsOtherFile);

        File.WriteAllText(Path.Combine(_project, "hosts", "backup.md"), string.Empty);
        File.Delete(Path.Combine(_project, "notes.md"));
        sidebar.RefreshFiles();

        Assert.Same(hosts, sidebar.Files.Single(node => string.Equals(node.Name, "hosts", StringComparison.Ordinal)));
        Assert.True(hosts.IsExpanded);
        Assert.Equal<string>(["backup.md", "db1.md", "web1.md"], [.. hosts.Children.Select(node => node.Name)]);
        Assert.DoesNotContain(sidebar.Files, node => string.Equals(node.Name, "notes.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Shows_hosts_their_open_terminals_and_the_others_from_ssh_config()
    {
        var sidebar = new SidebarViewModel(_project, HostsFolder);
        var chosen = new List<string>();
        var added = new List<string>();
        sidebar.HostChosen = connection => chosen.Add(connection.Name);
        sidebar.HostAdded = added.Add;

        sidebar.SetHosts([new ProjectConnection { Name = "web1", SshHost = "web1", Environment = "production" }, new ProjectConnection { Name = "db1", SshHost = "db1" }], ["bastion", "nas"]);
        sidebar.SetOpenTerminals(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["WEB1"] = 2 });
        sidebar.ShowHost("db1");
        sidebar.ChooseHostCommand.Execute(sidebar.Hosts[0]);
        sidebar.OtherHosts[1].AddCommand.Execute(null);

        Assert.Equal("2 open", sidebar.Hosts[0].OpenText);
        Assert.Equal("web1, production", sidebar.Hosts[0].AccessibleName);
        Assert.False(sidebar.Hosts[1].IsOpen);
        Assert.Same(sidebar.Hosts[1], sidebar.SelectedHost);
        Assert.Equal("2 more in your SSH config", sidebar.OtherHostsText);
        Assert.Equal<string>(["web1"], [.. chosen]);
        Assert.Equal<string>(["nas"], [.. added]);
    }

    [AvaloniaFact]
    public void Renders_hosts_and_files()
    {
        var sidebar = new SidebarViewModel(_project, HostsFolder);
        sidebar.SetHosts([new ProjectConnection { Name = "web1", SshHost = "web1" }, new ProjectConnection { Name = "db1", SshHost = "db1" }, new ProjectConnection { Name = "backup", SshHost = "backup" }], ["bastion", "nas", "router"]);
        sidebar.SetOpenTerminals(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["web1"] = 1 });
        sidebar.RefreshFiles();
        sidebar.Files.Single(node => string.Equals(node.Name, "hosts", StringComparison.Ordinal)).IsExpanded = true;
        sidebar.ShowHost("web1");
        var window = new Window { Width = 240, Height = 620, Content = new SidebarView { DataContext = sidebar } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Directory.CreateDirectory("rendered");
        using var frame = window.CaptureRenderedFrame();
        frame?.Save("rendered/sidebar.png", new PngBitmapEncoderOptions());

        var names = window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToList();
        Assert.Contains("web1.md", names, StringComparer.Ordinal);
        Assert.Contains("3 more in your SSH config", names, StringComparer.Ordinal);
        Assert.DoesNotContain(".terminal", names, StringComparer.Ordinal);
    }
}
