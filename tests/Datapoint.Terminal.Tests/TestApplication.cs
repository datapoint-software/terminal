using Avalonia;
using Avalonia.Headless;
using Datapoint.Terminal.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Datapoint.Terminal.Tests;

public static class TestApplication
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHarfBuzz()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
