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
    [InlineData("DATABASE_URL=postgres://app:hunter2hunter2@db.internal/prod")]
    [InlineData("amqp://guest:hunter2@broker:5672/")]
    [InlineData("DB_PASS=hunter2hunter2")]
    [InlineData("db.pwd: hunter2hunter2")]
    [InlineData("SSH_PASSPHRASE='correct horse'")]
    [InlineData("AWS_SECRET_ACCESS_KEY=hunter2hunter2abcdefghijklmnop")]
    [InlineData("ConnectionString=Server=db;User=sa;Password=hunter2")]
    [InlineData("{\"password\": \"hunter2hunter2\"}")]
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

    [Fact]
    public void Finds_each_match_with_its_line()
    {
        var redactor = new SecretRedactor();
        redactor.AddKnownSecret("my-very-own-key-123");

        var matches = redactor.Find("name: api\npassword=hunter2hunter2\nkey my-very-own-key-123 and AKIAIOSFODNN7EXAMPLE");

        Assert.Equal([(2, "hunter2hunter2"), (3, "my-very-own-key-123"), (3, "AKIAIOSFODNN7EXAMPLE")], matches.Select(match => (match.Line, match.Value)));
    }

    [Fact]
    public void Merges_overlapping_matches()
    {
        var redactor = new SecretRedactor();
        redactor.AddKnownSecret("sk-ant-api03-abcdefghijklmnopqrstuvwxyz");

        var matches = redactor.Find("token=sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123");

        var match = Assert.Single(matches);
        Assert.Equal("sk-ant-api03-abcdefghijklmnopqrstuvwxyz0123", match.Value);
    }

    [Fact]
    public void Leaves_values_the_user_allowed()
    {
        const string text = "password=correct-horse\ntoken=hunter2hunter2";

        var redacted = new SecretRedactor().Redact(text, value => string.Equals(value, "correct-horse", StringComparison.Ordinal));

        Assert.Equal("password=correct-horse\ntoken=" + SecretRedactor.Mask, redacted);
    }

    [Fact]
    public void Fingerprints_without_keeping_the_value()
    {
        var fingerprint = SecretRedactor.Fingerprint("hunter2");

        Assert.Equal("f52fbd32b2b3b86ff88ef6c490628285f482af15ddcb29541f94bcf526a3f6c7", fingerprint);
        Assert.DoesNotContain("hunter2", fingerprint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("compass: north-north-west")]
    [InlineData("bypass=enabled-for-tests")]
    [InlineData("passenger: Maria Silva")]
    [InlineData("See https://example.com:8443/docs for details")]
    public void Leaves_lookalike_words_alone(string text) => Assert.Equal(text, new SecretRedactor().Redact(text));

    [Fact]
    public void Keeps_the_address_around_a_url_password()
    {
        Assert.Equal("postgres://app:" + SecretRedactor.Mask + "@db/prod", new SecretRedactor().Redact("postgres://app:S3cret-value@db/prod"));
    }
}
