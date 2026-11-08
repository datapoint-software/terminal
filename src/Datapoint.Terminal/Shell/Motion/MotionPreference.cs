using Datapoint.Terminal.Shell.Motion.Platforms;

namespace Datapoint.Terminal.Shell.Motion;

// Whether overlays and cards animate: not when the user turned it off in Appearance, nor while the system asks to reduce
// motion. Avalonia doesn't read the system's setting, so each platform's own is asked.
public static class MotionPreference
{
    private static bool s_isReducedInSettings;

    public static bool IsEnabled => !s_isReducedInSettings && !(SystemOverride ?? IsReducedBySystem)();

    // Tests set the system's answer, so they don't depend on the machine they run on: CI machines often reduce motion.
    internal static Func<bool>? SystemOverride { get; set; }

    public static void Apply(bool reduce) => s_isReducedInSettings = reduce;

    private static bool IsReducedBySystem()
    {
        if (OperatingSystem.IsMacOS())
        {
            return MacMotion.IsReduced();
        }

        if (OperatingSystem.IsWindows())
        {
            return WindowsMotion.IsReduced();
        }

        return OperatingSystem.IsLinux() && LinuxMotion.IsReduced();
    }
}
