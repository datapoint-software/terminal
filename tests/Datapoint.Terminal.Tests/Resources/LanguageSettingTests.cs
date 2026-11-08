using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Setup;
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

    [AvaloniaFact]
    public void Switches_setup_to_portuguese_from_its_first_step()
    {
        var settings = Create();
        var window = new SetupWindow { DataContext = new SetupViewModel(settings, _ => null) };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.FindControl<ComboBox>("LanguageChoice")!.SelectedIndex = (int)AppLanguage.PortuguesePortugal;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => string.Equals(text.Text, Strings.SetupWelcomeTitle, StringComparison.Ordinal));
        Assert.NotEqual(Strings.Get(nameof(Strings.SetupWelcomeTitle)), English(nameof(Strings.SetupWelcomeTitle)), StringComparer.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void Shows_settings_in_portuguese()
    {
        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);
        var window = new SettingsWindow { DataContext = Create(), History = new FakeHistory() };
        window.Show();
        for (var page = 0; page < 7; page++)
        {
            window.ShowPage(page);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(Strings.SettingsTitle, window.Title);
        Assert.NotEqual(English(nameof(Strings.SettingsTitle)), window.Title, StringComparer.Ordinal);
        window.Close();
    }

    private static string English(string key) => new System.Resources.ResourceManager("Datapoint.Terminal.Resources.Strings", typeof(Strings).Assembly).GetString(key, System.Globalization.CultureInfo.GetCultureInfo("en"))!;

    private SettingsViewModel Create()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        return new SettingsViewModel(store, store.Load(), new ProviderKeys(new MemoryCredentialStore(), _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
    }
}
