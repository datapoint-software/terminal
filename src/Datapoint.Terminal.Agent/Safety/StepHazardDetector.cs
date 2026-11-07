using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Agent.Safety;

// The app's own look at a plan step, so a risky step is flagged even when the agent doesn't say so. A heuristic on the
// command text: it can miss a hazard hidden in a script, and it never makes a step safer than the approval mode says.
public static partial class StepHazardDetector
{
    public static StepHazards Detect(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hazards = StepHazards.None;
        if (EndsConnection().IsMatch(command))
        {
            hazards |= StepHazards.EndsConnection;
        }

        if (ErasesDisk().IsMatch(command))
        {
            hazards |= StepHazards.ErasesDisk;
        }

        if (DeletesRecursively().IsMatch(command))
        {
            hazards |= StepHazards.DeletesRecursively;
        }

        if (CommandClassifier.Classify(command) == CommandRisk.AlwaysAsk)
        {
            hazards |= StepHazards.ChangesTrust;
        }

        return hazards;
    }

    [GeneratedRegex(@"(^|[;&|(])\s*(sudo\s+)?(reboot|shutdown|poweroff|halt|init\s+[06]|systemctl\s+(reboot|poweroff|halt|kexec)|systemctl\s+(restart|stop)\s+(sshd?|networking|NetworkManager|systemd-networkd))\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex EndsConnection();

    [GeneratedRegex(@"(^|[;&|(])\s*(sudo\s+)?(mkfs(\.\w+)?|wipefs|fdisk|sfdisk|parted|dd\s[^;&|]*\bof=/dev/)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex ErasesDisk();

    [GeneratedRegex(@"(^|[;&|(])\s*(sudo\s+)?rm\s+(-\w*[rR]\w*|--recursive)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 200)]
    private static partial Regex DeletesRecursively();
}
