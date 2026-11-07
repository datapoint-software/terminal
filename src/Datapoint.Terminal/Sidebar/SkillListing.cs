using Datapoint.Terminal.Agent.Skills;

namespace Datapoint.Terminal.Sidebar;

// The workspace's skills: folders in skills/ with a SKILL.md, by name. A skill's name is its document's first heading, or its
// folder's name in words, e.g. "patch-and-reboot" as "Patch and reboot".
public static class SkillListing
{
    public const string FileName = SkillDocument.FileName;

    private const int MostSkills = 500;
    private const int HeadingSearchLength = 8192;

    public static IReadOnlyList<SkillItemViewModel> List(string skillsFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(skillsFolder);
        try
        {
            return [.. new DirectoryInfo(skillsFolder).EnumerateDirectories()
                .Select(folder => System.IO.Path.Combine(folder.FullName, FileName))
                .Where(File.Exists)
                .Take(MostSkills)
                .Select(path => new SkillItemViewModel(NameOf(path), path))
                .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    public static string NameOf(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) ?? string.Empty;
        return Heading(Start(path)) ?? Words(folder);
    }

    // The first heading after the header, whose YAML comments also start with "# ".
    public static string? Heading(string text) => SkillDocument.Heading(SkillDocument.ReadHeader(text).Body);

    // Only the start is read, so a large skill can't slow the sidebar.
    private static string Start(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[HeadingSearchLength];
            return new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string Words(string folder)
    {
        var words = folder.Replace('-', ' ').Replace('_', ' ').Trim();
        return words.Length == 0 ? folder : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
