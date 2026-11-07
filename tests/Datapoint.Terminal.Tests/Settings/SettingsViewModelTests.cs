using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Tests.Chat;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-settings-").FullName;
    private readonly MemoryCredentialStore _store = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private SettingsStore Store => new(Path.Combine(_folder, "settings.json"));

    private SettingsViewModel Create(Func<ProviderAccount, string?, IChatClient>? client = null) =>
        new(Store, Store.Load(), new ProviderKeys(_store, _ => null), client ?? ((_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK")))));

    [Fact]
    public void The_first_provider_added_is_used_and_saved()
    {
        var settings = Create();
        var changes = 0;
        settings.Changed += (_, _) => changes++;

        settings.AddCommand.Execute(null);

        var provider = Assert.Single(settings.Providers);
        Assert.True(provider.IsActive);
        Assert.Equal(ProviderKind.Anthropic, provider.Kind.Kind);
        Assert.Equal("claude-opus-5-5", provider.Model);
        Assert.Equal(provider.Id, Store.Load().SelectedProviderId);
        Assert.True(changes > 0);
    }

    // Settings closing or showing another provider clears its controls through their bindings; saving must still work.
    [Fact]
    public void Keeps_the_kind_and_model_when_a_binding_clears_them()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);
        var provider = settings.Providers[0];

        provider.Kind = null!;
        provider.Model = null!;
        settings.SetHistoryDays(30);

        Assert.Equal(ProviderKind.Anthropic, provider.Kind.Kind);
        Assert.Equal("claude-opus-5-5", provider.Model);
        Assert.Equal("claude-opus-5-5", Assert.Single(Store.Load().Providers).Model);
    }

    [Fact]
    public void Changing_the_kind_follows_with_defaults_but_keeps_the_users_choices()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);
        var provider = settings.Providers[0];

        provider.Kind = settings.Kinds.Single(kind => kind.Kind == ProviderKind.OpenAICompatible);

        Assert.Equal("OpenAI-compatible server", provider.Name);
        Assert.Equal(string.Empty, provider.Model);
        Assert.True(provider.IsCompatible);

        provider.Name = "Ollama";
        provider.Model = "llama3.3";
        provider.Endpoint = "http://localhost:11434/v1";
        provider.IsLocal = true;
        provider.Kind = settings.Kinds.Single(kind => kind.Kind == ProviderKind.OpenAI);

        Assert.Equal("Ollama", provider.Name);
        Assert.Equal("llama3.3", provider.Model);
        Assert.Null(Store.Load().Providers[0].Endpoint);
    }

    [Fact]
    public void Saves_a_key_without_keeping_it_on_screen()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);
        var provider = settings.Providers[0];

        provider.KeyDraft = "sk-ant-secret-value";
        provider.SaveKeyCommand.Execute(null);

        Assert.Equal(string.Empty, provider.KeyDraft);
        Assert.True(provider.HasSavedKey);
        Assert.False(provider.ShowKeyEntry);
        Assert.Contains("Test store", provider.KeyStatus, StringComparison.Ordinal);
        Assert.Equal("sk-ant-secret-value", _store.Items[$"Datapoint Terminal|provider/{provider.Id}"]);
        Assert.DoesNotContain("sk-ant-secret-value", File.ReadAllText(Store.Path), StringComparison.Ordinal);

        provider.RemoveKeyCommand.Execute(null);
        Assert.Empty(_store.Items);
        Assert.True(provider.ShowKeyEntry);
    }

    [Fact]
    public void Removing_a_provider_deletes_its_key_and_uses_the_next_one()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);
        settings.AddCommand.Execute(null);
        var first = settings.Providers[0];
        first.KeyDraft = "sk-ant-first";
        first.SaveKeyCommand.Execute(null);
        settings.Selected = first;

        settings.RemoveCommand.Execute(null);

        Assert.Empty(_store.Items);
        Assert.True(Assert.Single(settings.Providers).IsActive);
        Assert.Equal(settings.Providers[0].Id, Store.Load().SelectedProviderId);
    }

    [Fact]
    public async Task Tests_the_connection_and_hides_the_key_in_errors()
    {
        var settings = Create((_, key) => throw new InvalidOperationException($"401: invalid x-api-key {key}"));
        settings.AddCommand.Execute(null);
        var provider = settings.Providers[0];
        provider.KeyDraft = "sk-ant-wrong";
        provider.SaveKeyCommand.Execute(null);

        await provider.TestCommand.ExecuteAsync(null);

        Assert.True(provider.TestFailed);
        Assert.Contains("401", provider.TestResult, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-ant-wrong", provider.TestResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_working_connection()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);

        await settings.Providers[0].TestCommand.ExecuteAsync(null);

        Assert.False(settings.Providers[0].TestFailed);
        Assert.Equal("Connected. The model answered.", settings.Providers[0].TestResult);
    }

    [Fact]
    public void Choosing_a_model_in_the_chat_selects_its_provider()
    {
        var settings = Create();
        settings.AddCommand.Execute(null);
        settings.AddCommand.Execute(null);
        var second = settings.Providers[1];

        settings.Choose(second.Id, "claude-haiku-5-5");

        Assert.True(second.IsActive);
        Assert.Equal("claude-haiku-5-5", Store.Load().SelectedProvider!.Model);
    }

    [Fact]
    public void Describes_where_the_conversation_goes()
    {
        var local = new ProviderAccount { Id = "x", Kind = ProviderKind.OpenAICompatible, Name = "Ollama", Model = "llama", Endpoint = "http://localhost:11434/v1", IsLocal = true };

        Assert.Contains("localhost:11434", ProviderPrivacy.Describe(local), StringComparison.Ordinal);
        Assert.Contains("your own", ProviderPrivacy.Describe(local), StringComparison.Ordinal);
        Assert.Equal("Anthropic", ProviderPrivacy.Destination(local with { Kind = ProviderKind.Anthropic }));
    }

    [Theory]
    [InlineData("claude-opus-5-5", "Opus 5.5")]
    [InlineData("claude-haiku-5", "Haiku 5")]
    [InlineData("claude-sonnet-4-5-20250929", "Sonnet 4.5")]
    [InlineData("gpt-5", "gpt-5")]
    public void Shortens_model_names(string model, string expected) =>
        Assert.Equal(expected, ProviderCatalog.DisplayName(model));
}
