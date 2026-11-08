using System.Reflection;

namespace Datapoint.Terminal.About;

// What the About window shows. The attribution is fixed legal text (NOTICE, term 1), so it isn't translated: a fork must
// keep it word for word, with the logo beside it and the link to Datapoint's site.
public static class AboutInfo
{
    public const string Attribution = "Based on Datapoint Terminal by Datapoint - Sistemas de Informação, Lda.";

    public static readonly Uri DatapointSite = new("https://datapoint.software/");

    public static readonly Uri ProductSite = new("https://datapoint.software/terminal/");

    public static readonly Uri SourceCode = new("https://github.com/datapoint-software/terminal/");

    // The version, and the commit it was built from when the build knew it, e.g. ("0.1.0", "6065426").
    public static (string Version, string? Commit) Version() => ParseVersion(
        typeof(AboutInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0");

    internal static (string Version, string? Commit) ParseVersion(string informational)
    {
        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        if (plus < 0)
        {
            return (informational, null);
        }

        var commit = informational[(plus + 1)..];
        return (informational[..plus], commit.Length == 0 ? null : commit[..Math.Min(7, commit.Length)]);
    }

    // The license, its additional terms and the third-party notices travel inside the application, so the About window shows
    // them wherever it's installed.
    public static string License() => Read("LICENSE") + "\n\n" + Read("NOTICE");

    public static string ThirdPartyNotices() => Read("THIRD-PARTY-NOTICES.txt");

    private static string Read(string name)
    {
        using var stream = typeof(AboutInfo).Assembly.GetManifestResourceStream("Datapoint.Terminal.Legal." + name)
            ?? throw new InvalidOperationException($"The application was built without {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
