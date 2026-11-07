namespace Datapoint.Terminal.Chat;

public enum ChatItemKind
{
    User,
    Agent,
    Command,
    Notice,

    Plan,

    Rollout,

    // A question the agent asked, kept as a quiet line once it's answered.
    Asked,

    // A web search or page read, with the pages it found.
    Web,
}
