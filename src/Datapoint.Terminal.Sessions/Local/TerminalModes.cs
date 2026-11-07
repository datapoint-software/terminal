using System.Runtime.InteropServices;

namespace Datapoint.Terminal.Sessions.Local;

// The pseudo-terminal's input modes, as the program reading from it last set them.
internal static partial class TerminalModes
{
    // Line-by-line input with echo off is what a password prompt sets (sudo, ssh, passwd, read -s); a shell's line editor and
    // full-screen programs turn line-by-line input off instead. Null where the modes can't be read, e.g. on Windows.
    public static bool? IsInputHidden(int descriptor)
    {
        if (descriptor < 0 || !(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
        {
            return null;
        }

        var termios = new byte[256];
        if (GetAttributes(descriptor, termios) != 0)
        {
            return null;
        }

        // c_lflag follows c_iflag, c_oflag and c_cflag: 8-byte flags on macOS, 4-byte on Linux, with different bit values.
        var (flags, echo, canonical) = OperatingSystem.IsMacOS()
            ? (BitConverter.ToUInt64(termios, 24), 0x8UL, 0x100UL)
            : (BitConverter.ToUInt32(termios, 12), 0x8UL, 0x2UL);
        return (flags & canonical) != 0 && (flags & echo) == 0;
    }

    [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static partial int GetAttributes(int descriptor, [Out] byte[] termios);
}
