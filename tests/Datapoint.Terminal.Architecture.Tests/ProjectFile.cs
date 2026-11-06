using System.Xml.Linq;

namespace Datapoint.Terminal.Architecture.Tests;

public sealed record ProjectFile(string Name, IReadOnlyList<string> ProjectReferences, IReadOnlyList<string> PackageReferences)
{
    public static ProjectFile Load(string name)
    {
        var path = Path.Combine(RepositoryRoot.Path, "src", name, $"{name}.csproj");
        var document = XDocument.Load(path);
        var projectReferences = document.Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(((string)reference.Attribute("Include")!).Replace('\\', '/')))
            .ToList();
        var packageReferences = document.Descendants("PackageReference")
            .Select(reference => (string)reference.Attribute("Include")!)
            .ToList();
        return new ProjectFile(name, projectReferences, packageReferences);
    }
}
