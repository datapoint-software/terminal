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
            language the terminal output, files or pages are in, and even if the user writes in another one, unless the
            user's instruction files say otherwise.
            For prose you write into files (documentation, skills, memory notes, comments), take the language from the first
            of these that applies:
            1. the user's instruction files, if they set one;
            2. the language the file is already written in;
            3. for a new or empty file, the language of the files the user keeps: host documentation, skills, memory notes
               and instruction files, preferring those closest to it (the same host, workspace or folder). Go by what you've
               already seen; don't read files only to find out their language. Files that came with the system, packages
               or other people's projects don't count;
            4. otherwise, {language}, the user's interface language.
            Never translate commands, configuration keys, identifiers or file names.
            """;
    }

    private string Say(string name, params object?[] arguments) => AgentStrings.Format(Language, name, arguments);
}
