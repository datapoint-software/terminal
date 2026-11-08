using System.Globalization;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Agent.Tests.Fakes;
using static Datapoint.Terminal.Agent.Tests.Fakes.ScriptedChatClient;

namespace Datapoint.Terminal.Agent.Tests;

// Product decision 150.
public sealed class AgentLanguageTests
{
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
    public async Task Says_nothing_about_language_without_a_source()
    {
        var client = new ScriptedChatClient(_ => Text("Done."), _ => Text("Done."));
        using var agent = new TerminalAgent(client, new FakeUser(), new SecretRedactor());

        await agent.SendAsync("Hello", CancellationToken.None);

        Assert.DoesNotContain("Language:", client.Requests[^1][0].Text, StringComparison.Ordinal);
    }
}
