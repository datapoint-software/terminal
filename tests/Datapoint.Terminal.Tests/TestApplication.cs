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

    // Tests save what they render to rendered/ beside the tests, for a person to look at. Created once, so none depends on another
    // having run first; the working folder isn't the tests' folder yet when this runs.
    [ModuleInitializer]
    internal static void CreateRenderedFolder() => Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "rendered"));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHarfBuzz()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
