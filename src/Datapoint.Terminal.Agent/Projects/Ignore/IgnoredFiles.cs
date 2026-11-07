namespace Datapoint.Terminal.Agent.Projects.Ignore;

// One .terminalignore file's patterns, which apply below its folder.
public sealed record IgnoredFiles(string Folder, IReadOnlyList<string> Patterns);
