using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Product decision 102: the memory kept for every workspace lives in the application's data, beside its settings. It used to
// be in ~/.datapoint-terminal/, and is moved from there once.
public static class MemoryLocation
{
    private static readonly Lazy<string> s_global = new(() => Locate(Path.GetDirectoryName(SettingsStore.DefaultPath)!, WorkspaceFolders.Global));

    public static string Global => s_global.Value;

    public static string Locate(string appData, string legacyFolder)
    {
        var path = Path.Combine(appData, MemoryFile.FileName);
        var legacy = Path.Combine(legacyFolder, MemoryFile.FileName);
        try
        {
            if (File.Exists(legacy) && !File.Exists(path))
            {
                Directory.CreateDirectory(appData);
                File.Move(legacy, path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left where it was; the agent starts a new one, and the old file is still there for the user.
        }

        return path;
    }
}
