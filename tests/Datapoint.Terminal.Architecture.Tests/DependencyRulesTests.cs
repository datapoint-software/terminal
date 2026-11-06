namespace Datapoint.Terminal.Architecture.Tests;

public sealed class DependencyRulesTests
{
    private const string Emulator = "Datapoint.Terminal.Emulator";
    private const string Security = "Datapoint.Terminal.Security";
    private const string Sessions = "Datapoint.Terminal.Sessions";
    private const string Agent = "Datapoint.Terminal.Agent";
    private const string Controls = "Datapoint.Terminal.Controls";

    [Fact]
    public void Emulator_references_no_project_and_no_package()
    {
        var project = ProjectFile.Load(Emulator);

        Assert.Empty(project.ProjectReferences);
        Assert.Empty(project.PackageReferences);
    }

    [Fact]
    public void Security_references_no_project()
    {
        var project = ProjectFile.Load(Security);

        Assert.Empty(project.ProjectReferences);
    }

    [Fact]
    public void Sessions_references_only_emulator_and_security()
    {
        var project = ProjectFile.Load(Sessions);

        Assert.All(project.ProjectReferences, reference => Assert.Contains(reference, (string[])[Emulator, Security], StringComparer.Ordinal));
    }

    [Fact]
    public void Agent_references_only_emulator_sessions_and_security()
    {
        var project = ProjectFile.Load(Agent);

        Assert.All(project.ProjectReferences, reference => Assert.Contains(reference, (string[])[Emulator, Sessions, Security], StringComparer.Ordinal));
    }

    [Fact]
    public void Controls_references_only_emulator()
    {
        var project = ProjectFile.Load(Controls);

        Assert.All(project.ProjectReferences, reference => Assert.Contains(reference, (string[])[Emulator], StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(Emulator)]
    [InlineData(Security)]
    [InlineData(Sessions)]
    [InlineData(Agent)]
    public void Projects_without_a_display_never_reference_avalonia(string name)
    {
        var project = ProjectFile.Load(name);

        Assert.DoesNotContain(project.PackageReferences, package => package.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
