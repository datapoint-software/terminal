using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Datapoint.Terminal.Security.Credentials;

/// <summary>Generic credentials in Windows Credential Manager, protected by the user's logon (DPAPI).</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsCredentialStore : ICredentialStore
{
    private const uint GenericType = 1;
    private const uint PersistLocalMachine = 2;
    private const int NotFound = 1168;

    public string Name => "Credential Manager";

    public string? Read(string service, string account)
    {
        if (!CredRead(Target(service, account), GenericType, 0, out var pointer))
        {
            var error = Marshal.GetLastPInvokeError();
            return error == NotFound ? null : throw Failure("read", error);
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            var secret = Encoding.UTF8.GetString(bytes);
            Array.Clear(bytes);
            return secret;
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void Save(string service, string account, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        var bytes = Encoding.UTF8.GetBytes(secret);
        var target = Marshal.StringToCoTaskMemUni(Target(service, account));
        var user = Marshal.StringToCoTaskMemUni(account);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = user,
            };
            if (!CredWrite(ref credential, 0))
            {
                throw Failure("save", Marshal.GetLastPInvokeError());
            }
        }
        finally
        {
            Array.Clear(bytes);
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeCoTaskMem(blob);
            Marshal.FreeCoTaskMem(target);
            Marshal.FreeCoTaskMem(user);
        }
    }

    public bool Delete(string service, string account)
    {
        if (CredDelete(Target(service, account), GenericType, 0))
        {
            return true;
        }

        var error = Marshal.GetLastPInvokeError();
        return error == NotFound ? false : throw Failure("delete", error);
    }

    private static string Target(string service, string account)
    {
        ArgumentException.ThrowIfNullOrEmpty(service);
        ArgumentException.ThrowIfNullOrEmpty(account);
        return $"{service}/{account}";
    }

    private static CredentialStoreException Failure(string action, int error) =>
        new($"Credential Manager couldn't {action} the secret: {new Win32Exception(error).Message}");

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(ref Credential credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDelete(string target, uint type, uint flags);

    [LibraryImport("advapi32.dll")]
    private static partial void CredFree(IntPtr buffer);

    /// <summary>CREDENTIALW.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
