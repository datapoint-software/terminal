using System.Globalization;

namespace Datapoint.Terminal.Resources;

// The Language list in Settings and in setup, in the order of AppLanguage's values (product decision 150). Each language
// is named in itself, so it can be found whatever is shown now.
public static class LanguageChoices
{
    public static IReadOnlyList<TextChoice> Create() =>
    [
        new(() => Strings.Format(Strings.AppearanceLanguageSystem, NameOf(Localizer.Resolve(AppLanguage.System, Localizer.Instance.SystemCulture)))),
        new(() => Strings.LanguageEnglish),
        new(() => Strings.LanguagePortuguese),
    ];

    private static string NameOf(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "pt", StringComparison.Ordinal) ? Strings.LanguagePortuguese : Strings.LanguageEnglish;
}
