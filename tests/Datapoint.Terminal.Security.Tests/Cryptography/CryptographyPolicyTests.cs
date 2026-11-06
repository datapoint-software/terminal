using Datapoint.Terminal.Security.Cryptography;

namespace Datapoint.Terminal.Security.Tests.Cryptography;

public sealed class CryptographyPolicyTests
{
    [Fact]
    public void Fips_mode_allows_only_approved_algorithms_from_the_operating_system()
    {
        Assert.True(CryptographyPolicy.Fips.Allows(AesGcmCipher.Info));
        Assert.True(CryptographyPolicy.Fips.Allows(EcdhKeyPair.InfoFor(NistCurve.P384)));
        Assert.False(CryptographyPolicy.Fips.Allows(X25519KeyPair.Info));
        Assert.False(CryptographyPolicy.Fips.Allows(ChaCha20.Info));
        Assert.False(CryptographyPolicy.Fips.Allows(MlKem768KeyPair.Info));
    }

    [Fact]
    public void Standard_mode_allows_everything()
    {
        Assert.True(CryptographyPolicy.Standard.Allows(X25519KeyPair.Info));
    }
}
