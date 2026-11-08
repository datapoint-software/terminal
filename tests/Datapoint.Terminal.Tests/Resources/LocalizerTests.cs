using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Tests.Resources;

// Product decision 150. These change the language for the whole process, so they run on their own.
[Collection(nameof(LanguageSwitching))]
public sealed class LocalizerTests : IDisposable
{
    public void Dispose() => Localizer.Instance.Use(AppLanguage.English);

    [Theory]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("pt-BR", "pt-PT")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    public void Follows_the_system_language_when_it_has_one(string system, string expected)
    {
        var culture = Localizer.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(system));

        Assert.Equal(expected, culture.Name);
    }

    [AvaloniaFact]
    public void Changes_bound_text_without_a_restart()
    {
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new TrExtension(nameof(Strings.ChatTitle)).ProvideValue(null!));

        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);

        Assert.Equal("Agente", text.Text);
    }

    [AvaloniaFact]
    public void Sets_headings_in_capitals()
    {
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new TrExtension(nameof(Strings.PlanStepsTitle)) { Upper = true }.ProvideValue(null!));

        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);

        Assert.Equal("PASSOS", text.Text);
    }

    [AvaloniaFact]
    public void Fills_a_format_from_a_binding()
    {
        var text = new TextBlock { DataContext = "web-1" };
        text.Bind(TextBlock.TextProperty, new TrExtension(nameof(Strings.SidebarAddHost)) { Argument = new Avalonia.Data.Binding() }.ProvideValue(null!));

        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);

        Assert.Equal("Adicionar web-1 ao espaço de trabalho", text.Text);
    }

    [AvaloniaFact]
    public void Refreshes_text_a_view_model_builds()
    {
        var model = new Titled();
        var text = new TextBlock { DataContext = model };
        text.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(Titled.Title)));

        Localizer.Instance.Use(AppLanguage.PortuguesePortugal);

        Assert.Equal("Agente", text.Text);
    }

    private sealed class Titled : LocalizedObject
    {
        private readonly string _key = nameof(Strings.ChatTitle);

        public string Title => Strings.Get(_key);
    }
}
