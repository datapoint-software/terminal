namespace Datapoint.Terminal.Agent.Web;

// The user's answer when the agent asks to search the web or read a page.
public enum WebAccess
{
    Denied,

    Once,

    // Every later search and page read too, until the user takes it back in Settings.
    Always,
}
