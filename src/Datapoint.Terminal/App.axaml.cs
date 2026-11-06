using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal;

public sealed partial class App : Application
{
    private ProjectWindows? _windows;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SettingsStore(SettingsStore.DefaultPath);
            var keys = new ProviderKeys(CredentialStores.ForThisSystem(), Environment.GetEnvironmentVariable);
            var settings = new SettingsViewModel(store, MainWindow.FirstRun(store.Load()), keys, MainWindow.CreateClient);
            _windows = new ProjectWindows(desktop, settings);
            _windows.OpenAtStartup();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsClicked(object? sender, EventArgs e) => _windows?.OpenSettings();
}
