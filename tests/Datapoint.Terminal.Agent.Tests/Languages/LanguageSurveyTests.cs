using Datapoint.Terminal.Agent.Languages;

namespace Datapoint.Terminal.Agent.Tests.Languages;

// Product decision 151.
public sealed class LanguageSurveyTests
{
    private const string English = "The web server for the shop. Restart it with care, as it takes a minute to warm up.";
    private const string Portuguese = "Servidor web da loja. Reinicie com cuidado, porque demora um minuto a aquecer e não deve ser feito de dia.";

    [Fact]
    public void Counts_each_kind_of_file_and_names_hosts_by_language()
    {
        var survey = new LanguageSurvey();
        survey.AddHost("web1", Portuguese);
        survey.AddHost("db1", Portuguese);
        survey.AddHost("cache1", English);
        survey.AddHost("new1", "# new1");
        survey.Add("skills", Portuguese);

        var text = survey.Describe(language => language);

        Assert.Equal(
            """
            - all: pt 3, en 1, unclear 1
            - host documentation: pt 2, en 1, unclear 1
              - en: cache1
              - pt: db1, web1
              - unclear: new1
            - skills: pt 1

            """.ReplaceLineEndings("\n"),
            text);
    }

    [Fact]
    public void Says_none_yet_without_files() => Assert.Equal("- none yet\n", new LanguageSurvey().Describe(language => language));
}
