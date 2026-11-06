using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public sealed class EcdsaPrivateKey : IDisposable
{
    private readonly ECDsa _key;

    private EcdsaPrivateKey(NistCurve curve, ECDsa key)
    {
        Curve = curve;
        _key = key;
        PublicKey = NistCurves.EncodePoint(key.ExportParameters(includePrivateParameters: false).Q);
    }

    public NistCurve Curve { get; }

    public byte[] PublicKey { get; }

    public static EcdsaPrivateKey Create(NistCurve curve) => new(curve, ECDsa.Create(NistCurves.CurveOf(curve)));

    public static EcdsaPrivateKey FromParameters(NistCurve curve, ReadOnlySpan<byte> encodedPoint, ReadOnlySpan<byte> privateScalar)
    {
        if (!NistCurves.TryDecodePoint(curve, encodedPoint, out var point))
        {
            throw new ArgumentException("The public point isn't on the curve.", nameof(encodedPoint));
        }

        var size = NistCurves.CoordinateSize(curve);
        var scalar = new byte[size];
        var trimmed = privateScalar.TrimStart((byte)0);
        trimmed.CopyTo(scalar.AsSpan(size - trimmed.Length));
        var key = ECDsa.Create(new ECParameters { Curve = NistCurves.CurveOf(curve), Q = point, D = scalar });
        SecretMemory.Wipe(scalar);
        return new EcdsaPrivateKey(curve, key);
    }

    public byte[] SignP1363(ReadOnlySpan<byte> data) =>
        _key.SignData(data, Hashing.NameOf(NistCurves.HashOf(Curve)), DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public void Dispose() => _key.Dispose();
}
