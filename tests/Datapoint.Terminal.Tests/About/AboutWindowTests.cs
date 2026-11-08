using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Datapoint.Terminal.About;

namespace Datapoint.Terminal.Tests.About;

// Product decision 145.
public sealed class AboutWindowTests
{
    [AvaloniaFact]
    public void Shows_the_version_and_the_attribution_the_license_requires_with_the_logo()
    {
        var window = new AboutWindow();
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Directory.CreateDirectory("rendered");
        using (var frame = window.CaptureRenderedFrame())
        {
            frame?.Save("rendered/about.png", new PngBitmapEncoderOptions());
        }

        var attribution = window.FindControl<WrapPanel>("Attribution")!;
        Assert.StartsWith($"Version {AboutInfo.Version().Version}", window.FindControl<SelectableTextBlock>("VersionText")!.Text, StringComparison.Ordinal);
        var words = attribution.Children.Select(child => child is HyperlinkButton link ? (string)link.Content! : ((TextBlock)child).Text);
        Assert.Equal("Based on Datapoint Terminal by Datapoint - Sistemas de Informação, Lda.", string.Join(' ', words));
        Assert.Equal("Based on Datapoint Terminal by Datapoint - Sistemas de Informação, Lda.", Avalonia.Automation.AutomationProperties.GetName(attribution));
        Assert.Equal(new Uri("https://datapoint.software/"), window.FindControl<HyperlinkButton>("CompanyLink")!.NavigateUri);
        Assert.Equal(new Uri("https://datapoint.software/terminal/"), window.FindControl<HyperlinkButton>("ProductLink")!.NavigateUri);
        Assert.True(window.FindControl<DatapointLogo>("Logo")!.IsEffectivelyVisible);
        Assert.Equal(18, window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Count());
        window.Close();
    }

    [AvaloniaFact]
    public void Closes_with_escape()
    {
        var window = new AboutWindow();
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

        Assert.True(closed);
    }

    [Fact]
    public void Carries_the_license_its_terms_and_the_third_party_notices()
    {
        var license = AboutInfo.License();

        Assert.Contains("GNU AFFERO GENERAL PUBLIC LICENSE", license, StringComparison.Ordinal);
        Assert.Contains("Based on Datapoint Terminal by Datapoint", license, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(AboutInfo.ThirdPartyNotices()));
    }

    [Theory]
    [InlineData("0.1.0+6065426e7e3ba8e0b9363086a28d13ab1229b13", "0.1.0", "6065426")]
    [InlineData("0.1.0", "0.1.0", null)]
    [InlineData("1.0.0-alpha.1+6065426e7e3ba8e0b9363086a28d13ab1229b13", "1.0.0-alpha.1", "6065426")]
    [InlineData("1.0.0-rc.1+abc", "1.0.0-rc.1", "abc")]
    public void Reads_the_version_and_the_commit_it_was_built_from(string informational, string version, string? commit)
    {
        Assert.Equal((version, commit), AboutInfo.ParseVersion(informational));
    }
}
