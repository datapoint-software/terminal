using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Motion.Platforms;

// Settings › Accessibility › Visual effects › Animation effects.
[SupportedOSPlatform("windows")]
internal static partial class WindowsMotion
{
    private const uint GetClientAreaAnimation = 0x1042;

    public static bool IsReduced()
    {
        var isOn = 1;
        return SystemParametersInfo(GetClientAreaAnimation, 0, ref isOn, 0) && isOn == 0;
    }

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint parameter, ref int value, uint flags);
}
