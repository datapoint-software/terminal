namespace Datapoint.Terminal.Security.Credentials;

public static class CredentialStores
{
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
