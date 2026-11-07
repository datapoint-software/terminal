using Datapoint.Terminal.Agent.Runbooks;

namespace Datapoint.Terminal.Agent.Tests.Runbooks;

public sealed class RunbookDocumentTests
{
    [Fact]
    public void Writes_readable_markdown()
    {
        var text = RunbookDocument.Create("Renew certificates", "Renews and reloads nginx.", [new("domain", "The site's domain.")], "1. `sudo certbot renew --cert-name {{domain}}`");

        Assert.Equal(
            "# Renew certificates\n\nRenews and reloads nginx.\n\n## Parameters\n\n- `domain`: The site's domain.\n\n## Steps\n\n1. `sudo certbot renew --cert-name {{domain}}`\n",
            text);
    }

    [Theory]
    [InlineData("Renew certificates", "renew-certificates.md")]
    [InlineData("  Rotate  logs / nginx ", "rotate-logs-nginx.md")]
    [InlineData("../../etc/passwd", "etc-passwd.md")]
    [InlineData("!!!", "runbook.md")]
    public void Names_files_safely(string name, string expected)
    {
        Assert.Equal(expected, RunbookDocument.FileNameFor(name));
    }

    [Fact]
    public void Finds_listed_and_placeholder_parameters()
    {
        var text = "## Parameters\n\n- `domain`: x\n\n## Steps\n\n1. ssh {{host}} and renew {{domain}}\n";

        Assert.Equal(["domain", "host"], RunbookDocument.Parameters(text));
    }
}
