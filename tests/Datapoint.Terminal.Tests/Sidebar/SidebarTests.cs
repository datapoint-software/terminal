using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Sidebar;

namespace Datapoint.Terminal.Tests.Sidebar;

public sealed class SidebarTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "sidebar-" + Guid.NewGuid().ToString("N"));

    public SidebarTests()
    {
        Write("skills/patch-and-reboot/SKILL.md", "---\nname: patch-and-reboot\n# not a title\n---\n\nSteps.\n");
        Write("skills/renew/SKILL.md", "---\nname: renew\n---\n\n# Renew certificates\n");
        Write("skills/notes/README.md", "Not a skill.");
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_workspace, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void Lists_skills_by_their_title_or_their_folder_in_words()
    {
        var sidebar = new SidebarViewModel(_workspace);

        sidebar.RefreshSkills();

        Assert.Equal<string>(["Patch and reboot", "Renew certificates"], [.. sidebar.Skills.Select(skill => skill.Name)]);
        Assert.True(sidebar.HasSkills);
    }

    [Theory]
    [InlineData("Acme Corp", "AC")]
    [InlineData("globex", "GL")]
    [InlineData("home-lab", "HL")]
    [InlineData("--", "?")]
    public void Gives_a_workspace_initials(string name, string expected) =>
        Assert.Equal(expected, WorkspaceLocation.Initials(name));

    [Fact]
    public void Names_the_service_a_workspace_is_shared_through()
    {
        var home = Path.Combine(Path.GetTempPath(), "home");

        Assert.Equal("OneDrive", WorkspaceLocation.Describe(Path.Combine(home, "Library", "CloudStorage", "OneDrive-Acme", "infra"), home));
        Assert.Equal("Dropbox", WorkspaceLocation.Describe(Path.Combine(home, "Dropbox", "infra"), home));
        Assert.Equal("~/Workspaces/globex", WorkspaceLocation.Describe(Path.Combine(home, "Workspaces", "globex"), home));
    }

    [Fact]
    public void Shows_hosts_their_environments_open_terminals_and_the_others_from_ssh_config()
    {
        var sidebar = new SidebarViewModel(_workspace);
        var chosen = new List<string>();
        var added = new List<string>();
        sidebar.HostChosen = connection => chosen.Add(connection.Name);
        sidebar.HostAdded = added.Add;

        sidebar.SetHosts(
            [
                new WorkspaceConnection { Name = "web1", SshHost = "web1", Environment = "production" },
                new WorkspaceConnection { Name = "db1", SshHost = "db1" },
                new WorkspaceConnection { Name = "stage", SshHost = "stage", Environment = "staging" },
            ],
            ["bastion", "nas"]);
        sidebar.SetOpenTerminals(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["WEB1"] = 2, ["stage"] = 1 });
        sidebar.ShowHost("db1");
        sidebar.ChooseHostCommand.Execute(sidebar.Hosts[0]);
        sidebar.OtherHosts[1].AddCommand.Execute(null);

        Assert.Equal("2 open", sidebar.Hosts[0].OpenText);
        Assert.Equal("1 open", sidebar.Hosts[2].OpenText);
        Assert.Equal("web1, production", sidebar.Hosts[0].AccessibleName);
        Assert.Equal((true, false, false), (sidebar.Hosts[0].IsProduction, sidebar.Hosts[0].IsStaging, sidebar.Hosts[0].HasNoEnvironment));
        Assert.True(sidebar.Hosts[1].HasNoEnvironment);
        Assert.True(sidebar.Hosts[2].IsStaging);
        Assert.False(sidebar.Hosts[1].IsOpen);
        Assert.Same(sidebar.Hosts[1], sidebar.SelectedHost);
        Assert.Equal("2 more in your SSH config", sidebar.OtherHostsText);
        Assert.Equal<string>(["web1"], [.. chosen]);
        Assert.Equal<string>(["nas"], [.. added]);
    }

    [AvaloniaFact]
    public void Renders_the_switcher_hosts_skills_and_instructions()
    {
        var sidebar = new SidebarViewModel(_workspace)
        {
            ListWorkspaces = () => [new WorkspaceSwitchItem("Acme Corp", "OneDrive · 3 hosts", _workspace, IsCurrent: true), new WorkspaceSwitchItem("Globex", "~/Workspaces/globex", "/tmp/globex", IsCurrent: false)],
            OpenWorkspaceRequested = () => { },
        };
        sidebar.SetWorkspace("Acme Corp", "OneDrive · 3 hosts");
        sidebar.SetHosts([new WorkspaceConnection { Name = "web1", SshHost = "web1", Environment = "production" }, new WorkspaceConnection { Name = "db1", SshHost = "db1" }, new WorkspaceConnection { Name = "backup", SshHost = "backup" }], ["bastion", "nas", "router"]);
        sidebar.SetOpenTerminals(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["web1"] = 1 });
        sidebar.RefreshSkills();
        sidebar.ShowHost("web1");
        var view = new SidebarView { DataContext = sidebar };
        var window = new Window { Width = 260, Height = 620, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Directory.CreateDirectory("rendered");
        using var frame = window.CaptureRenderedFrame();
        frame?.Save("rendered/sidebar.png", new PngBitmapEncoderOptions());

        var names = window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToList();
        Assert.Contains("AC", names, StringComparer.Ordinal);
        Assert.Contains("Renew certificates", names, StringComparer.Ordinal);
        Assert.Contains("Instructions", names, StringComparer.Ordinal);
        Assert.Contains("3 more in your SSH config", names, StringComparer.Ordinal);
        Assert.DoesNotContain("SKILL.md", names, StringComparer.Ordinal);

        var menu = view.BuildSwitcherMenu()!;
        Assert.Equal(7, menu.Items.Count);
        Assert.Single(Assert.IsType<Grid>(Assert.IsType<MenuItem>(menu.Items[0]).Header).Children.OfType<Avalonia.Controls.Shapes.Path>());
        Assert.Empty(Assert.IsType<Grid>(Assert.IsType<MenuItem>(menu.Items[1]).Header).Children.OfType<Avalonia.Controls.Shapes.Path>());
        Assert.False(Assert.IsType<MenuItem>(menu.Items[3]).IsEnabled);

        // Headless popups draw in the window's overlay, so the window is widened to show the whole menu.
        window.Width = 420;
        menu.ShowAt(view.SwitcherButton);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        using var menuFrame = window.CaptureRenderedFrame();
        menuFrame?.Save("rendered/sidebar-switcher.png", new PngBitmapEncoderOptions());
        menu.Hide();
    }

    [AvaloniaFact]
    public void Clicking_a_host_opens_its_documentation_and_never_a_terminal()
    {
        var sidebar = new SidebarViewModel(_workspace);
        var events = new List<string>();
        sidebar.HostChosen = connection => events.Add("docs " + connection.Name);
        sidebar.TerminalRequested = connection => events.Add("terminal " + connection.Name);
        sidebar.HostRemoved = connection => events.Add("removed " + connection.Name);
        sidebar.SetHosts([new WorkspaceConnection { Name = "web1", SshHost = "web1" }, new WorkspaceConnection { Name = "db1", SshHost = "db1" }], []);
        var window = new Window { Width = 260, Height = 620, Content = new SidebarView { DataContext = sidebar } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var row = window.GetVisualDescendants().OfType<TextBlock>().First(text => string.Equals(text.Text, "db1", StringComparison.Ordinal));
        var point = Avalonia.VisualExtensions.TranslatePoint(row, new Avalonia.Point(4, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        sidebar.RemoveHostCommand.Execute(sidebar.Hosts[0]);

        Assert.Equal("docs db1|removed web1", string.Join('|', events));
    }
}
