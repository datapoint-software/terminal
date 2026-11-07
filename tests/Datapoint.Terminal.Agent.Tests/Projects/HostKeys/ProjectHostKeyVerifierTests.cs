using Datapoint.Terminal.Agent.Projects.HostKeys;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Agent.Tests.Projects.HostKeys;

public sealed class ProjectHostKeyVerifierTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-host-keys-").FullName;
    private readonly List<HostKeyChange> _asked = [];

    private string StorePath => Path.Combine(_folder, ".terminal", ProjectHostKeys.FileName);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static SshPublicKey NewKey()
    {
        using var signer = InMemorySigner.ForEd25519(Ed25519PrivateKey.FromSeed(RandomBytes.Create(Ed25519PrivateKey.SeedSize)));
        return SshPublicKey.Parse(signer.PublicKeyBlob.Span);
    }

    private Task<HostKeyDecision> VerifyAsync(SshPublicKey key, HostKeyDecision known = HostKeyDecision.Accepted, bool answer = true, string? path = "")
    {
        var verifier = new ProjectHostKeyVerifier(new FixedVerifier(known), new ProjectHostKeys(path is null ? null : StorePath), "web1", (change, _) =>
        {
            _asked.Add(change);
            return Task.FromResult(answer);
        });
        return verifier.VerifyAsync("web1.example.com", 22, key, CancellationToken.None);
    }

    [Fact]
    public async Task Remembers_the_first_key_without_asking()
    {
        var key = NewKey();

        Assert.Equal(HostKeyDecision.Accepted, await VerifyAsync(key));
        Assert.Equal(HostKeyDecision.Accepted, await VerifyAsync(key));

        Assert.Empty(_asked);
        Assert.Equal(key.KeyType + " " + key.Fingerprint, new ProjectHostKeys(StorePath).LastSeen("web1"));
    }

    [Fact]
    public async Task Asks_when_the_accepted_key_differs_from_the_one_last_seen()
    {
        var before = NewKey();
        var now = NewKey();
        await VerifyAsync(before);

        Assert.Equal(HostKeyDecision.Accepted, await VerifyAsync(now));

        var change = Assert.Single(_asked);
        Assert.Equal("web1", change.Connection);
        Assert.Contains(before.Fingerprint, change.Before, StringComparison.Ordinal);
        Assert.Contains(now.Fingerprint, change.Now, StringComparison.Ordinal);
        Assert.Contains(now.Fingerprint, new ProjectHostKeys(StorePath).LastSeen("web1"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_and_keeps_the_old_key_when_the_user_cancels()
    {
        var before = NewKey();
        await VerifyAsync(before);

        Assert.Equal(HostKeyDecision.Rejected, await VerifyAsync(NewKey(), answer: false));

        Assert.Contains(before.Fingerprint, new ProjectHostKeys(StorePath).LastSeen("web1"), StringComparison.Ordinal);
    }

    // The user's own known_hosts decides trust; the project's record can never let in a key it refuses.
    [Fact]
    public async Task Never_accepts_a_key_the_users_verifier_refuses()
    {
        var key = NewKey();
        await VerifyAsync(key);

        Assert.Equal(HostKeyDecision.Rejected, await VerifyAsync(key, known: HostKeyDecision.Rejected));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task Remembers_nothing_without_a_path()
    {
        Assert.Equal(HostKeyDecision.Accepted, await VerifyAsync(NewKey(), path: null));

        Assert.False(File.Exists(StorePath));
    }

    private sealed class FixedVerifier(HostKeyDecision decision) : IHostKeyVerifier
    {
        public Task<HostKeyDecision> VerifyAsync(string host, int port, SshPublicKey key, CancellationToken cancellationToken) => Task.FromResult(decision);
    }
}
