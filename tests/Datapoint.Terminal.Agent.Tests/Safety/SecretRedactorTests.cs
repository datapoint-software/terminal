using Datapoint.Terminal.Agent.Safety;

namespace Datapoint.Terminal.Agent.Tests.Safety;

public sealed class SecretRedactorTests
{
    [Theory]
    [InlineData("key: sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123456789")]
    [InlineData("export OPENAI_API_KEY=sk-proj-abcdefghijklmnopqrstuvwxyz012345")]
    [InlineData("token ghp_abcdefghijklmnopqrstuvwxyz0123456789AB")]
    [InlineData("aws AKIAIOSFODNN7EXAMPLE")]
    [InlineData("DB_PASSWORD=hunter2hunter2")]
    [InlineData("password: \"correct horse\"")]
    public void Removes_well_known_secret_formats(string text)
    {
        var redacted = new SecretRedactor().Redact(text);

        Assert.Contains(SecretRedactor.Mask, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnop", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("horse", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Removes_a_private_key_block()
    {
        const string text = "before\n-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAA\n-----END OPENSSH PRIVATE KEY-----\nafter";

        var redacted = new SecretRedactor().Redact(text);

        Assert.Equal("before\n" + SecretRedactor.Mask + "\nafter", redacted);
    }

    [Fact]
    public void Removes_known_secrets_wherever_they_appear()
    {
        var redactor = new SecretRedactor();
        redactor.AddKnownSecret("my-very-own-key-123");

        Assert.Equal("x" + SecretRedactor.Mask + "y", redactor.Redact("xmy-very-own-key-123y"));
    }

    [Fact]
    public void Leaves_ordinary_output_alone()
    {
        const string text = "Filesystem      Size  Used Avail Use% Mounted on\n/dev/sda1        50G   20G   28G  42% /";

        Assert.Equal(text, new SecretRedactor().Redact(text));
    }
}
