using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Skills;

// The workspace's skills/ folder: one folder per skill, named as the skill is. In a workspace the user hasn't trusted, skills
// are shown to the model as data, not steps to follow. IsTrusted is asked each time, since trust can change while the window
// is open.
public sealed record SkillFolder(string Path, Func<string, bool> IsIgnored, bool CanWrite, Func<bool> IsTrusted)
{
    // Product decision 101: given a skill's file and its text, the text the agent may use (the copy the user last accepted, while
    // a change from outside waits for review), or null to leave it out until the user has looked at it.
    public Func<string, string, string?>? Reviewed { get; init; }

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
                    .Select(file => (File: file, Text: TextToUse(file)))
                    .Where(item => item.Text is not null)
                    .Select(item => SkillDocument.Read(item.Text!, item.File))
                    .OrderBy(skill => skill.Title, StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private string? TextToUse(string file)
    {
        var text = TextFiles.Read(file);
        return Reviewed is null ? text : Reviewed(file, text);
    }

    // By its name or its title, as the user or the model may give either.
    public Skill? Find(string name) =>
        List().FirstOrDefault(skill => string.Equals(skill.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(skill.Title, name, StringComparison.OrdinalIgnoreCase));
}
