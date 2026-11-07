using System.Text;
using Datapoint.Terminal.Agent.Projects;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    private IReadOnlyList<ProjectConnection> _connections = [];
    private string? _keptHeld;

    // Connections as they apply: the confirmed settings, plus whatever changes from outside made stricter.
    private IReadOnlyList<ProjectConnection> Connections => _connections;

    // Product decision 92. Runs when the window opens and again before connections are listed, so a git pull made while the
    // window is open is caught too. A review the user put off isn't shown again until the project is next opened.
    private async Task ReviewConnectionsAsync(bool reload)
    {
        if (reload)
        {
            foreach (var problem in Project.ReloadSettings())
            {
                _chat.AddNotice(problem);
            }
        }

        var current = Project.Settings.Connections;
        if (_settings.ConfirmedFor(Project.Folder) is not { } confirmed)
        {
            Confirm(current);
            return;
        }

        var review = ConnectionReview.Compare(current, confirmed, _settings.Current.DefaultApprovalMode);
        ReportStricter(confirmed, review.Effective);
        if (review.Held.Count == 0)
        {
            Confirm(review.Effective);
            return;
        }

        var details = Describe(review.Held);
        _settings.SetConfirmed(Project.Folder, review.Effective);
        _connections = review.Effective;
        RefreshSidebarHosts();
        if (string.Equals(details, _keptHeld, StringComparison.Ordinal))
        {
            return;
        }

        var choice = await ChoiceWindow.AskAsync(
            this,
            Strings.ReviewTitle,
            Strings.Format(Strings.ReviewMessage, Project.Name),
            details,
            hasDefault: false,
            Strings.ReviewKeep,
            Strings.ReviewAccept).ConfigureAwait(true);
        if (choice == 1)
        {
            Confirm(current);
            return;
        }

        _keptHeld = details;
        _chat.AddNotice(Strings.NoticeConnectionsHeld);
    }

    private void Confirm(IReadOnlyList<ProjectConnection> connections)
    {
        _connections = connections;
        _settings.SetConfirmed(Project.Folder, connections);
        RefreshSidebarHosts();
    }

    private void ReportStricter(IReadOnlyList<ProjectConnection> confirmed, IReadOnlyList<ProjectConnection> effective)
    {
        var changed = confirmed
            .Where(previous => effective.FirstOrDefault(item => string.Equals(item.Name, previous.Name, StringComparison.OrdinalIgnoreCase)) is not { } now
                || !ConnectionReview.IsSameGuarded(now, previous))
            .Select(previous => previous.Name)
            .ToList();
        if (changed.Count == 0)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeConnectionsStricter, string.Join(", ", changed)));
    }

    private static string Describe(IReadOnlyList<ConnectionChange> changes)
    {
        var text = new StringBuilder();
        foreach (var change in changes)
        {
            text.AppendLine(string.Equals(change.Setting, ConnectionReview.NewConnection, StringComparison.Ordinal)
                ? Strings.Format(Strings.ReviewNewConnection, change.Connection, change.Proposed)
                : Strings.Format(Strings.ReviewChange, change.Connection, change.Setting, Value(change), Value(change, proposed: true)));
        }

        return text.ToString().TrimEnd();
    }

    private static string Value(ConnectionChange change, bool proposed = false)
    {
        var value = proposed ? change.Proposed : change.Confirmed;
        if (value is not null)
        {
            return value;
        }

        return string.Equals(change.Setting, ConnectionReview.ApprovalSetting, StringComparison.Ordinal) ? Strings.ReviewDefault : Strings.ReviewNone;
    }
}
