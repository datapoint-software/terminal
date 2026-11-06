using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class EcdhKeyPair : IDisposable
{
    private readonly ECDiffieHellman _key;
    private readonly NistCurve _curve;

    private EcdhKeyPair(NistCurve curve)
    {
        _curve = curve;
        _key = ECDiffieHellman.Create(NistCurves.CurveOf(curve));
        PublicKey = NistCurves.EncodePoint(_key.ExportParameters(includePrivateParameters: false).Q);
    }

    public byte[] PublicKey { get; }

    public static AlgorithmInfo InfoFor(NistCurve curve) => new($"ECDH {curve}", CryptographySource.DotNet, IsFipsApproved: true);

    public static EcdhKeyPair Create(NistCurve curve) => new(curve);

    public bool TryAgree(ReadOnlySpan<byte> peerPublicKey, out byte[] sharedSecret)
    {
        sharedSecret = [];
        if (!NistCurves.TryDecodePoint(_curve, peerPublicKey, out var point))
        {
            return false;
        }

        using var peer = ECDiffieHellman.Create(new ECParameters { Curve = NistCurves.CurveOf(_curve), Q = point });
        sharedSecret = _key.DeriveRawSecretAgreement(peer.PublicKey);
        return true;
    }

    public void Dispose() => _key.Dispose();
}
