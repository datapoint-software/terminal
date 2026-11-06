using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Keys;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Authentication;

public sealed class InMemorySigner : ISshSigner, IDisposable
{
    private readonly Func<ReadOnlyMemory<byte>, string, byte[]> _sign;
    private readonly IDisposable? _key;

    private InMemorySigner(string keyType, byte[] blob, Func<ReadOnlyMemory<byte>, string, byte[]> sign, IDisposable? key)
    {
        KeyType = keyType;
        PublicKeyBlob = blob;
        _sign = sign;
        _key = key;
    }

    public string KeyType { get; }

    public ReadOnlyMemory<byte> PublicKeyBlob { get; }

    public static InMemorySigner ForEd25519(Ed25519PrivateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var blob = new SshWriter().WriteString(AlgorithmNames.Ed25519).WriteString(key.PublicKey).ToArray();
        return new InMemorySigner(AlgorithmNames.Ed25519, blob, (data, _) => Wrap(AlgorithmNames.Ed25519, key.Sign(data.Span)), key: null);
    }

    public static InMemorySigner ForEcdsa(EcdsaPrivateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var keyType = key.Curve switch
        {
            NistCurve.P256 => AlgorithmNames.EcdsaP256,
            NistCurve.P384 => AlgorithmNames.EcdsaP384,
            _ => AlgorithmNames.EcdsaP521,
        };
        var blob = new SshWriter().WriteString(keyType).WriteString(EcdsaSshKey.CurveName(key.Curve)).WriteString(key.PublicKey).ToArray();
        return new InMemorySigner(keyType, blob, (data, _) => Wrap(keyType, EncodeEcdsa(key.SignP1363(data.Span))), key);
    }

    public static InMemorySigner ForRsa(RsaPrivateKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var (exponent, modulus) = key.PublicKey;
        var blob = new SshWriter().WriteString(AlgorithmNames.RsaSha1).WriteMPInt(exponent).WriteMPInt(modulus).ToArray();
        return new InMemorySigner(AlgorithmNames.RsaSha1, blob, (data, algorithm) => Wrap(algorithm, key.Sign(data.Span, RsaHash(algorithm))), key);
    }

    public Task<byte[]> SignAsync(ReadOnlyMemory<byte> data, string algorithm, CancellationToken cancellationToken) =>
        Task.FromResult(_sign(data, algorithm));

    public void Dispose() => _key?.Dispose();

    private static HashKind RsaHash(string algorithm) => algorithm switch
    {
        AlgorithmNames.RsaSha512 => HashKind.Sha512,
        AlgorithmNames.RsaSha256 => HashKind.Sha256,
        _ => throw new ArgumentException($"RSA signatures with {algorithm} aren't supported.", nameof(algorithm)),
    };

    private static byte[] EncodeEcdsa(byte[] fixedWidth)
    {
        var half = fixedWidth.Length / 2;
        return new SshWriter().WriteMPInt(fixedWidth.AsSpan(0, half)).WriteMPInt(fixedWidth.AsSpan(half)).ToArray();
    }

    private static byte[] Wrap(string algorithm, byte[] signature) =>
        new SshWriter().WriteString(algorithm).WriteString(signature).ToArray();
}
