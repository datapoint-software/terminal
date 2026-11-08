using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Tests.Chat;
using Datapoint.Terminal.Tests.Shell;
using Datapoint.Terminal.Themes;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class SettingsPagesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-settings-pages-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private SettingsViewModel Create()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        return new SettingsViewModel(store, store.Load(), new ProviderKeys(new MemoryCredentialStore(), _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
    }

    private static SettingsWindow Show(SettingsViewModel settings, int page)
    {
        var window = new SettingsWindow { DataContext = settings, History = new FakeHistory() };
        window.Show();
        window.ShowPage(page);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Lists_the_built_in_and_imported_themes_and_uses_the_chosen_one()
    {
        var settings = Create();
        var (theme, _) = ThemeImport.Read("One Dark.conf", OneDarkKitty);
        settings.SaveTheme(theme!);
        var window = Show(settings, page: 1);
        window.CaptureRenderedFrame()?.Save("rendered/settings-appearance.png", new PngBitmapEncoderOptions());

        var list = window.FindControl<AppearancePage>("AppearancePage")!.FindControl<ListBox>("ThemeList")!;
        var cards = list.Items.OfType<ThemeCard>().ToList();

        Assert.Equal(["Default", "Default dark", "Default light", "One Dark"], cards.Select(card => card.Name).ToList());
        Assert.Same(cards[3], list.SelectedItem);
        list.SelectedItem = cards[1];
        Assert.Null(settings.CustomThemeId);
        Assert.Equal(Datapoint.Terminal.Shell.TerminalTheme.Dark, settings.TerminalTheme);
    }

    [AvaloniaFact]
    public void Keeps_reduce_motion_when_turned_on_in_appearance()
    {
        var settings = Create();
        var window = Show(settings, page: 1);
        var toggle = window.FindControl<AppearancePage>("AppearancePage")!.GetLogicalDescendants().OfType<ToggleSwitch>()
            .Single(item => string.Equals(Avalonia.Automation.AutomationProperties.GetName(item), Datapoint.Terminal.Resources.Strings.AppearanceReduceMotion, StringComparison.Ordinal));

        toggle.IsChecked = true;

        Assert.True(settings.ReduceMotion);
        Assert.True(new SettingsStore(Path.Combine(_folder, "settings.json")).Load().ReduceMotion);
    }

    [AvaloniaFact]
    public void Chooses_the_window_theme_apart_from_the_terminal_theme()
    {
        var settings = Create();
        var window = Show(settings, page: 1);
        var choice = window.FindControl<AppearancePage>("AppearancePage")!.GetLogicalDescendants().OfType<ListBox>()
            .Single(item => string.Equals(Avalonia.Automation.AutomationProperties.GetName(item), Datapoint.Terminal.Resources.Strings.AppearanceWindowThemeName, StringComparison.Ordinal));

        choice.SelectedIndex = (int)WindowTheme.Light;

        Assert.Equal(WindowTheme.Light, settings.WindowTheme);
        var saved = new SettingsStore(Path.Combine(_folder, "settings.json")).Load();
        Assert.Equal(WindowTheme.Light, saved.WindowTheme);
        Assert.False(saved.MatchWindowToTheme);
    }

    [Theory]
    [InlineData(true, WindowTheme.Terminal)]
    [InlineData(false, WindowTheme.System)]
    public void Starts_the_window_theme_from_whether_the_window_matched_the_terminal(bool matched, WindowTheme expected)
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var settings = new SettingsViewModel(store, new AppSettings { MatchWindowToTheme = matched }, new ProviderKeys(new MemoryCredentialStore(), _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));

        Assert.Equal(expected, settings.WindowTheme);
    }

    [AvaloniaFact]
    public void Chooses_the_search_service_and_keeps_its_key_in_the_credential_store()
    {
        var settings = Create();
        var window = Show(settings, page: SettingsWindow.WebPageIndex);
        var model = (WebViewModel)window.FindControl<WebPage>("WebPage")!.DataContext!;

        model.SelectedService = 1;
        model.KeyDraft = "brave-key";
        model.SaveKeyCommand.Execute(null);
        model.AlwaysAllow = true;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/settings-web.png", new PngBitmapEncoderOptions());

        var saved = new SettingsStore(Path.Combine(_folder, "settings.json")).Load();
        Assert.Equal(WebSearchService.Brave, saved.WebSearch);
        Assert.True(saved.AlwaysAllowWeb);
        Assert.Equal("brave-key", settings.WebKeys.Read(WebSearchService.Brave));
        Assert.DoesNotContain("brave-key", File.ReadAllText(Path.Combine(_folder, "settings.json")), StringComparison.Ordinal);
        Assert.True(model.HasSavedKey);
    }

    [AvaloniaFact]
    public void Teaches_removes_and_refuses_read_only_commands_in_the_users_own_settings()
    {
        var settings = Create();
        settings.NoteApproved("vault status");
        settings.NoteApproved("docker swarm ca");
        var window = Show(settings, page: SettingsWindow.CommandsPageIndex);
        var model = (CommandsViewModel)window.FindControl<CommandsPage>("CommandsPage")!.DataContext!;

        model.Draft = "  patronictl   list ";
        model.AddCommand.Execute(null);
        model.Recent[1].ActCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/settings-commands.png", new PngBitmapEncoderOptions());

        Assert.Equal(["patronictl list", "vault status"], model.Learned.Select(entry => entry.Text), StringComparer.Ordinal);
        Assert.Equal(["docker swarm ca"], model.Recent.Select(entry => entry.Text), StringComparer.Ordinal);
        Assert.Equal(["patronictl list", "vault status"], new SettingsStore(Path.Combine(_folder, "settings.json")).Load().ReadOnlyCommands, StringComparer.Ordinal);
        Assert.Equal(Datapoint.Terminal.Agent.Safety.CommandRisk.ReadOnly, CommandRiskOf("vault status -format=json", settings));

        model.Draft = "bash -c ls";
        model.AddCommand.Execute(null);
        Assert.Equal(Datapoint.Terminal.Resources.Strings.Format(Datapoint.Terminal.Resources.Strings.CommandsCantTeach, "bash -c ls"), model.Status);
        model.Learned[0].ActCommand.Execute(null);
        Assert.Equal(["vault status"], settings.LearnedCommands.Commands, StringComparer.Ordinal);
    }

    private static Datapoint.Terminal.Agent.Safety.CommandRisk CommandRiskOf(string command, SettingsViewModel settings) =>
        Datapoint.Terminal.Agent.Safety.CommandClassifier.Classify(command, settings.LearnedCommands);

    [AvaloniaFact]
    public void Refuses_a_searxng_server_that_is_not_a_web_address()
    {
        var settings = Create();
        var model = new WebViewModel(settings) { SelectedService = 3 };

        model.Server = "searx.lan";
        model.SaveServerCommand.Execute(null);
        Assert.NotEmpty(model.ServerStatus);
        Assert.Null(settings.SearxngServer);
        model.Server = "http://searx.lan:8080";
        model.SaveServerCommand.Execute(null);

        Assert.Empty(model.ServerStatus);
        Assert.Equal("http://searx.lan:8080", settings.SearxngServer);
        Assert.Equal(WebSearchService.Searxng, settings.WebSearch);
    }

    [AvaloniaFact]
    public void Shows_what_each_connection_keeps_in_the_workspace()
    {
        var window = Show(Create(), page: 2);
        window.CaptureRenderedFrame()?.Save("rendered/settings-history.png", new PngBitmapEncoderOptions());

        var page = window.FindControl<HistoryPage>("HistoryPage")!;
        var texts = page.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();

        Assert.Contains("In shop", texts, StringComparer.Ordinal);
        Assert.Contains(texts, text => text?.StartsWith("42 conversations · 3", StringComparison.Ordinal) == true);
        Assert.Contains("db1", texts, StringComparer.Ordinal);
    }

    private const string OneDarkKitty = """
        background #282c34
        foreground #abb2bf
        cursor #528bff
        selection_background #3e4451
        color0 #282c34
        color1 #e06c75
        color2 #98c379
        color3 #e5c07b
        color4 #61afef
        color5 #c678dd
        color6 #56b6c2
        color7 #abb2bf
        color8 #5c6370
        color9 #e06c75
        color10 #98c379
        color11 #e5c07b
        color12 #61afef
        color13 #c678dd
        color14 #56b6c2
        color15 #ffffff
        """;
}
