using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Chat;

namespace Datapoint.Terminal.Tests.Chat;

public sealed class ProviderFromEnvironmentTests
{
    private static Func<string, string?> Variables(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.Ordinal)).Value;

    [Fact]
    public void Uses_anthropic_with_the_default_model_when_its_key_is_set()
    {
        var choice = ProviderFromEnvironment.Read(Variables(("ANTHROPIC_API_KEY", "sk-ant-test")));

        Assert.NotNull(choice);
        Assert.Equal(ProviderKind.Anthropic, choice.Settings.Kind);
        Assert.Equal(ProviderSettings.DefaultAnthropicModel, choice.Settings.Model);
        Assert.Contains("Anthropic", choice.PrivacyNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void Needs_a_key_or_a_model()
    {
        Assert.Null(ProviderFromEnvironment.Read(Variables()));
        Assert.Null(ProviderFromEnvironment.Read(Variables(("DATAPOINT_PROVIDER", "openai"), ("OPENAI_API_KEY", "sk-test"))));
    }

    [Fact]
    public void Says_when_a_compatible_server_is_local()
    {
        var choice = ProviderFromEnvironment.Read(Variables(("DATAPOINT_PROVIDER", "compatible"), ("DATAPOINT_MODEL", "llama"), ("DATAPOINT_ENDPOINT", "http://localhost:11434/v1")));

        Assert.NotNull(choice);
        Assert.True(choice.Settings.IsLocal);
        Assert.Contains("localhost:11434", choice.PrivacyNotice, StringComparison.Ordinal);
    }
}
