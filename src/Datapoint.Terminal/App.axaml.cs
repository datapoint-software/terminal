using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Shell.Motion;

namespace Datapoint.Terminal;

public sealed partial class App : Application
{
    private WorkspaceWindows? _windows;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SettingsStore(SettingsStore.DefaultPath);
            var keys = new ProviderKeys(CredentialStores.ForThisSystem(), Environment.GetEnvironmentVariable);
            var settings = new SettingsViewModel(store, MainWindow.FirstRun(store.Load()), keys, MainWindow.CreateClient);
            MotionPreference.Apply(settings.ReduceMotion);
            settings.AppearanceChanged += (_, _) => MotionPreference.Apply(settings.ReduceMotion);
            _windows = new WorkspaceWindows(desktop, settings);
            _windows.OpenAtStartup();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsClicked(object? sender, EventArgs e) => _windows?.OpenSettings();
}
