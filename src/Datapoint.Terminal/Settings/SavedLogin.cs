namespace Datapoint.Terminal.Settings;

// A login whose password is kept in the system's credential store (product decision 113). Only the login is kept here, never
// the password.
public sealed record SavedLogin
{
    // user@host:port, the host as the connection reaches it, so a connection renamed or pointed elsewhere never gets it.
    public required string Login { get; init; }

    // The user chose "Allow always": the saved password is used without asking.
    public bool UsesWithoutAsking { get; init; }
}
