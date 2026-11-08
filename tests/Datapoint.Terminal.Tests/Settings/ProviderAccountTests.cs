using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class ProviderAccountTests
{
    [Fact]
    public void Prices_anthropics_cache_at_its_published_rates_unless_given()
    {
        var anthropic = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = ProviderKind.Anthropic, InputPrice = 2m, OutputPrice = 10m }.Price!;
        var given = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = ProviderKind.Anthropic, InputPrice = 2m, OutputPrice = 10m, CachedInputPrice = 0.25m }.Price!;

        Assert.Equal((0.2m, 2.5m), (anthropic.CachedInputPerMillion, anthropic.CacheWritePerMillion));
        Assert.Equal(0.25m, given.CachedInputPerMillion);
    }

    [Theory]
    [InlineData(ProviderKind.OpenAI)]
    [InlineData(ProviderKind.OpenAICompatible)]
    public void Prices_other_providers_cache_at_the_input_price_unless_given(ProviderKind kind)
    {
        var price = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = kind, InputPrice = 2m, OutputPrice = 10m }.Price!;

        Assert.Equal(((decimal?)null, (decimal?)null), (price.CachedInputPerMillion, price.CacheWritePerMillion));
    }

    [Fact]
    public void Takes_a_known_models_window_and_prices_from_its_provider_over_what_was_saved()
    {
        var account = new ProviderAccount { Id = "a", Name = "A", Model = "claude-opus-5-5", Kind = ProviderKind.Anthropic, ContextWindow = 200_000, InputPrice = 15m, OutputPrice = 75m };

        Assert.Equal(1_000_000, account.EffectiveContextWindow);
        Assert.Equal((4m, 20m, (decimal?)0.2m, (decimal?)5m), (account.Price!.InputPerMillion, account.Price.OutputPerMillion, account.Price.CachedInputPerMillion, account.Price.CacheWritePerMillion));
    }

    [Fact]
    public void Knows_no_model_on_a_compatible_server_whatever_its_name()
    {
        var account = new ProviderAccount { Id = "a", Name = "A", Model = "gpt-6-astra", Kind = ProviderKind.OpenAICompatible, ContextWindow = 32_000 };

        Assert.Null(account.Known);
        Assert.Equal(32_000, account.EffectiveContextWindow);
        Assert.Null(account.Price);
    }

    [Theory]
    [InlineData(100_000, 0.06)]
    [InlineData(100_001, 0.3000005)]
    public void Prices_a_whole_request_at_the_long_prompt_rate_once_its_prompt_passes_the_threshold(long input, double expected)
    {
        var price = ProviderCatalog.Find(ProviderKind.Anthropic, "claude-haiku-5-5")!.Price;

        Assert.Equal((decimal)expected, price.CostOf(input, output: 100_000));
    }

    [Fact]
    public void Leaves_known_values_out_of_the_saved_settings()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new ProviderAccount { Id = "a", Name = "A", Model = "claude-opus-5-5", Kind = ProviderKind.Anthropic });

        Assert.DoesNotContain("known", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("effective", json, StringComparison.OrdinalIgnoreCase);
    }
}
