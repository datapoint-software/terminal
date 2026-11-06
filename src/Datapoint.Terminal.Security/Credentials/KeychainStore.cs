using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Datapoint.Terminal.Security.Credentials;

/// <summary>
/// Generic passwords in the user's login keychain, through Security.framework's SecItem functions. The first time another
/// build of the application reads an item, macOS asks the user to allow it.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainStore : ICredentialStore
{
    private const int ItemNotFound = -25300;

    public string Name => "Keychain";

    public string? Read(string service, string account)
    {
        using var query = Query(service, account, (Keychain.ReturnData, Keychain.True), (Keychain.MatchLimit, Keychain.MatchLimitOne));
        var status = Keychain.SecItemCopyMatching(query.Handle, out var result);
        if (status == ItemNotFound)
        {
            return null;
        }

        Check(status, "read");
        try
        {
            var length = (int)Keychain.CFDataGetLength(result);
            var bytes = new byte[length];
            Marshal.Copy(Keychain.CFDataGetBytePtr(result), bytes, 0, length);
            var secret = Encoding.UTF8.GetString(bytes);
            Array.Clear(bytes);
            return secret;
        }
        finally
        {
            Keychain.CFRelease(result);
        }
    }

    public void Save(string service, string account, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var bytes = Encoding.UTF8.GetBytes(secret);
        try
        {
            using var data = CFObject.Data(bytes);
            using var query = Query(service, account);
            using var changes = CFObject.Dictionary((Keychain.ValueData, data.Handle));
            var status = Keychain.SecItemUpdate(query.Handle, changes.Handle);
            if (status != ItemNotFound)
            {
                Check(status, "save");
                return;
            }

            using var label = CFObject.String($"{service} ({account})");
            using var item = Query(service, account, (Keychain.ValueData, data.Handle), (Keychain.Label, label.Handle));
            Check(Keychain.SecItemAdd(item.Handle, IntPtr.Zero), "save");
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    public bool Delete(string service, string account)
    {
        using var query = Query(service, account);
        var status = Keychain.SecItemDelete(query.Handle);
        if (status == ItemNotFound)
        {
            return false;
        }

        Check(status, "delete");
        return true;
    }

    private static CFObject Query(string service, string account, params (IntPtr Key, IntPtr Value)[] extra)
    {
        ArgumentException.ThrowIfNullOrEmpty(service);
        ArgumentException.ThrowIfNullOrEmpty(account);
        using var serviceString = CFObject.String(service);
        using var accountString = CFObject.String(account);
        (IntPtr, IntPtr)[] pairs =
        [
            (Keychain.Class, Keychain.ClassGenericPassword),
            (Keychain.Service, serviceString.Handle),
            (Keychain.Account, accountString.Handle),
            .. extra,
        ];
        return CFObject.Dictionary(pairs);
    }

    private static void Check(int status, string action)
    {
        if (status == 0)
        {
            return;
        }

        var reason = status switch
        {
            -128 => "you cancelled the request",
            -25293 => "access was denied",
            -25308 => "the keychain is locked or can't ask you for permission",
            _ => $"error {status}",
        };
        throw new CredentialStoreException($"The Keychain couldn't {action} the secret: {reason}.");
    }

    /// <summary>A Core Foundation object released when disposed. Dictionaries retain what they hold.</summary>
    private sealed class CFObject(IntPtr handle) : IDisposable
    {
        public IntPtr Handle { get; } = handle != IntPtr.Zero ? handle : throw new CredentialStoreException("Core Foundation couldn't create an object.");

        public static CFObject String(string value) => new(Keychain.CFStringCreateWithCharacters(IntPtr.Zero, value, value.Length));

        public static CFObject Data(byte[] bytes) => new(Keychain.CFDataCreate(IntPtr.Zero, bytes, bytes.Length));

        public static CFObject Dictionary(params (IntPtr Key, IntPtr Value)[] pairs) => new(Keychain.CFDictionaryCreate(
            IntPtr.Zero,
            [.. pairs.Select(pair => pair.Key)],
            [.. pairs.Select(pair => pair.Value)],
            pairs.Length,
            Keychain.DictionaryKeyCallBacks,
            Keychain.DictionaryValueCallBacks));

        public void Dispose() => Keychain.CFRelease(Handle);
    }
}
