using Datapoint.Terminal.Agent.Languages;

namespace Datapoint.Terminal.Agent.Tests.Languages;

// Product decision 151.
public sealed class TextLanguageTests
{
    [Fact]
    public void Tells_english_prose()
    {
        const string Text = "# web1\n\nThe web server for the shop. Restart it with care: the cache is cleared on each restart and it takes a minute to warm up.";

        Assert.Equal(TextLanguage.English, TextLanguage.Detect(Text));
    }

    [Fact]
    public void Tells_portuguese_prose()
    {
        const string Text = "# web1\n\nServidor web da loja. Reinicie com cuidado: a cache é limpa em cada reinício e demora um minuto a aquecer.";

        Assert.Equal(TextLanguage.Portuguese, TextLanguage.Detect(Text));
    }

    [Fact]
    public void Leaves_out_code_and_names_with_dots()
    {
        const string Text = """
            Servidor de base de dados da loja, em db1.example.com.

            ```sh
            if [ -f /etc/app.conf ]; then echo "the file is in the right place and it is ready for the app to use"; fi
            ```

            Não reinicie durante o dia: `systemctl restart postgresql` para todas as ligações.
            """;

        Assert.Equal(TextLanguage.Portuguese, TextLanguage.Detect(Text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("# web1\n\nnginx, PHP 8.3")]
    [InlineData("The server está em produção and it is o principal da loja for the app.")]
    public void Says_nothing_when_it_cant_tell(string text) => Assert.Null(TextLanguage.Detect(text));
}
