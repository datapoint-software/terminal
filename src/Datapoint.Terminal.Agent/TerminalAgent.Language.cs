using System.Globalization;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Languages;
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

    private const int MostHostsSurveyed = 300;

    // Written in English, which models follow most reliably, whatever language it asks for.
    private string LanguageBlock(LanguageSurvey survey)
    {
        if (LanguageSource is null)
        {
            return string.Empty;
        }

        var isPortuguese = string.Equals(Language.TwoLetterISOLanguageName, "pt", StringComparison.Ordinal);
        var language = isPortuguese
            ? "European Portuguese, as written in Portugal (e.g. ficheiro, ecrã, utilizador, palavra-passe; never Brazilian forms)"
            : "English";
        var interfaceLanguage = LanguageName(isPortuguese ? TextLanguage.Portuguese : TextLanguage.English);
        return $"""


            Language: write to the user in {language}: your replies, questions, plans, step titles and reports, whatever
            language the terminal output, files or pages are in, and even if the user writes in another one, unless the
            user's instruction files say otherwise.
            For prose you write into files (documentation, skills, memory notes, comments), take the language from the first
            of these that applies:
            1. the user's instruction files, if they set one;
            2. the language the file is already written in;
            3. for a new or empty file, the language of the user's other files in the count below, from the closest: the
               same host's documentation, then the kind of file you're writing, then all of them. If the closest files that
               have one disagree, or the count leaves it unclear, ask the user with ask_user (offering {interfaceLanguage}
               and the other language) before writing, and keep to the answer for the rest of the conversation;
            4. when the count says none yet, {interfaceLanguage}, the user's interface language.
            Never translate commands, configuration keys, identifiers or file names.
            The language of the user's files, counted by the application from their common words (their text isn't sent):
            {survey.Describe(LanguageName).TrimEnd()}
            """;
    }

    private static string LanguageName(string language) =>
        string.Equals(language, TextLanguage.Portuguese, StringComparison.Ordinal) ? "European Portuguese" : "English";

    // Product decision 151: the files the user keeps, as the agent would use them; an unreadable file is left out.
    private LanguageSurvey SurveyLanguages(AgentInstructions instructions)
    {
        var survey = new LanguageSurvey();
        if (HostDocs is { } hosts)
        {
            foreach (var host in hosts.Documented().Take(MostHostsSurveyed))
            {
                try
                {
                    survey.AddHost(host, TextFiles.Read(System.IO.Path.Combine(hosts.Path, host, HostDocsFolder.DocumentName)));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        foreach (var skill in Skills?.List() ?? [])
        {
            survey.Add("skills", skill.Description + "\n" + skill.Body);
        }

        foreach (var file in instructions.Memory.Where(file => file.Text.Length > 0))
        {
            survey.Add("memory notes", file.Text);
        }

        foreach (var file in instructions.Instructions.Where(file => file.Text.Length > 0))
        {
            survey.Add("instruction files", file.Text);
        }

        return survey;
    }

    private string Say(string name, params object?[] arguments) => AgentStrings.Format(Language, name, arguments);
}
