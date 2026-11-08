using Datapoint.Terminal.Sessions.Ssh.Transport;

namespace Datapoint.Terminal.Sessions.Tests.Ssh;

public sealed class VersionExchangeTests
{
    [Theory]
    [InlineData("1.0.0", "SSH-2.0-DatapointTerminal_1.0.0")]
    [InlineData("1.0.0-alpha.1", "SSH-2.0-DatapointTerminal_1.0.0_alpha.1")]
    [InlineData("1.0 beta", "SSH-2.0-DatapointTerminal_1.0_beta")]
    public void Keeps_hyphens_and_spaces_out_of_the_software_version(string version, string expected) =>
        Assert.Equal(expected, VersionExchange.Identification(version));

    [Fact]
    public void Identifies_with_a_single_hyphen_after_the_protocol_version() =>
        Assert.Equal(2, VersionExchange.ClientVersion.Count(character => character == '-'));
}
