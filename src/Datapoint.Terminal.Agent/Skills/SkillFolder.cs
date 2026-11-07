using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Skills;

// The workspace's skills/ folder: one folder per skill, named as the skill is. In a workspace the user hasn't trusted, skills
// are shown to the model as data, not steps to follow. IsTrusted is asked each time, since trust can change while the window
// is open.
public sealed record SkillFolder(string Path, Func<string, bool> IsIgnored, bool CanWrite, Func<bool> IsTrusted)
{
    private const int MostSkills = 500;

    public string FileFor(string name) => System.IO.Path.Combine(Path, name, SkillDocument.FileName);

    public IReadOnlyList<Skill> List()
    {
        try
        {
            return Directory.Exists(Path)
                ? [.. Directory.EnumerateDirectories(Path)
                    .Select(folder => System.IO.Path.Combine(folder, SkillDocument.FileName))
                    .Where(file => File.Exists(file) && !IsIgnored(file))
                    .Take(MostSkills)
                    .Select(file => SkillDocument.Read(TextFiles.Read(file), file))
                    .OrderBy(skill => skill.Title, StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // By its name or its title, as the user or the model may give either.
    public Skill? Find(string name) =>
        List().FirstOrDefault(skill => string.Equals(skill.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(skill.Title, name, StringComparison.OrdinalIgnoreCase));
}
