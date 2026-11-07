namespace Datapoint.Terminal.Agent.Files;

// Where the agent keeps files on each machine, under the user's home folder there: never beside the file it edits, where a
// program that loads every file in a folder (e.g. sites-enabled/*) could pick up a backup.
public static class HostFolders
{
    public const string Root = "~/.datapoint-terminal";

    public const string Staging = Root + "/staging";

    public const string Backups = Root + "/backups";
}
