namespace Datapoint.Terminal.Shell.Areas;

// The window's areas the user can move (product decision 109); the sidebar stays on the left. The plan under review has an
// area of its own (product decision 119), as does each kind of document: hosts' pages, skills, the workspace's own files, and
// files opened on a server (product decision 120).
public enum AreaKind
{
    Agent,
    Terminals,
    Hosts,
    Skills,
    Workspace,
    ServerFiles,
    Plan,
}
