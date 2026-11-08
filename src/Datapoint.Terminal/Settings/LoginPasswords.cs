using System.Globalization;
using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Settings;

// SSH login passwords, in the system's credential store beside the provider keys (product decision 113).
public sealed class LoginPasswords(ICredentialStore? store)
{
    public bool CanSave => store is not null;

    public string? StoreName => store?.Name;

    // The address is the one the connection reaches after reading ~/.ssh/config, never the connection's name, which a
    // workspace's shared files set.
    public static string Login(string user, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"{user}@{host.ToLowerInvariant()}:{port}");

    public string? Read(string login) => store?.Read(ProviderKeys.Service, Account(login));

    public void Save(string login, string password) =>
        (store ?? throw new CredentialStoreException("This system has no credential store to keep the password in.")).Save(ProviderKeys.Service, Account(login), password);

    public void Delete(string login) => store?.Delete(ProviderKeys.Service, Account(login));

    private static string Account(string login) => "ssh-password/" + login;
}
