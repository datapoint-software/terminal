using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public static class Hashing
{
    public static int SizeOf(HashKind kind) => kind switch
    {
        HashKind.Sha1 => SHA1.HashSizeInBytes,
        HashKind.Sha256 => SHA256.HashSizeInBytes,
        HashKind.Sha384 => SHA384.HashSizeInBytes,
        _ => SHA512.HashSizeInBytes,
    };

    [SuppressMessage("Security", "CA5350", Justification = "SHA-1 is only reachable where SSH requires it: hashed known_hosts names and hosts configured for ssh-rsa.")]
    public static byte[] Hash(HashKind kind, ReadOnlySpan<byte> data) => kind switch
    {
        HashKind.Sha1 => SHA1.HashData(data),
        HashKind.Sha256 => SHA256.HashData(data),
        HashKind.Sha384 => SHA384.HashData(data),
        _ => SHA512.HashData(data),
    };

    public static IncrementalHashBuilder Start(HashKind kind) => new(IncrementalHash.CreateHash(NameOf(kind)));

    public static IncrementalHashBuilder StartHmac(HashKind kind, ReadOnlySpan<byte> key) =>
        new(IncrementalHash.CreateHMAC(NameOf(kind), key));

    internal static HashAlgorithmName NameOf(HashKind kind) => kind switch
    {
        HashKind.Sha1 => HashAlgorithmName.SHA1,
        HashKind.Sha256 => HashAlgorithmName.SHA256,
        HashKind.Sha384 => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512,
    };
}
