namespace Datapoint.Terminal.Architecture.Tests;

public sealed class CryptographyBoundaryTests
{
    private static readonly string s_cryptographyFolder = Path.Combine("Datapoint.Terminal.Security", "Cryptography") + Path.DirectorySeparatorChar;

    [Theory]
    [InlineData("System.Security.Cryptography")]
    [InlineData("Org.BouncyCastle")]
    public void Only_the_cryptography_area_uses_cryptography_libraries(string library)
    {
        var sources = Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains(s_cryptographyFolder, StringComparison.Ordinal));

        var offenders = sources.Where(path => File.ReadAllText(path).Contains(library, StringComparison.Ordinal)).ToList();

        Assert.Empty(offenders);
    }
}
