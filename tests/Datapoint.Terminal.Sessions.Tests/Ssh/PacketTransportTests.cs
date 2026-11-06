using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class PacketTransportTests
{
    public static TheoryData<string, string?> Ciphers => new()
    {
        { AlgorithmNames.ChaCha20Poly1305, null },
        { AlgorithmNames.Aes256Gcm, null },
        { AlgorithmNames.Aes128Gcm, null },
        { AlgorithmNames.Aes256Ctr, AlgorithmNames.HmacSha256Etm },
        { AlgorithmNames.Aes128Ctr, AlgorithmNames.HmacSha512Etm },
    };

    private static byte[] Derive(char letter, int length) => Enumerable.Range(0, length).Select(index => (byte)(letter + index)).ToArray();

    private static (PacketTransport Sender, PacketTransport Receiver, MemoryStream Wire) Pair(string cipher, string? mac)
    {
        var wire = new MemoryStream();
        var sender = new PacketTransport(wire);
        var receiver = new PacketTransport(wire);
        sender.UseSendProtection(ProtectionFactory.Create(cipher, mac, Derive, clientToServer: true), resetSequence: false);
        receiver.UseReceiveProtection(ProtectionFactory.Create(cipher, mac, Derive, clientToServer: true), resetSequence: false);
        return (sender, receiver, wire);
    }

    [Theory]
    [MemberData(nameof(Ciphers))]
    public async Task Round_trips_packets(string cipher, string? mac)
    {
        var (sender, receiver, wire) = Pair(cipher, mac);
        byte[][] payloads = [[1], [.. Enumerable.Repeat((byte)0xAB, 1000)], [.. Enumerable.Range(0, 70_000).Select(index => (byte)index)]];
        foreach (var payload in payloads)
        {
            await sender.SendAsync(payload, TestContext.Current.CancellationToken);
        }

        wire.Position = 0;
        foreach (var payload in payloads)
        {
            Assert.Equal(payload, await receiver.ReceiveAsync(TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [MemberData(nameof(Ciphers))]
    public async Task Rejects_a_tampered_packet(string cipher, string? mac)
    {
        var (sender, receiver, wire) = Pair(cipher, mac);
        await sender.SendAsync(new byte[64], TestContext.Current.CancellationToken);
        var bytes = wire.ToArray();
        bytes[^20] ^= 1;

        var tampered = new PacketTransport(new MemoryStream(bytes));
        tampered.UseReceiveProtection(ProtectionFactory.Create(cipher, mac, Derive, clientToServer: true), resetSequence: false);

        await Assert.ThrowsAsync<SshProtocolException>(() => tampered.ReceiveAsync(TestContext.Current.CancellationToken));
        receiver.Dispose();
    }

    [Fact]
    public async Task Rejects_a_packet_longer_than_the_limit()
    {
        byte[] header = [0x7F, 0xFF, 0xFF, 0xF0, 4];
        using var transport = new PacketTransport(new MemoryStream(header));

        await Assert.ThrowsAsync<SshProtocolException>(() => transport.ReceiveAsync(TestContext.Current.CancellationToken));
    }
}
