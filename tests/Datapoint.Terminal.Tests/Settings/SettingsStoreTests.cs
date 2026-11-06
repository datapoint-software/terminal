using Datapoint.Terminal.Agent;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-settings-").FullName;

    private string FilePath => Path.Combine(_folder, "nested", "settings.json");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Starts_with_defaults_and_keeps_what_was_saved()
    {
        var store = new SettingsStore(FilePath);
        Assert.Empty(store.Load().Providers);

        var provider = new ProviderAccount { Id = "a1", Kind = ProviderKind.OpenAICompatible, Name = "Ollama", Model = "llama", Endpoint = "http://localhost:11434/v1", IsLocal = true };
        store.Save(new AppSettings { Providers = [provider], SelectedProviderId = "a1", DefaultApprovalMode = ApprovalMode.AskForRisky });
        var loaded = new SettingsStore(FilePath).Load();

        Assert.Equal(provider, Assert.Single(loaded.Providers));
        Assert.Equal(provider, loaded.SelectedProvider);
        Assert.Equal(ApprovalMode.AskForRisky, loaded.DefaultApprovalMode);
        Assert.Contains("\"openAICompatible\"", File.ReadAllText(FilePath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"selectedProvider\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Writes_a_file_only_the_user_can_read()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows protects the folder through its own permissions.");
            return;
        }

        new SettingsStore(FilePath).Save(new AppSettings());

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
    }

    [Fact]
    public void Keeps_an_unreadable_file_aside_instead_of_overwriting_it()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");
        var store = new SettingsStore(FilePath);

        var settings = store.Load();

        Assert.Empty(settings.Providers);
        Assert.NotNull(store.Problem);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(FilePath)!, "settings.json.unreadable-*"));
    }

    [Fact]
    public void Refuses_to_overwrite_settings_from_a_newer_version()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "version": 99, "providers": [] }""");
        var store = new SettingsStore(FilePath);
        store.Load();

        Assert.True(store.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => store.Save(new AppSettings()));
    }

    [Fact]
    public void Reads_a_file_from_before_projects_with_no_project_lists()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{\"version\": 1, \"providers\": []}");

        var settings = new SettingsStore(FilePath).Load();

        Assert.Empty(settings.RecentProjects);
        Assert.Empty(settings.OpenProjects);
    }
}
