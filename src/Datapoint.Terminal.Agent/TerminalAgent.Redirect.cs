namespace Datapoint.Terminal.Agent;

// A message the user sends while the agent waits on them (an approval, a plan or a question) replaces the task: the wait
// ends, the waiting tool returns the message as the user's new request, and the agent goes on in the same conversation.
public sealed partial class TerminalAgent
{
    private const string SkippedForNewMessage = "Not done: the user sent a new message before this ran.";

    private CancellationTokenSource? _userWait;
    private Redirection? _redirect;

    public bool IsWaitingForUser
    {
        get
        {
            lock (_sync)
            {
                return _userWait is not null && _redirect is null;
            }
        }
    }

    // False when nothing is waiting on the user any more, e.g. they answered a moment before; the message isn't taken then.
    public bool Redirect(string message, TerminalExcerpt? excerpt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (_sync)
        {
            if (_userWait is null || _redirect is not null)
            {
                return false;
            }

            _redirect = new Redirection(message.Trim(), excerpt);

            // Inside the lock, so the wait can't end and dispose its source in between.
            _userWait.Cancel();
            return true;
        }
    }

    private Task<CommandApproval> RequestApprovalAsync(CommandProposal proposal, CancellationToken cancellationToken) =>
        WaitForUserAsync(token => _user.RequestApprovalAsync(proposal, token), cancellationToken);

    private Task<string?> AskAsync(AgentQuestion question, CancellationToken cancellationToken) =>
        WaitForUserAsync(token => _user.AskAsync(question, token), cancellationToken);

    private async Task<T> WaitForUserAsync<T>(Func<CancellationToken, Task<T>> wait, CancellationToken cancellationToken)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_sync)
        {
            _userWait = waiting;
        }

        try
        {
            var result = await wait(waiting.Token).ConfigureAwait(false);

            // The user may have answered and sent a message at the same moment; the message they saw taken wins.
            return EndWait() ? throw new RedirectedException() : result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && EndWait())
        {
            throw new RedirectedException();
        }
        finally
        {
            EndWait();
        }
    }

    private bool EndWait()
    {
        lock (_sync)
        {
            _userWait = null;
            return _redirect is not null;
        }
    }

    private bool IsRedirected
    {
        get
        {
            lock (_sync)
            {
                return _redirect is not null;
            }
        }
    }

    // The message is the user's own words, so unlike terminal output it's an instruction; an attached excerpt stays data.
    private string RedirectedResult()
    {
        Redirection redirect;
        lock (_sync)
        {
            redirect = _redirect!;
        }

        _plan = null;
        _proposedPlan = null;
        ForgetApprovals();
        return "Cancelled: instead of answering, the user sent a new message. It replaces the task you were working on, and any "
            + "plan they approved for it no longer applies. Don't go back to that task unless the message asks you to. Their message:\n"
            + redirect.Message + ExcerptBlock(redirect.Excerpt);
    }

    private void ClearRedirect()
    {
        lock (_sync)
        {
            _redirect = null;
        }
    }

    private sealed record Redirection(string Message, TerminalExcerpt? Excerpt);

    private sealed class RedirectedException : Exception
    {
        public RedirectedException()
            : base("The user sent a new message instead.")
        {
        }

        public RedirectedException(string message)
            : base(message)
        {
        }

        public RedirectedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
