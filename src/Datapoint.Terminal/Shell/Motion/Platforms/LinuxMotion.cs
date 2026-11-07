using System.Diagnostics;
using System.Runtime.Versioning;

namespace Datapoint.Terminal.Shell.Motion.Platforms;

// GNOME's Settings › Accessibility › Reduce animation, which other desktops built on GSettings follow too. Read once, as it
// takes a process; a desktop without gsettings animates unless Appearance says otherwise.
[SupportedOSPlatform("linux")]
internal static class LinuxMotion
{
    private static readonly Lazy<bool> s_isReduced = new(Read);

    public static bool IsReduced() => s_isReduced.Value;

    private static bool Read()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("gsettings", ["get", "org.gnome.desktop.interface", "enable-animations"])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return false;
            }

            return string.Equals(output.Trim(), "false", StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
