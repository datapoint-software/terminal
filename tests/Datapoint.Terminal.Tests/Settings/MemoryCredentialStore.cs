using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Tests.Settings;

internal sealed class MemoryCredentialStore : ICredentialStore
{
    public Dictionary<string, string> Items { get; } = new(StringComparer.Ordinal);

    public string Name => "Test store";

    public string? Read(string service, string account) => Items.GetValueOrDefault($"{service}|{account}");

    public void Save(string service, string account, string secret) => Items[$"{service}|{account}"] = secret;

    public bool Delete(string service, string account) => Items.Remove($"{service}|{account}");
}
