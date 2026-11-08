namespace Datapoint.Terminal.Shell.Areas;

// The window's areas the user can move (product decision 109); the sidebar stays on the left. The plan under review has an
// area of its own, apart from documents (product decision 119).
public enum AreaKind
{
    Agent,
    Terminals,
    Documents,
    Plan,
}
