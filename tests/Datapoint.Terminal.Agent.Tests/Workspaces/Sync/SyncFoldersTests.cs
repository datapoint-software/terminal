using Datapoint.Terminal.Agent.Workspaces.Sync;

namespace Datapoint.Terminal.Agent.Tests.Workspaces.Sync;

public sealed class SyncFoldersTests : IDisposable
{
    private static readonly (string Root, string Label)[] s_noVolumes = [];

    private readonly string _home = Directory.CreateTempSubdirectory("datapoint-sync-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string Folder(params string[] names) => Directory.CreateDirectory(Path.Combine([_home, .. names])).FullName;

    private IReadOnlyList<SyncFolder> Find(Func<string, string?>? environment = null, IEnumerable<(string, string)>? volumes = null) =>
        SyncFolders.Find(_home, environment ?? (_ => null), volumes ?? s_noVolumes);

    [Fact]
    public void Finds_nothing_in_an_empty_home() => Assert.Empty(Find());

    [Fact]
    public void Names_the_services_in_macOS_cloud_storage()
    {
        Folder("Library", "CloudStorage", "OneDrive-Example");
        Folder("Library", "CloudStorage", "GoogleDrive-someone@example.com");
        Folder("Library", "CloudStorage", "SomethingElse");

        var services = Find().Select(folder => folder.Service).Order(StringComparer.Ordinal);

        Assert.Equal(["Google Drive", "OneDrive", "SomethingElse"], services, StringComparer.Ordinal);
    }

    [Fact]
    public void Finds_iCloud_Drive_and_folders_in_home()
    {
        Folder("Library", "Mobile Documents");
        Folder("Dropbox");
        Folder("OneDrive - Example");

        Assert.Equal(["iCloud Drive", "Dropbox", "OneDrive"], Find().Select(folder => folder.Service), StringComparer.Ordinal);
    }

    [Fact]
    public void Reads_the_Dropbox_folders_from_its_settings()
    {
        var personal = Folder("Elsewhere", "Personal");
        var business = Folder("Elsewhere", "Work");
        var settings = Folder("AppData", "Dropbox");
        File.WriteAllText(Path.Combine(settings, "info.json"), "{\"personal\": {\"path\": " + Json(personal) + "}, \"business\": {\"path\": " + Json(business) + "}}");

        var found = Find(name => string.Equals(name, "APPDATA", StringComparison.Ordinal) ? Path.Combine(_home, "AppData") : null);

        Assert.Equal([personal, business], found.Select(folder => folder.Path), StringComparer.Ordinal);
    }

    [Fact]
    public void Ignores_unreadable_Dropbox_settings()
    {
        File.WriteAllText(Path.Combine(Folder(".dropbox"), "info.json"), "{ not json");

        Assert.Empty(Find());
    }

    [Fact]
    public void Finds_OneDrive_from_the_environment_and_Google_Drive_by_volume_label()
    {
        var oneDrive = Folder("Work OneDrive");
        var googleDrive = Folder("G");

        var found = Find(
            name => string.Equals(name, "OneDriveCommercial", StringComparison.Ordinal) ? oneDrive : null,
            [(googleDrive, "Google Drive"), (_home, "Local Disk")]);

        Assert.Equal([("OneDrive", oneDrive), ("Google Drive", googleDrive)], found.Select(folder => (folder.Service, folder.Path)));
    }

    [Fact]
    public void Finds_the_sync_folder_holding_a_workspace()
    {
        Folder("Dropbox", "code", "api");
        var folders = Find();

        Assert.Equal("Dropbox", SyncFolders.Containing(Path.Combine(_home, "Dropbox", "code", "api"), folders)?.Service);
        Assert.Null(SyncFolders.Containing(Folder("code", "api"), folders));
    }

    [Fact]
    public void Follows_links_to_a_sync_folder()
    {
        var storage = Folder("Library", "CloudStorage", "OneDrive-Example");
        Directory.CreateDirectory(Path.Combine(storage, "api"));
        var link = Path.Combine(_home, "OneDrive - Example");
        Directory.CreateSymbolicLink(link, storage);

        var found = SyncFolders.Containing(Path.Combine(link, "api"), [new SyncFolder("OneDrive", storage)]);

        Assert.Equal("OneDrive", found?.Service);
    }

    private static string Json(string text) => "\"" + System.Text.Json.JsonEncodedText.Encode(text) + "\"";
}
