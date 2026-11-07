using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Skills;

namespace Datapoint.Terminal.Agent.Tests.Skills;

public sealed class SkillFolderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("skill-folder-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, string description)
    {
        var path = Path.Combine(_folder, name, SkillDocument.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SkillDocument.Create(name, description, [], new SkillRollout(), "# " + name + "\n\n1. Do it."));
        return path;
    }

    [Fact]
    public void Gives_the_reviewed_text_and_leaves_out_skills_not_yet_reviewed()
    {
        var changed = Write("patch", "Changed from outside.");
        Write("new-one", "Arrived from outside.");
        var accepted = File.ReadAllText(changed).Replace("Changed from outside.", "As accepted.", StringComparison.Ordinal);
        var folder = new SkillFolder(_folder, _ => false, CanWrite: true, () => true)
        {
            Reviewed = (path, text) => path.Contains("new-one", StringComparison.Ordinal) ? null : accepted,
        };

        var skill = Assert.Single(folder.List());

        Assert.Equal(("patch", "As accepted."), (skill.Name, skill.Description));
    }

    [Fact]
    public void Says_when_the_application_writes_a_file()
    {
        var written = new List<string>();
        void OnWritten(object? sender, FileWrittenEventArgs e) => written.Add(e.Path + "|" + e.Text);
        FileWrites.Written += OnWritten;
        try
        {
            TextFiles.Write(Path.Combine(_folder, "notes.md"), "# Notes\n", isPrivate: false);
        }
        finally
        {
            FileWrites.Written -= OnWritten;
        }

        Assert.Contains(Path.Combine(_folder, "notes.md") + "|# Notes\n", written, StringComparer.Ordinal);
    }
}
