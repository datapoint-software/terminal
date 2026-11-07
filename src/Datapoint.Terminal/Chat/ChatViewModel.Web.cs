using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Web;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Chat;

// Product decision 105: the agent asks before each web search or page read, and only a choice grants it.
public sealed partial class ChatViewModel
{
    // Whether the current model is marked as running on the user's own network, so asking warns that the web is outside it.
    public bool IsModelLocal { get; set; }

    // Raised on the user interface thread when the user allows the web always, so it's saved to their settings.
    public event EventHandler? WebAlwaysAllowed;

    public async Task<WebAccess> RequestWebAccessAsync(WebAccessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var isSearch = request.Kind == WebAccessKind.Search;
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Purpose))
        {
            details.Add(request.Purpose);
        }

        details.Add(isSearch ? Strings.Format(Strings.WebSearchSent, request.Service ?? string.Empty) : Strings.WebReadSent);
        if (IsModelLocal)
        {
            details.Add(Strings.WebLeavesNetwork);
        }

        var question = new AgentQuestion(
            isSearch ? Strings.Format(Strings.WebSearchQuestion, request.Target) : Strings.Format(Strings.WebReadQuestion, request.Target),
            [
                new AgentOption(Strings.WebDeny, Strings.WebDenyDescription),
                new AgentOption(Strings.WebAllowOnce, isSearch ? Strings.WebAllowOnceSearch : Strings.WebAllowOncePage),
                new AgentOption(Strings.WebAllowAlways, Strings.WebAllowAlwaysDescription),
            ])
        {
            Heading = Strings.WebAccessHeading,
            Details = string.Join("\n\n", details.Select(EscapeMarkdown)),
        };
        var view = new QuestionViewModel(question, allowsOwnWords: false);
        view.Answered = answer => RecordWebAnswer(view, answer);
        _ui.Post(() => Question = view);
        var registration = cancellationToken.Register(view.Cancel);
        var answer = await FinishAsync(view.Response, registration, () => ReferenceEquals(Question, view), () => Question = null).ConfigureAwait(false);
        return Access(answer);
    }

    private static WebAccess Access(string? answer) => answer switch
    {
        var label when string.Equals(label, Strings.WebAllowAlways, StringComparison.Ordinal) => WebAccess.Always,
        var label when string.Equals(label, Strings.WebAllowOnce, StringComparison.Ordinal) => WebAccess.Once,
        _ => WebAccess.Denied,
    };

    // The choice stays in the conversation as the outcome of what was asked; skipping is not allowing.
    private void RecordWebAnswer(QuestionViewModel question, string? answer)
    {
        Items.Add(Asked(question, answer ?? Strings.WebDeny));
        if (!string.Equals(answer, Strings.WebAllowAlways, StringComparison.Ordinal))
        {
            return;
        }

        WebAlwaysAllowed?.Invoke(this, EventArgs.Empty);
    }

    // What the agent searched for or read, with each page as a link; the view shows each link's address beside it.
    private static ChatItemViewModel WebItem(AgentEventArgs e)
    {
        var isSearch = e.WebKind == WebAccessKind.Search;
        var title = isSearch
            ? Strings.Format(Strings.WebSearched, e.Text)
            : Strings.Format(Strings.WebReadPage, e.Sources.Count > 0 ? e.Sources[0].Title : e.Text);
        if (isSearch && e.Service is { } service)
        {
            title = Strings.Format(Strings.CommandInTerminal, title, service);
        }

        var detail = e.Problem is { } problem
            ? EscapeMarkdown(problem)
            : e.Sources.Count == 0 ? Strings.WebNoResults : string.Join('\n', e.Sources.Select(source => "- " + Link(source)));
        return new ChatItemViewModel(ChatItemKind.Web, title) { Detail = detail };
    }

    private static string Link(WebSearchResult source)
    {
        var address = source.Address.AbsoluteUri.Replace("(", "%28", StringComparison.Ordinal).Replace(")", "%29", StringComparison.Ordinal);
        return $"[{EscapeMarkdown(source.Title)}]({address})";
    }

    // The purpose is the model's words: shown as text, so it can't dress itself up as a link or a heading.
    private static string EscapeMarkdown(string text)
    {
        var escaped = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '`' or '*' or '_' or '[' or ']' or '<' or '>' or '#' or '!' or '|' or '(' or ')')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }
}
