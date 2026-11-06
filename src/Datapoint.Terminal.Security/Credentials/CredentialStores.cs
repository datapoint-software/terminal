namespace Datapoint.Terminal.Security.Credentials;

public static class CredentialStores
{
    /// <summary>The operating system's store, or null when this system has none the application can use.</summary>
    public static ICredentialStore? ForThisSystem()
    {
        if (OperatingSystem.IsMacOS())
        {
            return new KeychainStore();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialStore();
        }

        return OperatingSystem.IsLinux() && SecretToolStore.IsInstalled() ? new SecretToolStore() : null;
    }
}
