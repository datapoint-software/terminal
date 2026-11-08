using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Datapoint.Terminal.About;

namespace Datapoint.Terminal.Tests.About;

// Product decision 149.
public sealed class AppIconTests
{
    [AvaloniaFact]
    public void Gives_every_window_the_application_icon()
    {
        var about = new AboutWindow();
        var plain = new Window();
        about.Show();
        plain.Show();

        Assert.NotNull(about.Icon);
        Assert.NotNull(plain.Icon);
        about.Close();
        plain.Close();
    }

    [Fact]
    public void Keeps_the_icon_files_beside_their_source()
    {
        var branding = Path.Combine(Repository(), "branding");

        foreach (var name in new[] { "terminal-icon.svg", "terminal-icon.png", "terminal-icon.ico", "terminal-icon.icns" })
        {
            Assert.True(File.Exists(Path.Combine(branding, name)), name);
        }

        Assert.Contains("#0077BE", File.ReadAllText(Path.Combine(branding, "terminal-icon.svg")), StringComparison.Ordinal);
    }

    private static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "NOTICE")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("The repository's root wasn't found.");
    }
}
