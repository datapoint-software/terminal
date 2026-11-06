using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Security.Tests.Credentials;

/// <summary>Runs against this system's real store with a throwaway item, removed afterwards.</summary>
public sealed class CredentialStoreTests : IDisposable
{
    private const string Service = "Datapoint Terminal Tests";
    private readonly string _account = "test-" + Guid.NewGuid().ToString("N");
    private readonly ICredentialStore? _store = CredentialStores.ForThisSystem();

    public void Dispose() => _store?.Delete(Service, _account);

    private ICredentialStore Store
    {
        get
        {
            Assert.SkipWhen(_store is null, "This system has no credential store the application can use.");
            return _store!;
        }
    }

    [Fact]
    public void Saves_reads_replaces_and_deletes_a_secret()
    {
        Assert.Null(Store.Read(Service, _account));

        Store.Save(Service, _account, "sk-test-first-é✓");
        Assert.Equal("sk-test-first-é✓", Store.Read(Service, _account));

        Store.Save(Service, _account, "sk-test-second");
        Assert.Equal("sk-test-second", Store.Read(Service, _account));

        Assert.True(Store.Delete(Service, _account));
        Assert.Null(Store.Read(Service, _account));
        Assert.False(Store.Delete(Service, _account));
    }

    [Fact]
    public void Keeps_accounts_apart()
    {
        var other = _account + "-other";
        try
        {
            Store.Save(Service, _account, "one");
            Store.Save(Service, other, "two");

            Assert.Equal("one", Store.Read(Service, _account));
            Assert.Equal("two", Store.Read(Service, other));
        }
        finally
        {
            Store.Delete(Service, other);
        }
    }
}
