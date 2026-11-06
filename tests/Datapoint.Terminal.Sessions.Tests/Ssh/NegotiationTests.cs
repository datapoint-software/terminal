using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class NegotiationTests
{
    private static KexInit Server(string keyExchange, string cipher = AlgorithmNames.Aes256Gcm, string mac = AlgorithmNames.HmacSha256Etm)
    {
        var payload = new SshWriter()
            .WriteByte(MessageNumber.KexInit)
            .WriteRaw(new byte[16])
            .WriteString(keyExchange)
            .WriteString(AlgorithmNames.Ed25519)
            .WriteString(cipher)
            .WriteString(cipher)
            .WriteString(mac)
            .WriteString(mac)
            .WriteString("none")
            .WriteString("none")
            .WriteString(string.Empty)
            .WriteString(string.Empty)
            .WriteBoolean(false)
            .WriteUInt32(0)
            .ToArray();
        return KexInit.Parse(payload);
    }

    [Fact]
    public void Picks_the_clients_first_choice_the_server_supports()
    {
        var client = KexInit.ForClient(AlgorithmPreferences.Standard, isInitial: true);

        var result = Negotiation.Negotiate(client, Server($"{AlgorithmNames.EcdhP256},{AlgorithmNames.Curve25519}"), isInitial: true);

        Assert.Equal(AlgorithmNames.Curve25519, result.KeyExchange);
    }

    [Fact]
    public void Uses_strict_key_exchange_only_when_the_server_offers_it()
    {
        var client = KexInit.ForClient(AlgorithmPreferences.Standard, isInitial: true);

        Assert.False(Negotiation.Negotiate(client, Server(AlgorithmNames.Curve25519), isInitial: true).IsStrictKex);
        Assert.True(Negotiation.Negotiate(client, Server($"{AlgorithmNames.Curve25519},{AlgorithmNames.StrictKexServerOpenSsh}"), isInitial: true).IsStrictKex);
    }

    [Fact]
    public void Fails_when_nothing_is_shared()
    {
        var client = KexInit.ForClient(AlgorithmPreferences.Standard, isInitial: true);

        Assert.Throws<SshProtocolException>(() => Negotiation.Negotiate(client, Server("diffie-hellman-group1-sha1"), isInitial: true));
    }

    [Fact]
    public void Offers_no_cbc_ciphers_and_no_sha1_macs()
    {
        var client = KexInit.ForClient(AlgorithmPreferences.Standard, isInitial: true);

        Assert.DoesNotContain(client.CipherClientToServer, name => name.Contains("cbc", StringComparison.Ordinal));
        Assert.DoesNotContain(client.MacClientToServer, name => name.Contains("sha1", StringComparison.Ordinal));
    }
}
