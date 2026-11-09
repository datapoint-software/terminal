using System.Text;

namespace Datapoint.Terminal.ThirdPartyNotices;

public static class NoticesFile
{
    private static readonly string s_rule = new('=', 78);

    public static string Generate(string repository)
    {
        var assets = Path.Combine(repository, "src", "Datapoint.Terminal", "obj", "project.assets.json");
        if (!File.Exists(assets))
        {
            throw new NoticeException($"{assets} is missing: build the application first.");
        }

        var templates = Path.Combine(repository, "tools", "ThirdPartyNotices", "Licenses");
        var unicode = new Component(
            "Unicode Character Database (the emulator's character width tables)",
            "Unicode-3.0",
            "https://www.unicode.org/ucd/",
            Packages.Normalize(File.ReadAllText(Path.Combine(templates, "Unicode-3.0.txt"))),
            []);
        return Write([.. Packages.Read(assets, templates), unicode], Bundled.Read(repository));
    }

    // Components with the same license text and notices share a section, and each notice is printed once at the end.
    private static string Write(IReadOnlyList<Component> application, IReadOnlyList<Component> bundled)
    {
        IReadOnlyList<Component> components = [.. application, .. bundled];
        var notices = components.SelectMany(component => component.Notices).Distinct(StringComparer.Ordinal).ToList();
        var groups = components.GroupBy(component => component.LicenseText + "\0" + string.Join('\0', component.Notices), StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        text.Append("""
            THIRD-PARTY NOTICES FOR DATAPOINT TERMINAL

            Datapoint Terminal includes the third-party software and data listed below,
            each used under the license shown with it.

            This file is generated from the packages the application ships and from what
            its installers bundle. Don't edit it by hand: after adding or upgrading a
            package, or Velopack's vpk tool, run

                dotnet run --project tools/ThirdPartyNotices

            and commit the result with the change.

            Contents

            In the application:

            """).Append('\n');
        AppendContents(text, application);
        text.Append("""

            In the installers: the Rust crates in Velopack's Update, Setup and stub
            programs, and the AppImage runtime at the start of each Linux .AppImage:

            """).Append('\n');
        AppendContents(text, bundled);

        foreach (var group in groups)
        {
            AppendGroup(text, [.. group], notices);
        }

        for (var index = 0; index < notices.Count; index++)
        {
            text.Append('\n').Append(s_rule).Append('\n').Append("Notice ").Append(index + 1).Append(", included in the packages that refer to it\n")
                .Append(s_rule).Append("\n\n").Append(notices[index]).Append('\n');
        }

        return text.ToString();
    }

    private static void AppendContents(StringBuilder text, IReadOnlyList<Component> components)
    {
        foreach (var component in components)
        {
            text.Append("  ").Append(component.Name).Append(" (").Append(component.License).Append(")\n");
        }
    }

    private static void AppendGroup(StringBuilder text, IReadOnlyList<Component> group, List<string> notices)
    {
        text.Append('\n').Append(s_rule).Append('\n');
        foreach (var component in group)
        {
            text.Append(component.Name).Append(component.Project is null ? string.Empty : " - " + component.Project).Append('\n');
        }

        text.Append("License: ").Append(group[0].License).Append('\n').Append(s_rule).Append("\n\n").Append(group[0].LicenseText).Append('\n');
        if (group[0].Notices.Count == 0)
        {
            return;
        }

        var numbers = group[0].Notices.Select(notice => (notices.IndexOf(notice) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        text.Append("\nThese packages also include third-party material; see Notice ").Append(string.Join(", ", numbers)).Append(" below.\n");
    }
}
