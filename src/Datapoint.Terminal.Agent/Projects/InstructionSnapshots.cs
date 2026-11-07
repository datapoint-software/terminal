using System.Text.Json;

namespace Datapoint.Terminal.Agent.Projects;

// The text of each instruction file as the agent last read it, so a change can be shown to the user (product line 29). Kept
// in the application's own data, never in a project, so nothing in the project can hide a change. Shared by all windows.
public sealed class InstructionSnapshots(string path)
{
    private readonly Lock _sync = new();
    private Dictionary<string, string>? _texts;

    public IReadOnlyList<InstructionChange> Update(IEnumerable<InstructionFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        lock (_sync)
        {
            _texts ??= Load();
            var changes = new List<InstructionChange>();
            var isChanged = false;
            foreach (var file in files.Where(file => file.Problem is null))
            {
                if (_texts.TryGetValue(file.Path, out var before) && string.Equals(before, file.Text, StringComparison.Ordinal))
                {
                    continue;
                }

                if (before is not null)
                {
                    changes.Add(new InstructionChange(file.Path, before, file.Text));
                }

                _texts[file.Path] = file.Text;
                isChanged = true;
            }

            if (isChanged)
            {
                Save(_texts);
            }

            return changes;
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            return File.Exists(path)
                ? new Dictionary<string, string>(JsonSerializer.Deserialize(File.ReadAllBytes(path), ProjectJsonContext.Default.DictionaryStringString) ?? [], ProjectFolders.Comparer)
                : new Dictionary<string, string>(ProjectFolders.Comparer);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(ProjectFolders.Comparer);
        }
    }

    // Written like the settings: to a private temporary file first, then moved into place.
    private void Save(Dictionary<string, string> texts)
    {
        try
        {
            var folder = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(folder);
            var temporary = Path.Combine(folder, $".instructions-{Guid.NewGuid():N}.tmp");
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                JsonSerializer.Serialize(stream, texts, ProjectJsonContext.Default.DictionaryStringString);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not knowing the previous text only means a later change can't be shown; the files themselves are unaffected.
        }
    }
}
