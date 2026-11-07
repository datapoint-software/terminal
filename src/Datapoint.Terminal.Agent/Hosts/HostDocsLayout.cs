using Datapoint.Terminal.Agent.Files;

namespace Datapoint.Terminal.Agent.Hosts;

// Before workspaces, each host had one file, hosts/<name>.md, with its log as a section. Each becomes hosts/<name>/README.md
// with its log entries in log/, once, when the workspace opens; a host that already has a folder is left alone.
public static class HostDocsLayout
{
    public static void Upgrade(string hostsFolder, string author)
    {
        if (!Directory.Exists(hostsFolder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(hostsFolder, "*.md").ToList())
        {
            var folder = Path.Combine(hostsFolder, Path.GetFileNameWithoutExtension(file));
            if (Directory.Exists(folder))
            {
                continue;
            }

            var (text, log) = HostDocument.SplitLegacyLog(TextFiles.Read(file));
            Directory.CreateDirectory(folder);
            foreach (var (date, entry) in log)
            {
                var when = date == DateOnly.MinValue ? new DateTimeOffset(File.GetLastWriteTimeUtc(file)) : new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
                HostLog.Write(Path.Combine(folder, HostDocsFolder.LogFolderName), when, author, withAgent: true, entry);
            }

            TextFiles.Write(Path.Combine(folder, HostDocsFolder.DocumentName), text, isPrivate: false);
            File.Delete(file);
        }
    }
}
