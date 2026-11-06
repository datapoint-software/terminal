using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

public sealed class EcdsaSshKey : SshPublicKey
{
    private readonly EcdsaPublicKey _key;

    private EcdsaSshKey(string keyType, EcdsaPublicKey key, byte[] blob)
        : base(keyType, blob) => _key = key;

    public static NistCurve CurveOf(string keyType) => keyType switch
    {
        AlgorithmNames.EcdsaP256 => NistCurve.P256,
        AlgorithmNames.EcdsaP384 => NistCurve.P384,
        _ => NistCurve.P521,
    };

    public static string CurveName(NistCurve curve) => curve switch
    {
        NistCurve.P256 => "nistp256",
        NistCurve.P384 => "nistp384",
        _ => "nistp521",
    };

    internal static EcdsaSshKey Read(string keyType, ref SshReader reader, ReadOnlySpan<byte> blob)
    {
        var curve = CurveOf(keyType);
        var curveName = reader.ReadAsciiString();
        var point = reader.ReadString();
        reader.ExpectEnd();
        if (!string.Equals(curveName, CurveName(curve), StringComparison.Ordinal) || !EcdsaPublicKey.TryCreate(curve, point, out var key))
        {
            throw new SshProtocolException("An ECDSA key is malformed.", DisconnectReason.HostKeyNotVerifiable);
        }

        return new EcdsaSshKey(keyType, key!, blob.ToArray());
    }

    protected override bool Verify(string algorithm, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (!string.Equals(algorithm, KeyType, StringComparison.Ordinal))
        {
            return false;
        }

        var reader = new SshReader(signature);
        var r = reader.ReadMPIntUnsigned();
        var s = reader.ReadMPIntUnsigned();
        reader.ExpectEnd();
        var size = NistCurves.CoordinateSize(_key.Curve);
        if (r.IsEmpty || s.IsEmpty || r.Length > size || s.Length > size)
        {
            return false;
        }

        var fixedWidth = new byte[2 * size];
        r.CopyTo(fixedWidth.AsSpan(size - r.Length));
        s.CopyTo(fixedWidth.AsSpan((2 * size) - s.Length));
        return _key.Verify(data, fixedWidth);
    }
}
