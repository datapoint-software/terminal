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
            if (OperatingSystem.IsMacOS())
            {
                MacDockIcon.Apply();
            }

            var store = new SettingsStore(SettingsStore.DefaultPath);
            var keys = new ProviderKeys(CredentialStores.ForThisSystem(), Environment.GetEnvironmentVariable);
            var settings = new SettingsViewModel(store, MainWindow.FirstRun(store.Load()), keys, MainWindow.CreateClient);
            MotionPreference.Apply(settings.ReduceMotion);
            settings.AppearanceChanged += (_, _) => MotionPreference.Apply(settings.ReduceMotion);
            ApplyWindowTheme(settings);
            settings.AppearanceChanged += (_, _) => ApplyWindowTheme(settings);
            PlatformSettings?.ColorValuesChanged += (_, _) => ApplyWindowTheme(settings);
            OverlayMotion.Start();
            _windows = new WorkspaceWindows(desktop, settings);
            _windows.OpenAtStartup();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Once for the whole application, at start as well as on each change, since every window shares its colours.
    private static void ApplyWindowTheme(SettingsViewModel settings)
    {
        var followsSystem = settings.CustomThemeId is null && settings.TerminalTheme == TerminalTheme.System;
        Themes.WindowColors.Apply(settings.WindowTheme, settings.ActiveTheme(Themes.WindowColors.IsSystemDark), followsSystem);
    }

    private void OnSettingsClicked(object? sender, EventArgs e) => _windows?.OpenSettings();

    private void OnAboutClicked(object? sender, EventArgs e) => _windows?.OpenAbout();
}
