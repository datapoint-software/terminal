using System.Globalization;
using Datapoint.Terminal.Agent.Hosts;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 150.
public sealed class AgentLanguageTests
{
    private static readonly string[] s_rules =
    [
        "1. the user's instruction files",
        "2. the language the file is already written in",
        "3. for a new or empty file",
        "4. when the count says none yet, European Portuguese",
    ];

    [Fact]
    public async Task Tells_the_model_to_write_in_the_language_the_conversation_started_in()
    {
        var language = CultureInfo.GetCultureInfo("pt-PT");
        var client = new ScriptedChatClient(_ => Text("Feito."), _ => Text("Feito."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor()) { LanguageSource = () => language };

        await agent.SendAsync("Olá", CancellationToken.None);
        language = CultureInfo.GetCultureInfo("en");
        await agent.SendAsync("Again", CancellationToken.None);

        var prompts = client.Requests.Select(request => request[0].Text).ToList();
        Assert.All(prompts, prompt => Assert.Contains("European Portuguese", prompt, StringComparison.Ordinal));
        Assert.Equal("pt-PT", agent.Language.Name);
    }

    [Fact]
    public async Task Changes_language_with_the_next_conversation()
    {
        var language = CultureInfo.GetCultureInfo("pt-PT");
        var client = new ScriptedChatClient(_ => Text("Done."), _ => Text("Done."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor()) { LanguageSource = () => language };
        await agent.SendAsync("Olá", CancellationToken.None);

        language = CultureInfo.GetCultureInfo("en");
        agent.ClearConversation();
        await agent.SendAsync("Hello", CancellationToken.None);

        Assert.Contains("write to the user in English", client.Requests[^1][0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ranks_where_the_language_of_a_file_comes_from()
    {
        var client = new ScriptedChatClient(_ => Text("Feito."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor()) { LanguageSource = () => CultureInfo.GetCultureInfo("pt-PT") };

        await agent.SendAsync("Olá", CancellationToken.None);

        var prompt = client.Requests[^1][0].Text;
        var rules = s_rules.Select(rule => prompt.IndexOf(rule, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, rules);
        Assert.Equal(rules.Order().ToList(), rules);
        Assert.Contains("ask the user with ask_user", prompt, StringComparison.Ordinal);
        Assert.Contains("instruction files say otherwise", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counts_the_language_of_host_documentation_without_sending_it()
    {
        var folder = Directory.CreateTempSubdirectory("language-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "web1"));
            await File.WriteAllTextAsync(Path.Combine(folder, "web1", "README.md"), "Servidor web da loja. Reinicie com cuidado, porque demora um minuto a aquecer e não deve ser feito de dia.", TestContext.Current.CancellationToken);
            var client = new ScriptedChatClient(_ => Text("Done."));
            using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor())
            {
                LanguageSource = () => CultureInfo.GetCultureInfo("en"),
                HostDocs = new HostDocsFolder(folder, _ => false, CanWrite: true),
            };

            await agent.SendAsync("Hello", CancellationToken.None);

            var prompt = client.Requests[^1][0].Text;
            Assert.Contains("- host documentation: European Portuguese 1", prompt, StringComparison.Ordinal);
            Assert.Contains("  - European Portuguese: web1", prompt, StringComparison.Ordinal);
            Assert.Contains("4. when the count says none yet, English", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Reinicie com cuidado", string.Concat(client.Requests[^1].Select(message => message.Text)), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Says_nothing_about_language_without_a_source()
    {
        var client = new ScriptedChatClient(_ => Text("Done."), _ => Text("Done."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());

        await agent.SendAsync("Hello", CancellationToken.None);

        Assert.DoesNotContain("Language:", client.Requests[^1][0].Text, StringComparison.Ordinal);
    }
}
