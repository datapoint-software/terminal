using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Security.Credentials;

/// <summary>The parts of Security.framework and Core Foundation the Keychain store needs.</summary>
[SupportedOSPlatform("macos")]
internal static partial class Keychain
{
    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private static readonly IntPtr s_security = NativeLibrary.Load(SecurityLibrary);
    private static readonly IntPtr s_coreFoundation = NativeLibrary.Load(CoreFoundationLibrary);

    public static IntPtr Class { get; } = Constant(s_security, "kSecClass");

    public static IntPtr ClassGenericPassword { get; } = Constant(s_security, "kSecClassGenericPassword");

    public static IntPtr Service { get; } = Constant(s_security, "kSecAttrService");

    public static IntPtr Account { get; } = Constant(s_security, "kSecAttrAccount");

    public static IntPtr Label { get; } = Constant(s_security, "kSecAttrLabel");

    public static IntPtr ValueData { get; } = Constant(s_security, "kSecValueData");

    public static IntPtr ReturnData { get; } = Constant(s_security, "kSecReturnData");

    public static IntPtr MatchLimit { get; } = Constant(s_security, "kSecMatchLimit");

    public static IntPtr MatchLimitOne { get; } = Constant(s_security, "kSecMatchLimitOne");

    public static IntPtr True { get; } = Constant(s_coreFoundation, "kCFBooleanTrue");

    /// <summary>The callback structures themselves, passed by address.</summary>
    public static IntPtr DictionaryKeyCallBacks { get; } = NativeLibrary.GetExport(s_coreFoundation, "kCFTypeDictionaryKeyCallBacks");

    public static IntPtr DictionaryValueCallBacks { get; } = NativeLibrary.GetExport(s_coreFoundation, "kCFTypeDictionaryValueCallBacks");

    [LibraryImport(SecurityLibrary)]
    public static partial int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [LibraryImport(SecurityLibrary)]
    public static partial int SecItemAdd(IntPtr attributes, IntPtr result);

    [LibraryImport(SecurityLibrary)]
    public static partial int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [LibraryImport(SecurityLibrary)]
    public static partial int SecItemDelete(IntPtr query);

    [LibraryImport(CoreFoundationLibrary, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CFStringCreateWithCharacters(IntPtr allocator, string characters, nint length);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial IntPtr CFDictionaryCreate(IntPtr allocator, IntPtr[] keys, IntPtr[] values, nint count, IntPtr keyCallBacks, IntPtr valueCallBacks);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial nint CFDataGetLength(IntPtr data);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial IntPtr CFDataGetBytePtr(IntPtr data);

    [LibraryImport(CoreFoundationLibrary)]
    public static partial void CFRelease(IntPtr value);

    /// <summary>Reads a constant the framework exports as a global variable holding a Core Foundation reference.</summary>
    private static IntPtr Constant(IntPtr library, string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));
}
