namespace Datapoint.Terminal.Security.Cryptography;

public sealed class CryptographyPolicy(bool isFipsMode)
{
    public static CryptographyPolicy Standard { get; } = new(isFipsMode: false);

    public static CryptographyPolicy Fips { get; } = new(isFipsMode: true);

    public bool IsFipsMode { get; } = isFipsMode;

    public bool Allows(AlgorithmInfo algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);
        return !IsFipsMode || (algorithm.IsFipsApproved && algorithm.Source == CryptographySource.DotNet);
    }

    public static bool IsOperatingSystemInFipsMode() => OperatingSystem.IsLinux() && ReadLinuxFipsFlag();

    private static bool ReadLinuxFipsFlag()
    {
        const string flag = "/proc/sys/crypto/fips_enabled";
        return File.Exists(flag) && File.ReadAllText(flag).Trim() is "1";
    }
}
