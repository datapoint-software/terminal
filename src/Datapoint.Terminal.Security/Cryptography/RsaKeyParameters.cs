namespace Datapoint.Terminal.Security.Cryptography;

public sealed record RsaKeyParameters(
    byte[] Modulus,
    byte[] PublicExponent,
    byte[] PrivateExponent,
    byte[] P,
    byte[] Q,
    byte[] DP,
    byte[] DQ,
    byte[] InverseQ);
