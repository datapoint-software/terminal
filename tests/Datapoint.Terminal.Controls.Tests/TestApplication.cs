using Avalonia;
using Avalonia.Headless;
using Datapoint.Terminal.Controls.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Datapoint.Terminal.Controls.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseSkia()
        .UseHarfBuzz()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
