using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class ProviderAccountTests
{
    [Fact]
    public void Prices_anthropics_cache_at_its_published_rates_unless_given()
    {
        var anthropic = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = ProviderKind.Anthropic, InputPrice = 2m, OutputPrice = 10m }.ToPrice()!;
        var given = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = ProviderKind.Anthropic, InputPrice = 2m, OutputPrice = 10m, CachedInputPrice = 0.25m }.ToPrice()!;

        Assert.Equal((0.2m, 2.5m), (anthropic.CachedInputPerMillion, anthropic.CacheWritePerMillion));
        Assert.Equal(0.25m, given.CachedInputPerMillion);
    }

    [Theory]
    [InlineData(ProviderKind.OpenAI)]
    [InlineData(ProviderKind.OpenAICompatible)]
    public void Prices_other_providers_cache_at_the_input_price_unless_given(ProviderKind kind)
    {
        var price = new ProviderAccount { Id = "a", Name = "A", Model = "m", Kind = kind, InputPrice = 2m, OutputPrice = 10m }.ToPrice()!;

        Assert.Equal(((decimal?)null, (decimal?)null), (price.CachedInputPerMillion, price.CacheWritePerMillion));
    }
}
