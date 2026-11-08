using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.About;

// Product decision 145: the application's legal notices. The links are the application's own, not text from a reply or a
// terminal, so they open without the confirmation links in replies get.
public sealed partial class AboutWindow : Window, ILocalizable
{
    public AboutWindow()
    {
        InitializeComponent();
        OnLanguageChanged();
        Localizer.Instance.Register(this);
        AutomationProperties.SetName(Attribution, AboutInfo.Attribution);
        CompanyLink.NavigateUri = AboutInfo.DatapointSite;
        ToolTip.SetTip(CompanyLink, AboutInfo.DatapointSite.AbsoluteUri);
        ProductLink.NavigateUri = AboutInfo.ProductSite;
        ToolTip.SetTip(ProductLink, AboutInfo.ProductSite.AbsoluteUri);
        ToolTip.SetTip(SourceButton, AboutInfo.SourceCode.AbsoluteUri);
        ToolTip.SetTip(WebsiteButton, AboutInfo.ProductSite.AbsoluteUri);
        LicenseButton.Click += (_, _) => ShowText(Strings.AboutLicense, AboutInfo.License());
        NoticesButton.Click += (_, _) => ShowText(Strings.AboutThirdPartyNotices, AboutInfo.ThirdPartyNotices());
        SourceButton.Click += (_, _) => _ = Launcher.LaunchUriAsync(AboutInfo.SourceCode);
        WebsiteButton.Click += (_, _) => _ = Launcher.LaunchUriAsync(AboutInfo.ProductSite);
    }

    // Product decision 150. The attribution itself is the same in every language (decision 145).
    public void OnLanguageChanged()
    {
        var (version, commit) = AboutInfo.Version();
        VersionText.Text = commit is null ? Strings.Format(Strings.AboutVersion, version) : Strings.Format(Strings.AboutVersionWithCommit, version, commit);
        AutomationProperties.SetHelpText(CompanyLink, Strings.AboutAttributionLink);
        AutomationProperties.SetHelpText(ProductLink, Strings.AboutWebsite);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        Close();
        e.Handled = true;
    }

    private void ShowText(string title, string text) => new LegalTextWindow(title, text).Show(this);
}
