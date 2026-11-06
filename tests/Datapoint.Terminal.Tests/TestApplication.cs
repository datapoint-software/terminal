using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Datapoint.Terminal.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Datapoint.Terminal.Tests;

public sealed class TestApplication : Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseSkia()
        .UseHarfBuzz()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize() => Styles.Add(new FluentTheme());
}
