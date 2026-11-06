namespace Datapoint.Terminal.Security.Credentials;

/// <summary>
/// Secrets kept by the operating system: Keychain on macOS, Credential Manager on Windows, the Secret Service on Linux.
/// A secret is found by its service and account, e.g. "Datapoint Terminal" and "provider/anthropic".
/// </summary>
public interface ICredentialStore
{
    /// <summary>A name for the store the user recognises, e.g. "Keychain".</summary>
    string Name { get; }

    /// <summary>The secret, or null when there is none.</summary>
    /// <exception cref="CredentialStoreException">The store couldn't be read, e.g. the user denied access.</exception>
    string? Read(string service, string account);

    /// <summary>Saves the secret, replacing any earlier one.</summary>
    void Save(string service, string account, string secret);

    /// <summary>Deletes the secret; returns false when there was none.</summary>
    bool Delete(string service, string account);
}
