using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class EcdsaPublicKey : IDisposable
{
    private readonly ECDsa _key;

    private EcdsaPublicKey(NistCurve curve, ECDsa key)
    {
        Curve = curve;
        _key = key;
    }

    public NistCurve Curve { get; }

    public static AlgorithmInfo InfoFor(NistCurve curve) => new($"ECDSA {curve}", CryptographySource.DotNet, IsFipsApproved: true);

    public static bool TryCreate(NistCurve curve, ReadOnlySpan<byte> encodedPoint, out EcdsaPublicKey? key)
    {
        key = null;
        if (!NistCurves.TryDecodePoint(curve, encodedPoint, out var point))
        {
            return false;
        }

        key = new EcdsaPublicKey(curve, ECDsa.Create(new ECParameters { Curve = NistCurves.CurveOf(curve), Q = point }));
        return true;
    }

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signatureP1363)
    {
        if (signatureP1363.Length != 2 * NistCurves.CoordinateSize(Curve))
        {
            return false;
        }

        try
        {
            return _key.VerifyData(data, signatureP1363, Hashing.NameOf(NistCurves.HashOf(Curve)), DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public void Dispose() => _key.Dispose();
}
