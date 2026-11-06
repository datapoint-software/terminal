using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public static class SecretMemory
{
    public static void Wipe(Span<byte> secret) => CryptographicOperations.ZeroMemory(secret);

    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    public static bool IsAllZero(ReadOnlySpan<byte> value)
    {
        var accumulated = 0;
        foreach (var item in value)
        {
            accumulated |= item;
        }

        return accumulated == 0;
    }
}
