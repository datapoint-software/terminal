using System.Runtime.Versioning;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Keys;
using Datapoint.Terminal.Sessions.Ssh.KnownHosts;

using Datapoint.Terminal.Sessions.Tests.Ssh;

namespace Datapoint.Terminal.Sessions.Tests.Ssh.KnownHosts;

public sealed class KnownHostsFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dpt-known-").FullName;
    private readonly SshPublicKey _key = NewKey();
    private readonly SshPublicKey _otherKey = NewKey();

    private string FilePath => Path.Combine(_folder, "known_hosts");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static SshPublicKey NewKey()
    {
        using var signer = InMemorySigner.ForEd25519(Ed25519PrivateKey.FromSeed(RandomBytes.Create(Ed25519PrivateKey.SeedSize)));
        return SshPublicKey.Parse(signer.PublicKeyBlob.Span);
    }

    private static string Line(string names, SshPublicKey key) => $"{names} {key.KeyType} {Convert.ToBase64String(key.Blob)}";

    private KnownHostsFile Write(params string[] lines)
    {
        File.WriteAllLines(FilePath, lines);
        return new KnownHostsFile([FilePath]);
    }

    [Fact]
    public void Knows_a_host_listed_by_name()
    {
        var file = Write("# comment", Line("web.example.com,10.0.0.5", _key));

        Assert.Equal(KnownHostStatus.Known, file.Check("10.0.0.5", 22, _key).Status);
    }

    [Fact]
    public void Writes_ports_other_than_22_in_brackets()
    {
        var file = Write(Line("[web.example.com]:2222", _key));

        Assert.Equal(KnownHostStatus.Known, file.Check("web.example.com", 2222, _key).Status);
        Assert.Equal(KnownHostStatus.Unknown, file.Check("web.example.com", 22, _key).Status);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task Matches_names_hashed_by_ssh_keygen()
    {
        var file = Write(Line("web.example.com", _key));
        await OpenSshServer.RunToolAsync("ssh-keygen", "-H", "-f", FilePath);

        Assert.DoesNotContain("web.example.com", await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken), StringComparison.Ordinal);

        Assert.Equal(KnownHostStatus.Known, file.Check("web.example.com", 22, _key).Status);
        Assert.Equal(KnownHostStatus.Unknown, file.Check("db.example.com", 22, _key).Status);
    }

    [Fact]
    public void Honours_wildcards_and_negation()
    {
        var file = Write(Line("*.example.com,!evil.example.com", _key));

        Assert.Equal(KnownHostStatus.Known, file.Check("web.example.com", 22, _key).Status);
        Assert.Equal(KnownHostStatus.Unknown, file.Check("evil.example.com", 22, _key).Status);
    }

    [Fact]
    public void Reports_a_changed_key_with_where_the_old_one_is()
    {
        var file = Write(Line("web.example.com", _otherKey));

        var result = file.Check("web.example.com", 22, _key);

        Assert.Equal(KnownHostStatus.Changed, result.Status);
        Assert.Equal(1, result.Line);
        Assert.Equal(FilePath, result.Source);
    }

    [Fact]
    public void Refuses_a_revoked_key_even_when_it_is_also_listed()
    {
        var file = Write(Line("web.example.com", _key), "@revoked * " + _key.KeyType + " " + Convert.ToBase64String(_key.Blob));

        Assert.Equal(KnownHostStatus.Revoked, file.Check("web.example.com", 22, _key).Status);
    }

    [Fact]
    public void Adds_a_hashed_line_that_it_then_knows()
    {
        var file = Write(Line("db.example.com", _otherKey));

        file.Add("web.example.com", 2222, _key);

        var text = File.ReadAllText(FilePath);
        Assert.Equal(KnownHostStatus.Known, file.Check("web.example.com", 2222, _key).Status);
        Assert.DoesNotContain("web.example.com", text, StringComparison.Ordinal);
        Assert.StartsWith(Line("db.example.com", _otherKey) + "\n|1|", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Creates_the_file_when_it_does_not_exist()
    {
        var file = new KnownHostsFile([Path.Combine(_folder, "new", "known_hosts")]);

        file.Add("web.example.com", 22, _key);

        Assert.Equal(KnownHostStatus.Known, file.Check("web.example.com", 22, _key).Status);
    }
}
