namespace Datapoint.Terminal.Security.Credentials;

public interface ICredentialStore
{
    string Name { get; }

    string? Read(string service, string account);

    void Save(string service, string account, string secret);

    bool Delete(string service, string account);
}
