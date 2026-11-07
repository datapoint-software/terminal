using System.Net;
using Datapoint.Terminal.Agent.Web;

namespace Datapoint.Terminal.Agent.Tests.Web;

public sealed class WebAddressGuardTests
{
    [Theory]
    [InlineData("https://learn.microsoft.com/dotnet/")]
    [InlineData("http://example.org/page?q=1")]
    [InlineData("https://8.8.8.8/")]
    [InlineData("https://[2606:4700:4700::1111]/")]
    public void Allows_public_addresses(string address) => Assert.Null(WebAddressGuard.Check(new Uri(address)));

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.org/")]
    [InlineData("https://user:secret@example.org/")]
    [InlineData("http://localhost:8080/")]
    [InlineData("http://admin.localhost/")]
    [InlineData("http://printer.local/")]
    [InlineData("http://metadata.google.internal/")]
    [InlineData("http://intranet/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://172.20.1.1/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://100.100.100.200/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://[fd00::1]/")]
    [InlineData("http://[fe80::1]/")]
    [InlineData("http://[2002:7f00:1::]/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0x7f.0.0.1/")]
    public void Refuses_this_machine_private_networks_and_other_schemes(string address) => Assert.NotNull(WebAddressGuard.Check(new Uri(address)));

    [Theory]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("2002:5db8:d822::1", true)]
    [InlineData("100.63.255.255", true)]
    [InlineData("100.64.0.1", false)]
    [InlineData("172.15.0.1", true)]
    [InlineData("172.32.0.1", true)]
    [InlineData("172.31.255.255", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("::", false)]
    [InlineData("64:ff9b::7f00:1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2001:0:4136:e378::1", false)]
    [InlineData("ff02::1", false)]
    public void Judges_resolved_addresses(string address, bool isPublic) => Assert.Equal(isPublic, WebAddressGuard.IsPublic(IPAddress.Parse(address)));
}
