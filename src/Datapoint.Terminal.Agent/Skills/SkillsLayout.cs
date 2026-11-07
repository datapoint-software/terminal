using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Skills;

// Before skills, a workspace kept runbooks: runbooks/<name>.md, a title, a description and steps. Each becomes
// skills/<name>/SKILL.md, once, when the workspace opens; one that would replace an existing skill is left where it is.
public static class SkillsLayout
{
    public static void Upgrade(string workspaceFolder)
    {
        var runbooks = System.IO.Path.Combine(workspaceFolder, "runbooks");
        if (!Directory.Exists(runbooks))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(runbooks, "*.md").ToList())
        {
            var text = TextFiles.Read(file);
            var name = SkillDocument.NameFor(System.IO.Path.GetFileNameWithoutExtension(file));
            var target = System.IO.Path.Combine(workspaceFolder, "skills", name, SkillDocument.FileName);
            if (File.Exists(target))
            {
                continue;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            TextFiles.Write(target, SkillDocument.Create(name, DescriptionOf(text), [], new SkillRollout(), text), isPrivate: false);
            File.Delete(file);
        }

        if (Directory.EnumerateFileSystemEntries(runbooks).Any())
        {
            return;
        }

        Directory.Delete(runbooks);
    }

    // The first paragraph after the title, which is where a runbook said what it does.
    private static string DescriptionOf(string text)
    {
        var paragraph = text.ReplaceLineEndings("\n").Split("\n\n")
            .Select(block => block.Trim())
            .FirstOrDefault(block => block.Length > 0 && !block.StartsWith('#') && !block.StartsWith('-'));
        var description = paragraph?.ReplaceLineEndings(" ") ?? SkillDocument.Heading(text) ?? "A procedure saved as a runbook.";
        return description.Length > SkillDocument.MaxDescriptionLength ? description[..SkillDocument.MaxDescriptionLength] : description;
    }
}
