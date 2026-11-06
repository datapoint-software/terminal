using System.Numerics;
using System.Security.Cryptography;

namespace Datapoint.Terminal.Security.Cryptography;

public static class NistCurves
{
    public static ECCurve CurveOf(NistCurve curve) => curve switch
    {
        NistCurve.P256 => ECCurve.NamedCurves.nistP256,
        NistCurve.P384 => ECCurve.NamedCurves.nistP384,
        _ => ECCurve.NamedCurves.nistP521,
    };

    public static int CoordinateSize(NistCurve curve) => curve switch
    {
        NistCurve.P256 => 32,
        NistCurve.P384 => 48,
        _ => 66,
    };

    public static HashKind HashOf(NistCurve curve) => curve switch
    {
        NistCurve.P256 => HashKind.Sha256,
        NistCurve.P384 => HashKind.Sha384,
        _ => HashKind.Sha512,
    };

    public static bool TryDecodePoint(NistCurve curve, ReadOnlySpan<byte> encoded, out ECPoint point)
    {
        point = default;
        var size = CoordinateSize(curve);
        if (encoded.Length != 1 + (2 * size) || encoded[0] != 0x04)
        {
            return false;
        }

        var x = encoded.Slice(1, size).ToArray();
        var y = encoded.Slice(1 + size, size).ToArray();
        if (!IsOnCurve(curve, x, y))
        {
            return false;
        }

        point = new ECPoint { X = x, Y = y };
        return true;
    }

    public static byte[] EncodePoint(ECPoint point)
    {
        var encoded = new byte[1 + point.X!.Length + point.Y!.Length];
        encoded[0] = 0x04;
        point.X.CopyTo(encoded, 1);
        point.Y.CopyTo(encoded, 1 + point.X.Length);
        return encoded;
    }

    private static bool IsOnCurve(NistCurve curve, byte[] xBytes, byte[] yBytes)
    {
        var (prime, b) = Constants(curve);
        var x = new BigInteger(xBytes, isUnsigned: true, isBigEndian: true);
        var y = new BigInteger(yBytes, isUnsigned: true, isBigEndian: true);
        if (x >= prime || y >= prime || (x.IsZero && y.IsZero))
        {
            return false;
        }

        var left = BigInteger.ModPow(y, 2, prime);
        var right = ((BigInteger.ModPow(x, 3, prime) - (3 * x) + b) % prime + prime) % prime;
        return left == right;
    }

    private static (BigInteger Prime, BigInteger B) Constants(NistCurve curve) => curve switch
    {
        NistCurve.P256 => (
            Parse("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF"),
            Parse("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B")),
        NistCurve.P384 => (
            Parse("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFF"),
            Parse("B3312FA7E23EE7E4988E056BE3F82D19181D9C6EFE8141120314088F5013875AC656398D8A2ED19D2A85C8EDD3EC2AEF")),
        _ => (
            BigInteger.Pow(2, 521) - 1,
            Parse("0051953EB9618E1C9A1F929A21A0B68540EEA2DA725B99B315F3B8B489918EF109E156193951EC7E937B1652C0BD3BB1BF073573DF883D2C34F1EF451FD46B503F00")),
    };

    private static BigInteger Parse(string hex) =>
        new(Convert.FromHexString(hex), isUnsigned: true, isBigEndian: true);
}
