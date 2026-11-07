using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class NewHostTests
{
    private static readonly ProjectConnection[] Existing = [new() { Name = "web1", Host = "web1.example.com" }];

    private static NewHostForm Form() => new(Existing, ["bastion", "nas"]);

    [Fact]
    public void Builds_a_connection_from_an_address()
    {
        var form = Form();
        form.Name = " db1 ";
        form.Address = "db1.example.com";
        form.User = "deploy";
        form.Port = "2222";
        form.EnvironmentIndex = 3;

        var (connection, problem) = form.Build();

        Assert.Null(problem);
        Assert.Equal(new ProjectConnection { Name = "db1", Host = "db1.example.com", User = "deploy", Port = 2222, Environment = "production" }, connection);
    }

    [Fact]
    public void Uses_an_ssh_config_alias_as_the_ssh_host()
    {
        var form = Form();
        form.UseAlias("bastion");

        var (connection, _) = form.Build();

        Assert.Equal(new ProjectConnection { Name = "bastion", SshHost = "bastion" }, connection);
    }

    [Fact]
    public void Keeps_a_typed_name_when_an_alias_is_chosen()
    {
        var form = Form();
        form.Name = "Jump box";
        form.UseAlias("bastion");

        Assert.Equal("Jump box", form.Name);
        Assert.Equal("bastion", form.Address);
    }

    [Theory]
    [InlineData("", "a.example.com", "", "", "name")]
    [InlineData("WEB1", "a.example.com", "", "", "already")]
    [InlineData("a", "-oProxyCommand=sh", "", "", "host name")]
    [InlineData("a", "deploy@a.example.com", "", "", "host name")]
    [InlineData("a", "a b", "", "", "host name")]
    [InlineData("a", "a.example.com", "root;id", "", "user name")]
    [InlineData("a", "a.example.com", "", "70000", "port")]
    [InlineData("a", "a.example.com", "", "-1", "port")]
    public void Says_what_to_correct(string name, string address, string user, string port, string expected)
    {
        var form = Form();
        form.Name = name;
        form.Address = address;
        form.User = user;
        form.Port = port;

        var (connection, problem) = form.Build();

        Assert.Null(connection);
        Assert.Contains(expected, problem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("[2001:db8::1]")]
    [InlineData("localhost")]
    public void Accepts_ip_addresses_and_short_names(string address)
    {
        var form = Form();
        form.Name = "box";
        form.Address = address;

        Assert.NotNull(form.Build().Connection);
    }

    [AvaloniaFact]
    public void Fills_in_an_alias_and_shows_the_production_note()
    {
        var window = new NewHostWindow(Form(), ["bastion", "nas"]);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var production = window.FindControl<TextBlock>("ProductionNote")!;
        Assert.False(production.IsVisible);

        window.FindControl<ListBox>("EnvironmentList")!.SelectedIndex = 3;
        var chip = window.FindControl<WrapPanel>("AliasPanel")!.Children.OfType<Button>().First();
        chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/new-host.png", new PngBitmapEncoderOptions());

        Assert.True(production.IsVisible);
        Assert.Equal("bastion", window.FindControl<TextBox>("NameBox")!.Text);
        Assert.Equal("bastion", window.FindControl<TextBox>("AddressBox")!.Text);
    }

    [AvaloniaFact]
    public void Shows_the_problem_instead_of_closing()
    {
        var window = new NewHostWindow(Form(), []);
        window.Show();
        window.FindControl<TextBox>("NameBox")!.Text = "web1";
        window.FindControl<TextBox>("AddressBox")!.Text = "web1.example.com";
        window.FindControl<Button>("AddButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(window.IsVisible);
        Assert.True(window.FindControl<TextBlock>("Problem")!.IsVisible);
        Assert.False(window.FindControl<WrapPanel>("AliasPanel")!.IsVisible);
    }
}
