using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApplication))]

namespace Datapoint.Terminal.Tests;

public static class TestApplication
{
    // Tests check English text, so they read it in English whatever the machine's language (product decision 150).
    [ModuleInitializer]
    internal static void UseEnglish() => Localizer.Instance.Use(AppLanguage.English);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHarfBuzz()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
