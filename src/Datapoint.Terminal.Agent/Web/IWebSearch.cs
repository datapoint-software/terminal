namespace Datapoint.Terminal.Agent.Web;

public interface IWebSearch
{
    // Who receives the query, as the user knows them, e.g. "Brave Search".
    string Name { get; }

    Task<WebSearchResults> SearchAsync(string query, CancellationToken cancellationToken);
}
