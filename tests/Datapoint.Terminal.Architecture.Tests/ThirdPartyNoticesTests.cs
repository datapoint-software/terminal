using Datapoint.Terminal.ThirdPartyNotices;

namespace Datapoint.Terminal.Architecture.Tests;

// engineering.md, Dependencies: the notices file is generated and fails the build when it's out of date.
public sealed class ThirdPartyNoticesTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-notices-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void The_notices_file_is_up_to_date()
    {
        var committed = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "THIRD-PARTY-NOTICES.txt"));

        Assert.True(
            string.Equals(committed, NoticesFile.Generate(RepositoryRoot.Path), StringComparison.Ordinal),
            "THIRD-PARTY-NOTICES.txt is out of date: run dotnet run --project tools/ThirdPartyNotices and commit the result.");
    }

    [Fact]
    public void The_shipped_application_does_not_include_the_inter_font() =>
        Assert.DoesNotContain("Avalonia.Fonts.Inter", NoticesFile.Generate(RepositoryRoot.Path), StringComparison.Ordinal);

    [Theory]
    [InlineData("GPL-3.0-only")]
    [InlineData("MIT OR GPL-2.0-only")]
    public void Refuses_a_license_that_has_not_been_reviewed(string license)
    {
        var assets = FakePackage("Example", "1.0.0", $"""<license type="expression">{license}</license>""");

        var exception = Assert.Throws<NoticeException>(() => Packages.Read(assets, _folder));

        Assert.Contains("Example 1.0.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fills_in_a_template_when_a_package_has_no_license_file()
    {
        File.WriteAllText(Path.Combine(_folder, "MIT.txt"), "MIT License\n\n{copyright}\n\nPermission...");
        var assets = FakePackage("Example", "1.0.0", """<license type="expression">MIT</license><copyright>Ana Silva</copyright>""");

        var component = Assert.Single(Packages.Read(assets, _folder));

        Assert.Equal("MIT License\n\nCopyright (c) Ana Silva\n\nPermission...", component.LicenseText);
    }

    private string FakePackage(string id, string version, string metadata)
    {
        var cache = Path.Combine(_folder, "cache");
        var folder = Path.Combine(cache, id.ToLowerInvariant(), version);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, id.ToLowerInvariant() + ".nuspec"), $"""<?xml version="1.0"?><package><metadata><id>{id}</id>{metadata}</metadata></package>""");
        var assets = Path.Combine(_folder, "project.assets.json");
        var cacheJson = cache.Replace("\\", "\\\\", StringComparison.Ordinal);
        var library = "\"" + id + "/" + version + "\":{\"type\":\"package\",\"runtime\":{\"lib/net10.0/" + id + ".dll\":{}}}";
        var json = "{\"targets\":{\"net10.0\":{" + library + "}},\"packageFolders\":{\"" + cacheJson + "/\":{}}}";
        File.WriteAllText(assets, json);
        return assets;
    }
}
