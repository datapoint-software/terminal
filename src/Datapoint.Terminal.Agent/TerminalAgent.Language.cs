using System.Globalization;
using Datapoint.Terminal.Agent.Resources;

namespace Datapoint.Terminal.Agent;

// Product decision 150: the agent writes to the user in the interface's language. It's read when a conversation starts and
// kept until the conversation is cleared, so a change applies from the next one.
public sealed partial class TerminalAgent
{
    // The interface's language now; without it, the agent says nothing about language and its notices are in English.
    public Func<CultureInfo>? LanguageSource { get; set; }

    public CultureInfo Language { get; private set; } = CultureInfo.GetCultureInfo("en");

    private void StartLanguage() => Language = LanguageSource?.Invoke() ?? Language;

    // Written in English, which models follow most reliably, whatever language it asks for.
    private string LanguageBlock()
    {
        if (LanguageSource is null)
        {
            return string.Empty;
        }

        var language = string.Equals(Language.TwoLetterISOLanguageName, "pt", StringComparison.Ordinal)
            ? "European Portuguese, as written in Portugal (e.g. ficheiro, ecrã, utilizador, palavra-passe; never Brazilian forms)"
            : "English";
        return $"""


            Language: write to the user in {language}: your replies, questions, plans, step titles and reports, whatever
            language the terminal output, files or pages are in, and even if the user writes in another one. Keep commands,
            file contents, host documentation, memory notes and skills in the language they're already in, or in English
            when they're new.
            """;
    }

    private string Say(string name, params object?[] arguments) => AgentStrings.Format(Language, name, arguments);
}
