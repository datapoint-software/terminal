using System.Text.Json;
using System.Xml.Linq;

namespace Datapoint.Terminal.ThirdPartyNotices;

// Reads the packages the application ships from its restore output, and each one's license from the local NuGet cache.
public static class Packages
{
    // engineering.md, Dependencies: only these are accepted until someone reviews another.
    public static readonly IReadOnlySet<string> Reviewed = new HashSet<string>(StringComparer.Ordinal)
    {
        "MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "Unicode-3.0", "OFL-1.1",
    };

    // Material a package carries under a license of its own, which its stated license doesn't cover: the package's
    // license is for its code, and the material is listed separately with its own text from Licenses/.
    private static readonly Dictionary<string, (string Name, string License, string Project, string Text)> s_embedded = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Avalonia.Fonts.Inter"] = ("Inter 3.19 (the interface font, in Avalonia.Fonts.Inter)", "OFL-1.1", "https://github.com/rsms/inter", "Inter-OFL-1.1.txt"),
    };

    // Packages licensed by a file instead of an SPDX expression; each file was read and is the license named here.
    private static readonly Dictionary<string, string> s_fileLicenses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Avalonia.Angle.Windows.Natives"] = "BSD-3-Clause",
    };

    private static readonly string[] s_shippedKinds = ["runtime", "native", "runtimeTargets"];

    // Also a second license file, e.g. one that still covers code contributed under an earlier license.
    private static readonly string[] s_noticePrefixes = ["third", "notice", "license-"];

    private static readonly string[] s_licenseFiles = ["LICENSE", "LICENSE.txt", "LICENSE.md", "License.md", "license.txt"];

    public static IReadOnlyList<Component> Read(string assetsPath, string templates)
    {
        using var assets = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        var root = assets.RootElement;
        var cache = root.GetProperty("packageFolders").EnumerateObject().First().Name;
        var target = root.GetProperty("targets").EnumerateObject().First(item => !item.Name.Contains('/', StringComparison.Ordinal)).Value;
        var shipped = target.EnumerateObject()
            .Where(library => library.Value.TryGetProperty("type", out var type) && string.Equals(type.GetString(), "package", StringComparison.Ordinal) && Ships(library.Value))
            .Select(library => library.Name)
            .ToList();
        var embedded = shipped
            .Select(name => s_embedded.GetValueOrDefault(name.Split('/')[0]))
            .Where(material => material.Name is not null)
            .Select(material => new Component(material.Name, material.License, material.Project, Normalize(File.ReadAllText(Path.Combine(templates, material.Text))), []));
        return [.. shipped.Select(name => Describe(name, cache, templates)).Concat(embedded).OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)];
    }

    // Analyzers and build-only packages have no runtime or native files, and aren't distributed.
    private static bool Ships(JsonElement library) =>
        s_shippedKinds.Any(kind =>
            library.TryGetProperty(kind, out var files) && files.EnumerateObject().Any(file => !file.Name.EndsWith("/_._", StringComparison.Ordinal)));

    private static Component Describe(string name, string cache, string templates)
    {
        var (id, version) = (name.Split('/')[0], name.Split('/')[1]);
        var folder = Path.Combine(cache, id.ToLowerInvariant(), version.ToLowerInvariant());
        var nuspec = XDocument.Load(Directory.EnumerateFiles(folder, "*.nuspec").Single());
        var metadata = nuspec.Root!.Elements().Single(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.Ordinal));
        var license = Element(metadata, "license");
        var copyright = Element(metadata, "copyright")?.Value.Trim();
        var project = Element(metadata, "projectUrl")?.Value.Trim();
        var isFile = string.Equals(license?.Attribute("type")?.Value, "file", StringComparison.Ordinal);
        var spdx = ReviewedLicense(id, version, license, isFile);
        var text = LicenseText(folder, isFile ? license!.Value : null) ?? Template(templates, spdx, copyright, id);
        return new Component($"{id} {version}", spdx, project, text, Notices(folder));
    }

    private static string ReviewedLicense(string id, string version, XElement? license, bool isFile)
    {
        var spdx = isFile ? s_fileLicenses.GetValueOrDefault(id) : license?.Value.Trim();
        if (spdx is not null && Reviewed.Contains(spdx))
        {
            return spdx;
        }

        var stated = isFile ? "a license file" : license?.Value ?? "no stated license";
        throw new NoticeException($"{id} {version} is under {stated}, which hasn't been reviewed. See engineering.md, Dependencies.");
    }

    private static XElement? Element(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.Ordinal));

    private static string? LicenseText(string folder, string? file)
    {
        var candidates = file is null ? s_licenseFiles : [file];
        var path = candidates.Select(name => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
        return path is null ? null : Normalize(File.ReadAllText(path));
    }

    // The package states the license but doesn't include its text, so the standard text is filled in with its copyright.
    private static string Template(string templates, string spdx, string? copyright, string id)
    {
        var path = Path.Combine(templates, spdx + ".txt");
        if (!File.Exists(path) || !File.ReadAllText(path).Contains("{copyright}", StringComparison.Ordinal))
        {
            throw new NoticeException($"{id} doesn't include its {spdx} license text, and there's no template to fill in.");
        }

        var holder = string.IsNullOrWhiteSpace(copyright) ? $"the authors of {id}" : copyright;
        var line = holder.Contains("copyright", StringComparison.OrdinalIgnoreCase) || holder.StartsWith('©') || holder.StartsWith("(c)", StringComparison.OrdinalIgnoreCase)
            ? holder
            : "Copyright (c) " + holder;
        return Normalize(File.ReadAllText(path).Replace("{copyright}", line, StringComparison.Ordinal));
    }

    // Notices for material the package itself includes from others, e.g. the libraries compiled into Skia.
    private static IReadOnlyList<string> Notices(string folder) => [.. Directory.EnumerateFiles(folder)
        .Where(path => s_noticePrefixes.Any(prefix => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        .Order(StringComparer.Ordinal)
        .Select(path => Normalize(File.ReadAllText(path)))];

    public static string Normalize(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd())).Trim('\n');
}
