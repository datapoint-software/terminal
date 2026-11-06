using System.Numerics;
using System.Text;
using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Sessions.Ssh.Protocol;
using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Ssh.Keys;

// Private keys in OpenSSH's own format (what ssh-keygen writes). Keys protected by a passphrase are reported rather than
// read: until passphrases are supported here, they work through the user's SSH agent (ssh-add).
public static class OpenSshKeyFile
{
    private const string Begin = "-----BEGIN OPENSSH PRIVATE KEY-----";
    private const string End = "-----END OPENSSH PRIVATE KEY-----";
    private const int MaxFileBytes = 64 * 1024;
    private static readonly byte[] s_magic = "openssh-key-v1\0"u8.ToArray();

    public static byte[]? ReadPublicKeyBlob(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            var fields = File.ReadAllText(path).Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            return fields.Length >= 2 ? Convert.FromBase64String(fields[1]) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    public static OpenSshKeyResult Load(string path)
    {
        string text;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return new OpenSshKeyResult(null, null);
            }

            if (info.Length > MaxFileBytes)
            {
                return new OpenSshKeyResult(null, $"{path} is too large to be a private key.");
            }

            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new OpenSshKeyResult(null, $"{path} couldn't be read: {exception.Message}");
        }

        var start = text.IndexOf(Begin, StringComparison.Ordinal);
        var end = text.IndexOf(End, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return new OpenSshKeyResult(null, $"{path} isn't in OpenSSH's key format, which is the only one read so far. Add it to your SSH agent with ssh-add instead.");
        }

        var buffer = new byte[end - start];
        var isDecoded = Convert.TryFromBase64String(text[(start + Begin.Length)..end].Trim(), buffer, out var length);
        var data = buffer[..length];
        SecretMemory.Wipe(buffer);
        if (!isDecoded)
        {
            return new OpenSshKeyResult(null, $"{path} is damaged.");
        }

        try
        {
            return Parse(data, path);
        }
        catch (Exception exception) when (exception is SshProtocolException or ArgumentException or FormatException)
        {
            return new OpenSshKeyResult(null, $"{path} couldn't be read as a private key: {exception.Message}");
        }
        finally
        {
            SecretMemory.Wipe(data);
        }
    }

    private static OpenSshKeyResult Parse(byte[] data, string path)
    {
        if (!data.AsSpan().StartsWith(s_magic))
        {
            return new OpenSshKeyResult(null, $"{path} isn't an OpenSSH private key.");
        }

        var reader = new SshReader(data.AsSpan(s_magic.Length));
        var cipher = reader.ReadAsciiString();
        reader.ReadAsciiString();
        reader.ReadString();
        if (reader.ReadUInt32() != 1)
        {
            return new OpenSshKeyResult(null, $"{path} holds more than one key, which isn't supported.");
        }

        reader.ReadString();
        if (!string.Equals(cipher, "none", StringComparison.Ordinal))
        {
            return new OpenSshKeyResult(null, $"{path} is protected by a passphrase. Add it to your SSH agent with ssh-add to use it here; passphrases can't be entered in Datapoint Terminal yet.", IsEncrypted: true);
        }

        var section = new SshReader(reader.ReadString());
        if (section.ReadUInt32() != section.ReadUInt32())
        {
            return new OpenSshKeyResult(null, $"{path} is damaged.");
        }

        var keyType = section.ReadAsciiString();
        ISshSigner signer = keyType switch
        {
            AlgorithmNames.Ed25519 => ReadEd25519(ref section),
            AlgorithmNames.EcdsaP256 or AlgorithmNames.EcdsaP384 or AlgorithmNames.EcdsaP521 => ReadEcdsa(ref section, keyType),
            AlgorithmNames.RsaSha1 => ReadRsa(ref section),
            _ => throw new FormatException($"{keyType} keys aren't supported."),
        };
        return new OpenSshKeyResult(signer, null) { Comment = Encoding.UTF8.GetString(section.ReadString()) };
    }

    private static InMemorySigner ReadEd25519(ref SshReader reader)
    {
        reader.ReadString();
        var secret = reader.ReadString();
        if (secret.Length != 64)
        {
            throw new FormatException("The Ed25519 key has the wrong length.");
        }

        return InMemorySigner.ForEd25519(Ed25519PrivateKey.FromSeed(secret[..32]));
    }

    private static InMemorySigner ReadEcdsa(ref SshReader reader, string keyType)
    {
        reader.ReadAsciiString();
        var point = reader.ReadString();
        var scalar = reader.ReadMPIntUnsigned();
        return InMemorySigner.ForEcdsa(EcdsaPrivateKey.FromParameters(EcdsaSshKey.CurveOf(keyType), point, scalar));
    }

    private static InMemorySigner ReadRsa(ref SshReader reader)
    {
        var modulus = reader.ReadMPIntUnsigned().ToArray();
        var exponent = reader.ReadMPIntUnsigned().ToArray();
        var d = reader.ReadMPIntUnsigned().ToArray();
        var inverseQ = reader.ReadMPIntUnsigned().ToArray();
        var p = reader.ReadMPIntUnsigned().ToArray();
        var q = reader.ReadMPIntUnsigned().ToArray();
        var half = (modulus.Length + 1) / 2;
        var dValue = new BigInteger(d, isUnsigned: true, isBigEndian: true);
        var dp = (dValue % (new BigInteger(p, isUnsigned: true, isBigEndian: true) - 1)).ToByteArray(isUnsigned: true, isBigEndian: true);
        var dq = (dValue % (new BigInteger(q, isUnsigned: true, isBigEndian: true) - 1)).ToByteArray(isUnsigned: true, isBigEndian: true);
        var parameters = new RsaKeyParameters(modulus, exponent, Pad(d, modulus.Length), Pad(p, half), Pad(q, half), Pad(dp, half), Pad(dq, half), Pad(inverseQ, half));
        try
        {
            return InMemorySigner.ForRsa(RsaPrivateKey.FromParameters(parameters));
        }
        finally
        {
            foreach (var secret in (byte[][])[d, p, q, dp, dq, inverseQ, parameters.PrivateExponent, parameters.P, parameters.Q, parameters.DP, parameters.DQ, parameters.InverseQ])
            {
                SecretMemory.Wipe(secret);
            }
        }
    }

    private static byte[] Pad(byte[] value, int length)
    {
        if (value.Length >= length)
        {
            return value;
        }

        var padded = new byte[length];
        value.CopyTo(padded, length - value.Length);
        return padded;
    }
}
