using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public static class RandomBytes
{
    public static void Fill(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);

    public static byte[] Create(int length) => RandomNumberGenerator.GetBytes(length);

    public static string CreateHexToken(int bytes) => Convert.ToHexStringLower(Create(bytes));
}
