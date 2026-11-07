using Avalonia.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Product decision 44: search in a terminal's scrollback. Enter finds the next older match, Shift+Enter the next newer one.
public sealed partial class MainWindow
{
    private void SetUpFind()
    {
        FindBox.TextChanged += (_, _) => FindNext(older: true, fromEnd: true);
        FindOlder.Click += (_, _) => FindNext(older: true);
        FindNewer.Click += (_, _) => FindNext(older: false);
        FindClose.Click += (_, _) => CloseFind();
        FindBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                FindNext(older: !e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseFind();
            }
        };
    }

    private void OpenFind()
    {
        FindBar.IsVisible = true;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void CloseFind()
    {
        FindBar.IsVisible = false;
        SelectedTab?.View.Focus();
    }

    // Typing starts again from the newest output, as the query changed.
    private void FindNext(bool older, bool fromEnd = false)
    {
        if (SelectedTab?.View is not { } view)
        {
            return;
        }

        var query = FindBox.Text ?? string.Empty;
        if (fromEnd)
        {
            view.ClearSelection();
        }

        if (query.Length == 0)
        {
            FindStatus.Text = string.Empty;
            return;
        }

        var count = view.CountMatches(query);
        if (view.Find(query, older) is { } match)
        {
            view.Select(match.Start, match.End);
        }

        FindStatus.Text = count == 0 ? Strings.FindNone : Strings.Format(Strings.FindCount, count);
    }
}
