using System.Text.Json;
using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Workspaces;

// Each instruction file as the agent last read it, so a change can be shown to the user (product line 29). Kept in the
// application's own data, never in a workspace, so nothing in the workspace can hide a change. Shared by all windows.
//
// Only masked text is stored: the file sits outside the workspace, in backups and synced home folders, and would otherwise keep
// a copy of any secret written in an instruction file long after it was removed there.
public sealed class InstructionSnapshots(string path)
{
    private readonly Lock _sync = new();
    private Dictionary<string, InstructionSnapshot>? _snapshots;

    public IReadOnlyList<InstructionChange> Update(IEnumerable<InstructionFile> files, SecretRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(redactor);
        lock (_sync)
        {
            var isChanged = false;
            _snapshots ??= Load(redactor, out isChanged);
            var changes = new List<InstructionChange>();
            foreach (var file in files.Where(file => file.Problem is null))
            {
                var fingerprint = SecretRedactor.Fingerprint(file.Text);
                _snapshots.TryGetValue(file.Path, out var before);
                if (before is not null && string.Equals(before.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    continue;
                }

                var masked = redactor.Redact(file.Text);
                if (before is not null)
                {
                    changes.Add(new InstructionChange(file.Path, before.Text, masked));
                }

                _snapshots[file.Path] = new InstructionSnapshot(masked, fingerprint);
                isChanged = true;
            }

            if (isChanged)
            {
                Save(_snapshots);
            }

            return changes;
        }
    }

    // Product decision 101: how a workspace's instruction file or skill compares with the copy the user last accepted, without
    // accepting it. Accepted is that copy, masked, when there is one.
    public ReviewStatus Check(string path, string text, SecretRedactor redactor, out InstructionSnapshot? accepted)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_sync)
        {
            _snapshots ??= LoadAndSave(redactor);
            accepted = _snapshots.GetValueOrDefault(path);
            return accepted is null ? ReviewStatus.New
                : string.Equals(accepted.Fingerprint, SecretRedactor.Fingerprint(text), StringComparison.Ordinal) ? ReviewStatus.Accepted
                : ReviewStatus.Changed;
        }
    }

    public void Accept(string path, string text, SecretRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(redactor);
        lock (_sync)
        {
            _snapshots ??= LoadAndSave(redactor);
            _snapshots[path] = new InstructionSnapshot(redactor.Redact(text), SecretRedactor.Fingerprint(text));
            Save(_snapshots);
        }
    }

    // A folder is known once its files were first taken as they were, so a file added later counts as new.
    public bool IsKnown(string path, SecretRedactor redactor)
    {
        lock (_sync)
        {
            _snapshots ??= LoadAndSave(redactor);
            return _snapshots.ContainsKey(path);
        }
    }

    // Whether the accepted copy is the text itself, with nothing masked, so it can be written back over a change.
    public static bool IsWhole(InstructionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return string.Equals(SecretRedactor.Fingerprint(snapshot.Text), snapshot.Fingerprint, StringComparison.Ordinal);
    }

    private Dictionary<string, InstructionSnapshot> LoadAndSave(SecretRedactor redactor)
    {
        var snapshots = Load(redactor, out var changed);
        if (changed)
        {
            Save(snapshots);
        }

        return snapshots;
    }

    // Files that no longer exist are dropped, and the unmasked copies earlier versions stored are masked, so changed is set
    // whenever the file needs saving again.
    private Dictionary<string, InstructionSnapshot> Load(SecretRedactor redactor, out bool changed)
    {
        changed = false;
        Dictionary<string, InstructionSnapshot> snapshots;
        try
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, InstructionSnapshot>(WorkspaceFolders.Comparer);
            }

            var bytes = File.ReadAllBytes(path);
            var isLegacy = IsLegacy(bytes);
            snapshots = isLegacy ? Migrate(bytes, redactor) : ReadCurrent(bytes);
            changed = isLegacy;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new Dictionary<string, InstructionSnapshot>(WorkspaceFolders.Comparer);
        }

        foreach (var gone in snapshots.Keys.Where(file => !File.Exists(file) && !Directory.Exists(file)).ToList())
        {
            snapshots.Remove(gone);
            changed = true;
        }

        return snapshots;
    }

    private static Dictionary<string, InstructionSnapshot> ReadCurrent(byte[] bytes)
    {
        var read = JsonSerializer.Deserialize(bytes, WorkspaceJsonContext.Default.DictionaryStringInstructionSnapshot) ?? [];
        return new Dictionary<string, InstructionSnapshot>(read, WorkspaceFolders.Comparer);
    }

    private static Dictionary<string, InstructionSnapshot> Migrate(byte[] bytes, SecretRedactor redactor)
    {
        var texts = JsonSerializer.Deserialize(bytes, WorkspaceJsonContext.Default.DictionaryStringString) ?? [];
        return texts.ToDictionary(
            item => item.Key,
            item => new InstructionSnapshot(redactor.Redact(item.Value), SecretRedactor.Fingerprint(item.Value)),
            WorkspaceFolders.Comparer);
    }

    // Earlier versions stored each file's text directly, as a string.
    private static bool IsLegacy(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.EnumerateObject().Any(property => property.Value.ValueKind == JsonValueKind.String);
    }

    // Written like the settings: to a private temporary file first, then moved into place over the old one.
    private void Save(Dictionary<string, InstructionSnapshot> snapshots)
    {
        try
        {
            var folder = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(folder);
            var temporary = Files.TemporaryFiles.For(path);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                JsonSerializer.Serialize(stream, snapshots, WorkspaceJsonContext.Default.DictionaryStringInstructionSnapshot);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not knowing the previous text only means a later change can't be shown; the files themselves are unaffected.
        }
    }
}
