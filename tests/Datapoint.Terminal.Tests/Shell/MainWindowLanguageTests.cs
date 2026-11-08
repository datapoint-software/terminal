using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Tests.Chat;
using Datapoint.Terminal.Tests.Resources;
using Datapoint.Terminal.Tests.Settings;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Shell;

// Product decision 150.
[Collection(nameof(LanguageSwitching))]
public sealed class MainWindowLanguageTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-main-language-").FullName;

    public void Dispose()
    {
        Localizer.Instance.Use(AppLanguage.English);
        Directory.Delete(_folder, recursive: true);
    }

    [AvaloniaFact]
    public void Builds_the_menu_again_in_the_new_language()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var settings = new SettingsViewModel(store, store.Load(), new ProviderKeys(new MemoryCredentialStore(), _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        var workspace = Workspace.Open(Path.Combine(_folder, "workspace"), _folder, create: true, Path.Combine(_folder, "personal"));
        var window = new MainWindow(workspace, new WorkspaceWindows(lifetime, settings));

        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);

        var menu = NativeMenu.GetMenu(window)!;
        Assert.Equal(Strings.MenuFile, menu.Items.OfType<NativeMenuItem>().First().Header);
        var file = menu.Items.OfType<NativeMenuItem>().First().Menu!;
        Assert.Contains(file.Items.OfType<NativeMenuItem>(), item => string.Equals(item.Header, Strings.MenuOpenRecent, StringComparison.Ordinal) && item.Menu is not null);
    }
}
