using System.Globalization;
using System.Text;
using Datapoint.Terminal.Agent.Workspaces.History;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions;

namespace Datapoint.Terminal.Shell;

// Product decision 52: a terminal's output is kept when it closes, and shown, dimmed and marked as not live, above the prompt
// the next time a tab opens for the same connection; the chat recalls what the agent last did there.
public sealed partial class MainWindow
{
    // Earlier output is shown once per connection in a window, not again in every split or new tab.
    private readonly HashSet<string> _recalled = new(StringComparer.OrdinalIgnoreCase);

    private TerminalConnection Connect(TerminalTab tab, ITerminalSession session)
    {
        var earlier = tab.ConnectionName is { } name && _recalled.Add(name) ? Recall(name) : null;
        return new TerminalConnection(session, 80, 24, earlier is null ? default : Prelude(earlier));
    }

    // Read without creating the history file; any failure just means nothing is shown.
    private SavedScrollback? Recall(string connection)
    {
        try
        {
            // The earlier session shows in the terminal itself; while the agent looks at the host, the chat stays on the look.
            var work = WithHistory(store => store.LastWorkOn(connection), null);
            if (work is { } last && !_chat.IsLookingAt(connection))
            {
                var when = Describe(last.Time).ToLower(CultureInfo.CurrentCulture);
                var text = last.Reply is { } reply
                    ? Strings.Format(Strings.NoticeEarlierWork, connection, when, Shorten(reply))
                    : Strings.Format(Strings.NoticeEarlierWorkNoReply, connection, when);
                _chat.AddNotice(text, Strings.HistoryShowConversation, () => ShowHistory(last.Conversation));
            }

            return WithHistory(store => store.LatestScrollback(connection), null);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    // Only printable text goes back into the terminal, faint, so nothing saved can act as a control sequence when replayed.
    private static byte[] Prelude(SavedScrollback earlier)
    {
        var text = new StringBuilder("\e[2m");
        foreach (var line in earlier.Text.TrimEnd().Split('\n'))
        {
            text.Append(string.Concat(line.Where(character => !char.IsControl(character)))).Append("\r\n");
        }

        text.Append("\e[22m\r\n\e[2m── ").Append(Strings.Format(Strings.ScrollbackDivider, Describe(earlier.Time))).Append(" ──\e[22m\r\n\r\n");
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private void SaveScrollback(TerminalTab tab)
    {
        if (Workspace.IsReadOnly || tab.ConnectionName is not { } name || tab.Connection is not { } connection)
        {
            return;
        }

        string text;
        var terminal = connection.Terminal;
        lock (terminal.SyncRoot)
        {
            text = terminal.GetLineText(connection.LiveFromLine, terminal.Primary.FirstVisibleLine + terminal.Rows - 1);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lock (_historyLock)
        {
            if (_isHistoryUnavailable)
            {
                return;
            }

            try
            {
                OpenHistory().SaveScrollback(name, DateTimeOffset.Now, text);
            }
            catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or HistoryException or IOException or UnauthorizedAccessException)
            {
                StopHistory(exception.Message);
            }
        }
    }

    private static string Describe(DateTimeOffset time)
    {
        var local = time.ToLocalTime();
        var clock = local.ToString("t", CultureInfo.CurrentCulture);
        var days = (DateTime.Today - local.Date).Days;
        return days switch
        {
            0 => Strings.Format(Strings.WhenToday, clock),
            1 => Strings.Format(Strings.WhenYesterday, clock),
            _ => Strings.Format(Strings.WhenOnDay, local.ToString("d", CultureInfo.CurrentCulture), clock),
        };
    }

    private static string Shorten(string reply)
    {
        var line = reply.ReplaceLineEndings(" ").Trim();
        return line.Length <= 200 ? line : line[..199].TrimEnd() + "…";
    }
}
