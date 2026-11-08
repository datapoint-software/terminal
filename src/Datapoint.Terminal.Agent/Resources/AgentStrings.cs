using System.Globalization;
using System.Resources;

namespace Datapoint.Terminal.Agent.Resources;

// What the agent tells the user in the chat, in the language its conversation started in (product decision 150). What it
// tells the model (tool results, the system prompt) stays in English, which models follow most reliably.
internal static class AgentStrings
{
    private static readonly ResourceManager s_resources = new("Datapoint.Terminal.Agent.Resources.AgentStrings", typeof(AgentStrings).Assembly);

    public static string Get(string name, CultureInfo language) => s_resources.GetString(name, language) ?? name;

    // Numbers inside follow the system's regional format, as the rest of the application's do.
    public static string Format(CultureInfo language, string name, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(name, language), arguments);
}
