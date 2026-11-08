using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Tests.Chat;
using Datapoint.Terminal.Tests.Settings;
using Datapoint.Terminal.Tests.Shell;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Resources;

// Product decision 150.
[Collection(nameof(LanguageSwitching))]
public sealed class LanguageSettingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-language-").FullName;

    public void Dispose()
    {
        Localizer.Instance.Use(AppLanguage.English);
        Directory.Delete(_folder, recursive: true);
    }

    [AvaloniaFact]
    public void Switches_language_from_appearance_and_keeps_the_other_choices()
    {
        var settings = Create();
        var window = new SettingsWindow { DataContext = settings, History = new FakeHistory() };
        window.Show();
        window.ShowPage(1);
        Dispatcher.UIThread.RunJobs();
        var page = window.FindControl<AppearancePage>("AppearancePage")!;
        var choice = page.FindControl<ComboBox>("LanguageChoice")!;

        choice.SelectedIndex = (int)AppLanguage.PortuguesePortugal;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(AppLanguage.PortuguesePortugal, Localizer.Instance.Language);
        Assert.Equal(AppLanguage.PortuguesePortugal, new SettingsStore(Path.Combine(_folder, "settings.json")).Load().Language);
        Assert.Equal((int)AppLanguage.PortuguesePortugal, choice.SelectedIndex);
        Assert.Equal(0, page.FindControl<ComboBox>("FontChoice")!.SelectedIndex);
        window.Close();
    }

    private SettingsViewModel Create()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        return new SettingsViewModel(store, store.Load(), new ProviderKeys(new MemoryCredentialStore(), _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
    }
}
