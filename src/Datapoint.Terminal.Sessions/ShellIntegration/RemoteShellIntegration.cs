using System.Text;

namespace Datapoint.Terminal.Sessions.ShellIntegration;

// Shell integration for a shell on an SSH host, without leaving files there or putting the scripts or the nonce on a command line
// another user of the host could read. The session runs a short bootstrap in place of the login shell: it turns the terminal's
// echo off and says it's ready; the scripts and the nonce then arrive as the terminal's input, go into a folder only the user can
// read, and the shell deletes that folder as it loads them. Shells other than bash, zsh and fish, and hosts without mktemp or
// stty, get their usual login shell with no integration.
public static class RemoteShellIntegration
{
    public const string ReadyMarker = "\u001b]633;P;DatapointReady\u0007";
    public const string PlainMarker = "\u001b]633;P;DatapointPlain\u0007";
    public const string FileLine = "__DPT_FILE__ ";
    public const string EndLine = "__DPT_END__";

    // One line with no single quote, no "\\" and no "\'" inside the quotes, so the user's login shell reads it the same way
    // whether it's sh, bash, zsh, fish or csh.
    private const string Bootstrap =
        "s=${SHELL:-/bin/sh}; n=${s##*/}; p() { printf \"\\033]633;P;%s\\007\" \"$1\"; }; " +
        "case $n in bash|zsh|fish) ;; *) p DatapointPlain; exec \"$s\" -l;; esac; " +
        "command -v stty >/dev/null 2>&1 || { p DatapointPlain; exec \"$s\" -l; }; " +
        "d=$(mktemp -d 2>/dev/null) || { p DatapointPlain; exec \"$s\" -l; }; " +
        "mkdir \"$d/zsh\"; stty -echo; p DatapointReady; IFS= read -r DATAPOINT_NONCE; f=; " +
        "while IFS= read -r l; do case $l in __DPT_END__) break;; \"__DPT_FILE__ \"*) f=$d/${l#__DPT_FILE__ };; *) printf \"%s\\n\" \"$l\" >> \"$f\";; esac; done; " +
        "stty echo; export DATAPOINT_NONCE; export DATAPOINT_CLEANUP=$d; " +
        "case $n in bash) DATAPOINT_LOGIN=1 exec \"$s\" --init-file \"$d/bash.sh\";; " +
        "zsh) DATAPOINT_USER_ZDOTDIR=${ZDOTDIR:-$HOME} ZDOTDIR=$d/zsh exec \"$s\" -l;; " +
        "*) exec \"$s\" -l --init-command \"source $d/fish.fish\";; esac";

    public static string Command { get; } = "exec /bin/sh -c '" + Bootstrap + "'";

    // What the bootstrap reads once it's ready: the nonce, then each script after a line naming it.
    public static byte[] Payload(string nonce)
    {
        ArgumentException.ThrowIfNullOrEmpty(nonce);
        var text = new StringBuilder().Append(nonce).Append('\n');
        foreach (var (path, script) in ShellIntegrationInstaller.Scripts().Where(script => !script.Path.EndsWith(".ps1", StringComparison.Ordinal)))
        {
            text.Append(FileLine).Append(path).Append('\n').Append(script);
            if (!script.EndsWith('\n'))
            {
                text.Append('\n');
            }
        }

        return Encoding.UTF8.GetBytes(text.Append(EndLine).Append('\n').ToString());
    }

    // Where a marker is in what the host has sent so far: what the bootstrap printed before it is the host's own output, e.g. a
    // message of the day, and is shown as usual.
    public static (bool? IsReady, int Index, int Length) FindMarker(ReadOnlySpan<byte> output)
    {
        var ready = output.IndexOf(Encoding.ASCII.GetBytes(ReadyMarker));
        if (ready >= 0)
        {
            return (true, ready, ReadyMarker.Length);
        }

        var plain = output.IndexOf(Encoding.ASCII.GetBytes(PlainMarker));
        return plain >= 0 ? (false, plain, PlainMarker.Length) : (null, -1, 0);
    }
}
