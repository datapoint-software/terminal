using Datapoint.Terminal.Sessions.Ssh.Config;

namespace Datapoint.Terminal.Sessions.Tests.Ssh.Config;

public sealed class SshJumpTests
{
    [Fact]
    public void Reads_each_hop_in_order()
    {
        var jumps = SshJump.Parse("admin@bastion:2222, ssh://inner, [fe80::1]:22", out var problem);

        Assert.Null(problem);
        Assert.Equal([new SshJump("bastion", "admin", 2222), new SshJump("inner", null, null), new SshJump("fe80::1", null, 22)], jumps);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    public void None_means_no_hops(string? value) => Assert.Empty(SshJump.Parse(value, out _)!);

    [Theory]
    [InlineData("bastion:99999")]
    [InlineData("@bastion")]
    [InlineData("-oProxyCommand=evil")]
    [InlineData("bastion,")]
    public void Refuses_a_hop_it_cant_read(string value)
    {
        Assert.Null(SshJump.Parse(value, out var problem));
        Assert.NotNull(problem);
    }
}
