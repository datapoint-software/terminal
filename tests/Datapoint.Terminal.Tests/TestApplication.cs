using Avalonia;
using Avalonia.Headless;
using Datapoint.Terminal.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Datapoint.Terminal.Tests;

/// <summary>The application itself, with its styles, on the headless platform.</summary>
public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHarfBuzz()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
