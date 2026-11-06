using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class ProviderKeysTests
{
    private static readonly ProviderAccount s_anthropic = new() { Id = "p1", Kind = ProviderKind.Anthropic, Name = "Anthropic", Model = "claude-opus-5-5" };

    [Fact]
    public void Keeps_keys_in_the_store_under_the_provider()
    {
        var store = new MemoryCredentialStore();
        var keys = new ProviderKeys(store, _ => null);

        keys.Save(s_anthropic, "  sk-ant-saved  ");

        Assert.Equal("sk-ant-saved", store.Items["Datapoint Terminal|provider/p1"]);
        Assert.Equal("sk-ant-saved", keys.Read(s_anthropic));
        Assert.True(keys.HasSavedKey(s_anthropic));

        keys.Delete(s_anthropic);
        Assert.Null(keys.Read(s_anthropic));
    }

    [Fact]
    public void Falls_back_to_the_environment_variable_and_says_so()
    {
        var keys = new ProviderKeys(new MemoryCredentialStore(), name => string.Equals(name, "ANTHROPIC_API_KEY", StringComparison.Ordinal) ? "sk-ant-env" : null);

        Assert.Equal("sk-ant-env", keys.Read(s_anthropic));
        Assert.Equal("ANTHROPIC_API_KEY", keys.EnvironmentVariableFor(s_anthropic));

        keys.Save(s_anthropic, "sk-ant-saved");
        Assert.Equal("sk-ant-saved", keys.Read(s_anthropic));
        Assert.Null(keys.EnvironmentVariableFor(s_anthropic));
    }

    [Fact]
    public void Cannot_save_without_a_store()
    {
        var keys = new ProviderKeys(null, _ => null);

        Assert.False(keys.CanSave);
        Assert.Throws<Security.Credentials.CredentialStoreException>(() => keys.Save(s_anthropic, "sk"));
    }
}
