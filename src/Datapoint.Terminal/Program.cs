using Avalonia;
using Avalonia.Media;
using Velopack;

namespace Datapoint.Terminal;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // First, before anything else runs: the installer starts the application to finish installing, updating or
        // uninstalling it, and Run handles that and exits. Started any other way, it returns at once.
        VelopackApp.Build().Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseHarfBuzz()
            // Inter on every system: Avalonia sets the macOS interface font off-centre in buttons and badges, and the
            // screenshot tests render with Inter, so they show what users see.
            .WithInterFont()
            .With(new FontManagerOptions { DefaultFamilyName = "fonts:Inter#Inter" })
            .LogToTrace();
}
