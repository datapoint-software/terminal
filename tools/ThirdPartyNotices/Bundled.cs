using System.Text.Json;

namespace Datapoint.Terminal.ThirdPartyNotices;

// Software the installers carry beside the application: the programs Velopack's vpk tool adds to every package, and the
// AppImage runtime it puts at the start of each Linux package. NuGet doesn't describe them, so their licenses are kept in
// Bundled/, one set per vpk version (see Bundled/README.md); a vpk upgrade fails here until they're regenerated.
public static class Bundled
{
    private const string Libfuse = "libfuse";

    // LGPL-2.1, section 6: a statically linked libfuse comes with its source and what's needed to relink it.
    private const string LibfuseSource = """
        libfuse in the Linux AppImages

        Each Linux .AppImage starts with the AppImage type-2 runtime, which links
        libfuse 3.15.0 statically, under the GNU Lesser General Public License,
        version 2.1. You can get libfuse's complete source, the change the runtime
        makes to it, and the scripts that build the runtime and link it, so you can
        change libfuse and relink the runtime with it:

          libfuse 3.15.0:  https://github.com/libfuse/libfuse/releases/tag/fuse-3.15.0
          The change:      https://github.com/AppImage/type2-runtime/blob/3d17002ee2a519a288e28aeb7bc41af5f5adaf4e/patches/libfuse/mount.c.diff
          Build scripts:   https://github.com/AppImage/type2-runtime/tree/3d17002ee2a519a288e28aeb7bc41af5f5adaf4e/scripts

        To use a relinked runtime, replace the runtime at the start of the .AppImage
        with yours, or extract the AppImage with --appimage-extract and run its
        contents directly, without the runtime. If any of these links stops
        working, ask Datapoint at hello@datapoint.software for a copy.
        """;

    public static IReadOnlyList<Component> Read(string repository)
    {
        var version = VpkVersion(repository);
        var folder = Path.Combine(repository, "tools", "ThirdPartyNotices", "Bundled");
        string[] sets = [$"vpk-{version}-rust-crates.json", $"vpk-{version}-appimage-runtime.json"];
        var missing = sets.FirstOrDefault(set => !File.Exists(Path.Combine(folder, set)));
        if (missing is not null)
        {
            throw new NoticeException($"Bundled/{missing} is missing: vpk is at {version}, so regenerate what its packages bundle (see tools/ThirdPartyNotices/Bundled/README.md).");
        }

        return [.. sets.SelectMany(set => ReadSet(Path.Combine(folder, set))).OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)];
    }

    private static IEnumerable<Component> ReadSet(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        foreach (var license in document.RootElement.GetProperty("licenses").EnumerateArray())
        {
            var id = license.GetProperty("id").GetString()!;
            if (!Packages.Reviewed.Contains(id))
            {
                throw new NoticeException($"{Path.GetFileName(path)} includes software under {id}, which hasn't been reviewed (engineering.md, Dependencies).");
            }

            var text = Packages.Normalize(license.GetProperty("text").GetString()!);
            foreach (var item in license.GetProperty("crates").EnumerateArray())
            {
                var name = item.GetProperty("name").GetString()!;
                var version = item.GetProperty("version").GetString();
                var project = item.GetProperty("repository").GetString();
                var notices = string.Equals(name, Libfuse, StringComparison.Ordinal) ? new[] { LibfuseSource } : [];
                yield return new Component(version is null ? name : $"{name} {version}", id, project, text, notices);
            }
        }
    }

    private static string VpkVersion(string repository)
    {
        using var tools = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repository, "dotnet-tools.json")));
        return tools.RootElement.GetProperty("tools").GetProperty("vpk").GetProperty("version").GetString()!;
    }
}
