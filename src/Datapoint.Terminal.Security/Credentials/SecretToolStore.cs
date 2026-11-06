using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace Datapoint.Terminal.Security.Credentials;

/// <summary>
/// The Secret Service on Linux (GNOME Keyring, KWallet), through libsecret's <c>secret-tool</c>. Secrets travel over its
/// standard input and output, never its command line, where other users could see them.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class SecretToolStore : ICredentialStore
{
    private static readonly string[] s_folders = ["/usr/bin", "/usr/local/bin", "/bin"];

    public string Name => "Secret Service";

    public static bool IsInstalled() => Program is not null;

    private static string? Program => s_folders.Select(folder => Path.Combine(folder, "secret-tool")).FirstOrDefault(File.Exists);

    public string? Read(string service, string account)
    {
        var (exitCode, output) = Run(null, "lookup", "service", service, "account", account);
        return exitCode == 0 && output.Length > 0 ? output : null;
    }

    public void Save(string service, string account, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var (exitCode, _) = Run(secret, "store", $"--label={service} ({account})", "service", service, "account", account);
        if (exitCode == 0)
        {
            return;
        }

        throw new CredentialStoreException("The Secret Service couldn't save the secret. Is a keyring running and unlocked?");
    }

    public bool Delete(string service, string account)
    {
        if (Read(service, account) is null)
        {
            return false;
        }

        _ = Run(null, "clear", "service", service, "account", account);
        return true;
    }

    private static (int ExitCode, string Output) Run(string? input, params string[] arguments)
    {
        var start = new ProcessStartInfo(Program ?? throw new CredentialStoreException("secret-tool isn't installed."))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new CredentialStoreException("secret-tool couldn't be started.");
        process.StandardInput.Write(input ?? string.Empty);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            process.Kill();
            throw new CredentialStoreException("secret-tool didn't answer; the keyring may be waiting for you to unlock it.");
        }

        return (process.ExitCode, output);
    }
}
