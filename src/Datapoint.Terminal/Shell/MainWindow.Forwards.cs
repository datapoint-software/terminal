using System.Net.Sockets;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Shell;

// Product decision 46, port forwarding: each forward is announced in the chat, so the user knows a port on this computer now
// leads into the host's network. A port that's taken is reported and skipped; the tab opens anyway.
public sealed partial class MainWindow
{
    private void StartForwards(TerminalTab tab, SshClient client, IReadOnlyList<string> forwards)
    {
        foreach (var text in forwards.Distinct(StringComparer.Ordinal))
        {
            if (SshForward.Parse(text) is not { } forward)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeForwardUnreadable, tab.ConnectionName, text));
                continue;
            }

            if (forward.AsksForNetwork)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeForwardLoopbackOnly, forward.BindAddress, forward.LocalPort));
            }

            try
            {
                tab.Forwards.Add(SshLocalForward.Start(client, forward.LocalPort, forward.Host, forward.Port));
                _chat.AddNotice(Strings.Format(Strings.NoticeForwarding, forward.LocalPort, forward.Host, forward.Port, tab.Name));
            }
            catch (SocketException exception)
            {
                _chat.AddNotice(Strings.Format(Strings.NoticeForwardFailed, forward.LocalPort, exception.Message));
            }
        }
    }
}
