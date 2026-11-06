using Datapoint.Terminal.Sessions.Ssh.Agent;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Config;
using Datapoint.Terminal.Sessions.Ssh.Keys;

namespace Datapoint.Terminal.Sessions.Ssh;

public static class SshLoginPlan
{
    private static readonly string[] s_defaultKeys = ["id_rsa", "id_ecdsa", "id_ed25519"];

    // A setting that changes where or how we connect, and that isn't supported yet, stops the connection with its name rather
    // than being ignored (engineering decision 5).
    public static string? Unsupported(SshHostConfig host)
    {
        ArgumentNullException.ThrowIfNull(host);
        string[] unsupported = ["ProxyJump", "LocalForward", "RemoteForward", "DynamicForward", "CertificateFile", "PKCS11Provider"];
        var setting = unsupported.FirstOrDefault(name => host.Get(name) is { } value && !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
            ?? (host.Get("ProxyCommand") is { } proxy && !string.Equals(proxy, "none", StringComparison.OrdinalIgnoreCase) ? "ProxyCommand" : null)
            ?? (host.IsYes("ForwardAgent") ? "ForwardAgent" : null)
            ?? (host.IsYes("GSSAPIAuthentication") ? "GSSAPIAuthentication" : null);
        return setting is null ? null : $"{setting} in your SSH configuration for {host.Alias} isn't supported yet, so the connection wasn't opened.";
    }

    public static async Task<(IReadOnlyList<ISshSigner> Keys, IReadOnlyList<string> Notes)> FindKeysAsync(SshHostConfig host, string home, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var notes = new List<string>();
        var explicitFiles = host.IdentityFiles.Count > 0;
        var files = explicitFiles ? host.IdentityFiles : [.. s_defaultKeys.Select(name => Path.Combine(home, ".ssh", name))];
        var filePublicKeys = files.Select(file => OpenSshKeyFile.ReadPublicKeyBlob(file + ".pub")).OfType<byte[]>().ToList();
        var agentKeys = await ListAgentKeysAsync(host, notes, cancellationToken).ConfigureAwait(false);
        if (host.IsYes("IdentitiesOnly"))
        {
            agentKeys = [.. agentKeys.Where(key => filePublicKeys.Any(blob => key.PublicKeyBlob.Span.SequenceEqual(blob)))];
        }

        var keys = new List<ISshSigner>(agentKeys);
        foreach (var file in files)
        {
            var publicKey = OpenSshKeyFile.ReadPublicKeyBlob(file + ".pub");
            if (publicKey is not null && agentKeys.Any(key => key.PublicKeyBlob.Span.SequenceEqual(publicKey)))
            {
                continue;
            }

            var result = OpenSshKeyFile.Load(file);
            if (result.Signer is not null)
            {
                keys.Add(result.Signer);
            }
            else if (result.Problem is not null && (explicitFiles || result.IsEncrypted))
            {
                notes.Add(result.Problem);
            }
        }

        return (keys, notes);
    }

    private static async Task<IReadOnlyList<SshAgentSigner>> ListAgentKeysAsync(SshHostConfig host, List<string> notes, CancellationToken cancellationToken)
    {
        var agent = SshAgentClient.Find(host.Get("IdentityAgent"));
        if (agent is null)
        {
            return [];
        }

        try
        {
            return await agent.ListKeysAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SshAgentException exception)
        {
            notes.Add(exception.Message);
            return [];
        }
    }
}
