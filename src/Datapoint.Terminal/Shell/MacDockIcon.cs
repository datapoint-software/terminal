using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell;

// Product decision 149. macOS takes the Dock's icon from the app bundle, and Avalonia doesn't pass a window's icon on to it,
// so outside a bundle the Dock showed a plain executable. The .icns, drawn on Apple's grid, is handed to the application
// instead; a bundle's own icon would show the same.
[SupportedOSPlatform("macos")]
internal static partial class MacDockIcon
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    private const string Resource = "Datapoint.Terminal.Branding.terminal-icon.icns";

    public static void Apply()
    {
        using var stream = typeof(MacDockIcon).Assembly.GetManifestResourceStream(Resource);
        if (stream is null)
        {
            return;
        }

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        var application = Send(Class("NSApplication"), Selector("sharedApplication"));
        if (application == IntPtr.Zero)
        {
            return;
        }

        // NSData copies the bytes; the image is kept for as long as the application runs, so it's never released.
        var data = Send(Class("NSData"), Selector("dataWithBytes:length:"), bytes, (nuint)bytes.Length);
        var image = Send(Send(Class("NSImage"), Selector("alloc")), Selector("initWithData:"), data);
        if (image == IntPtr.Zero)
        {
            return;
        }

        _ = Send(application, Selector("setApplicationIconImage:"), image);
    }

    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Class(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr Selector(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr Send(IntPtr receiver, IntPtr selector, byte[] bytes, nuint length);
}
