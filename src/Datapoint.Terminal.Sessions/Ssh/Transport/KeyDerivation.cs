using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Sessions.Ssh.Transport;

public static class KeyDerivation
{
    public static byte[] Derive(HashKind hash, ReadOnlySpan<byte> encodedSecret, ReadOnlySpan<byte> exchangeHash, char letter, ReadOnlySpan<byte> sessionId, int length)
    {
        var output = new List<byte>(length);
        using var first = Hashing.Start(hash);
        output.AddRange(first.Append(encodedSecret).Append(exchangeHash).Append([(byte)letter]).Append(sessionId).Finish());
        while (output.Count < length)
        {
            using var next = Hashing.Start(hash);
            output.AddRange(next.Append(encodedSecret).Append(exchangeHash).Append(output.ToArray()).Finish());
        }

        return output.GetRange(0, length).ToArray();
    }
}
