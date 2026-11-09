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
    public void Lists_the_inter_font_under_its_own_license_as_well_as_its_package()
    {
        var notices = NoticesFile.Generate(RepositoryRoot.Path);

        Assert.Contains("  Avalonia.Fonts.Inter 12.1.5 (MIT)", notices, StringComparison.Ordinal);
        Assert.Contains("  Inter 3.19 (the interface font, in Avalonia.Fonts.Inter) (OFL-1.1)", notices, StringComparison.Ordinal);
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_what_the_installers_bundle_with_where_to_get_libfuse()
    {
        var notices = NoticesFile.Generate(RepositoryRoot.Path);

        Assert.Contains("In the installers:", notices, StringComparison.Ordinal);
        Assert.Contains("  velopack 1.2.161 (MIT)", notices, StringComparison.Ordinal);
        Assert.Contains("  libfuse 3.15.0, with the AppImage runtime's patch to lib/mount.c (LGPL-2.1)", notices, StringComparison.Ordinal);
        Assert.Contains("https://github.com/libfuse/libfuse/releases/tag/fuse-3.15.0", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void Asks_to_regenerate_what_the_installers_bundle_after_a_vpk_upgrade()
    {
        File.WriteAllText(Path.Combine(_folder, "dotnet-tools.json"), """{ "tools": { "vpk": { "version": "9.9.9" } } }""");

        var exception = Assert.Throws<NoticeException>(() => Bundled.Read(_folder));

        Assert.Contains("vpk-9.9.9-rust-crates.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_bundled_software_under_a_license_that_has_not_been_reviewed()
    {
        File.WriteAllText(Path.Combine(_folder, "dotnet-tools.json"), """{ "tools": { "vpk": { "version": "1.0.0" } } }""");
        var bundled = Directory.CreateDirectory(Path.Combine(_folder, "tools", "ThirdPartyNotices", "Bundled")).FullName;
        File.WriteAllText(Path.Combine(bundled, "vpk-1.0.0-rust-crates.json"), """{ "licenses": [ { "id": "GPL-3.0-only", "text": "...", "crates": [ { "name": "example", "version": "1.0.0", "repository": null } ] } ] }""");
        File.WriteAllText(Path.Combine(bundled, "vpk-1.0.0-appimage-runtime.json"), """{ "licenses": [] }""");

        var exception = Assert.Throws<NoticeException>(() => Bundled.Read(_folder));

        Assert.Contains("GPL-3.0-only", exception.Message, StringComparison.Ordinal);
    }

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
