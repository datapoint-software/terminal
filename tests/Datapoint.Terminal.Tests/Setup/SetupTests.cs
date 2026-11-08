using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Setup;
using Datapoint.Terminal.Sidebar;
using Datapoint.Terminal.Tests.Chat;
using Datapoint.Terminal.Tests.Settings;
using Datapoint.Terminal.Themes;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Setup;

// Product decisions 146 and 147.
public sealed class SetupTests : IDisposable
{
    private const string FakeKey = "sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-setup-").FullName;
    private readonly MemoryCredentialStore _store = new();
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string SettingsFile => Path.Combine(_folder, "settings.json");

    private SettingsViewModel Settings()
    {
        var store = new SettingsStore(SettingsFile);
        return new SettingsViewModel(store, store.Load(), new ProviderKeys(_store, _environment.GetValueOrDefault), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
    }

    private SetupViewModel Setup(SettingsViewModel settings) => new(settings, _environment.GetValueOrDefault);

    [Fact]
    public void Is_offered_once_to_an_installation_without_a_provider()
    {
        var settings = Settings();
        Assert.True(settings.NeedsSetup);

        Setup(settings).Close();

        Assert.False(Settings().NeedsSetup);
    }

    [Fact]
    public void Is_never_offered_to_an_installation_from_before_it_that_has_a_provider()
    {
        File.WriteAllText(SettingsFile, """{"providers":[{"id":"a1","kind":"Anthropic","name":"Anthropic","model":"claude-opus-5-5"}],"selectedProviderId":"a1"}""");

        var settings = Settings();

        Assert.False(settings.NeedsSetup);
        Assert.False(settings.TipsPending);
    }

    [Fact]
    public void Leaves_a_key_from_the_environment_to_setup_on_the_first_run()
    {
        var settings = new AppSettings();

        Assert.Same(settings, Datapoint.Terminal.Shell.MainWindow.FirstRun(settings));
    }

    [Fact]
    public void Keeps_the_key_in_the_credential_store_and_uses_the_provider()
    {
        var settings = Settings();
        var setup = Setup(settings);
        setup.NextCommand.Execute(null);
        setup.Key = FakeKey;
        setup.NextCommand.Execute(null);
        setup.NextCommand.Execute(null);

        Assert.True(setup.IsReady);
        Assert.Equal("Agent: Anthropic", setup.AgentSummary);
        setup.NextCommand.Execute(null);

        var provider = Assert.Single(settings.Providers);
        Assert.True(provider.IsActive);
        Assert.Equal(ProviderKind.Anthropic, provider.Kind.Kind);
        Assert.Equal(FakeKey, settings.Keys.Read(provider.ToAccount()));
        Assert.DoesNotContain("sk-ant", File.ReadAllText(SettingsFile), StringComparison.Ordinal);
        Assert.True(Settings().TipsPending);
    }

    [Fact]
    public void Asks_for_a_key_unless_the_environment_has_one()
    {
        var setup = Setup(Settings());
        setup.NextCommand.Execute(null);
        setup.NextCommand.Execute(null);

        Assert.True(setup.IsProvider);
        Assert.Equal("Enter an API key, or choose Set up later.", setup.Problem);

        _environment["ANTHROPIC_API_KEY"] = FakeKey;
        var withEnvironment = Setup(Settings());
        withEnvironment.NextCommand.Execute(null);
        Assert.StartsWith("Using ANTHROPIC_API_KEY from the environment.", withEnvironment.KeyNote, StringComparison.Ordinal);
        withEnvironment.NextCommand.Execute(null);

        Assert.True(withEnvironment.IsAppearance);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public void Takes_an_open_weight_model_by_its_server_and_name()
    {
        var settings = Settings();
        var setup = Setup(settings);
        setup.NextCommand.Execute(null);
        setup.Choice = setup.Choices[2];
        setup.Endpoint = "localhost:11434";
        setup.NextCommand.Execute(null);

        Assert.Equal("Enter the server's address, starting with http:// or https://, and a model.", setup.Problem);

        setup.Endpoint = "http://192.0.2.10:11434/v1";
        setup.Model = "open-model:30b";
        Assert.Contains("which you marked as your own", setup.Privacy, StringComparison.Ordinal);
        setup.NextCommand.Execute(null);
        setup.Close();

        var account = Assert.Single(settings.Providers).ToAccount();
        Assert.Equal((ProviderKind.OpenAICompatible, "http://192.0.2.10:11434/v1", "open-model:30b", true), (account.Kind, account.Endpoint, account.Model, account.IsLocal));
    }

    [Fact]
    public async Task Tests_the_key_before_it_is_saved()
    {
        var setup = Setup(Settings());
        setup.NextCommand.Execute(null);
        setup.Key = FakeKey;

        await setup.TestCommand.ExecuteAsync(null);

        Assert.Equal("Connected. The model answered.", setup.TestResult);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public void Sets_the_provider_aside_and_says_so()
    {
        var settings = Settings();
        var setup = Setup(settings);
        setup.NextCommand.Execute(null);
        setup.LaterCommand.Execute(null);

        Assert.True(setup.IsAppearance);
        setup.NextCommand.Execute(null);
        Assert.StartsWith("Agent: not set up yet.", setup.AgentSummary, StringComparison.Ordinal);
        setup.NextCommand.Execute(null);

        Assert.Empty(settings.Providers);
        Assert.False(settings.NeedsSetup);
    }

    [Fact]
    public void Applies_the_themes_as_they_are_chosen()
    {
        var settings = Settings();
        var setup = Setup(settings);

        setup.SelectedWindowTheme = (int)WindowTheme.Dark;
        setup.SelectedTerminalTheme = 2;

        Assert.Equal((WindowTheme.Dark, Datapoint.Terminal.Shell.TerminalTheme.Light), (settings.WindowTheme, settings.TerminalTheme));
        Assert.Equal("Window: dark", setup.WindowSummary);
        Assert.Equal("Terminal: light", setup.TerminalSummary);
    }

    [Fact]
    public void Starts_with_the_window_and_the_terminal_matching_the_system()
    {
        var settings = Settings();

        Assert.Equal((WindowTheme.System, Datapoint.Terminal.Shell.TerminalTheme.System), (settings.WindowTheme, settings.TerminalTheme));
        Assert.Equal(("Window: matches your system", "Terminal: matches your system"), (Setup(settings).WindowSummary, Setup(settings).TerminalSummary));
    }

    [AvaloniaFact]
    public void Shows_each_step_and_closes_with_escape_keeping_what_was_chosen()
    {
        var settings = Settings();
        var setup = Setup(settings);
        var window = new SetupWindow { DataContext = setup };
        window.Show();
        Directory.CreateDirectory("rendered");
        foreach (var name in new[] { "welcome", "provider", "appearance", "ready" })
        {
            Dispatcher.UIThread.RunJobs();
            using (var frame = window.CaptureRenderedFrame())
            {
                frame?.Save($"rendered/setup-{name}.png", new PngBitmapEncoderOptions());
            }

            Assert.Equal($"Step {Array.IndexOf(["welcome", "provider", "appearance", "ready"], name) + 1} of 4", window.FindControl<TextBlock>("StepText")!.Text);
            if (string.Equals(name, "provider", StringComparison.Ordinal))
            {
                Assert.True(window.FindControl<TextBox>("KeyBox")!.IsEffectivelyVisible);
                Assert.Equal('•', window.FindControl<TextBox>("KeyBox")!.PasswordChar);
                setup.Key = FakeKey;
            }

            if (!string.Equals(name, "ready", StringComparison.Ordinal))
            {
                setup.NextCommand.Execute(null);
            }
        }

        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        Assert.True(closed);
        Assert.Single(settings.Providers);
        Assert.False(settings.NeedsSetup);
    }

    [AvaloniaFact]
    public void Tours_the_sidebar_one_tip_at_a_time()
    {
        var (window, sidebar) = SidebarWindow();
        var done = 0;

        sidebar.ShowTips(() => done++);
        Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/sidebar-tips.png", new PngBitmapEncoderOptions());
        }

        Assert.Equal("Hosts", sidebar.CurrentTip);
        sidebar.NextTip();
        Assert.Equal("Skills", sidebar.CurrentTip);
        sidebar.NextTip();
        Assert.Equal("Workspace", sidebar.CurrentTip);
        sidebar.NextTip();

        Assert.Null(sidebar.CurrentTip);
        Assert.Equal(1, done);
        window.Close();
    }

    [AvaloniaFact]
    public void Ends_the_tips_with_escape()
    {
        var (window, sidebar) = SidebarWindow();
        var done = 0;
        sidebar.ShowTips(() => done++);
        Dispatcher.UIThread.RunJobs();

        var focused = window.FocusManager!.GetFocusedElement();
        Assert.IsType<Button>(focused);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(sidebar.CurrentTip);
        Assert.Equal(1, done);
        window.Close();
    }

    private (Window Window, SidebarView Sidebar) SidebarWindow()
    {
        var sidebar = new SidebarView { DataContext = new SidebarViewModel(_folder) };
        var window = new Window { Width = 900, Height = 600, Content = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*"), Children = { sidebar } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, sidebar);
    }
}
