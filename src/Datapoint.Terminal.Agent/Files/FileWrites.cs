namespace Datapoint.Terminal.Agent.Files;

// Raised after the application writes a file, so a file it wrote itself, e.g. a skill the agent saved or one the user edited in
// the application, is taken as accepted rather than as a change from outside (product decision 101).
public static class FileWrites
{
    public static event EventHandler<FileWrittenEventArgs>? Written;

    public static void Notify(string path, string text) => Written?.Invoke(null, new FileWrittenEventArgs(path, text));
}
