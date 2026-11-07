using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Agent.Memory;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class MemoryPageTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("memory-page-").FullName;

    public MemoryPageTests()
    {
        MemoryFile.Add(Global, "Use doas, not sudo, on the OpenBSD hosts.");
        MemoryFile.Add(Global, "Prefer journalctl over reading files in /var/log.");
        MemoryFile.Add(Mine, "Restarts go through the load balancer drain script first.");
    }

    private string Global => Path.Combine(_folder, "app", MemoryFile.FileName);

    private string Mine => Path.Combine(_folder, "acme", MemoryFile.FileName);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Shows_both_memories_and_edits_deletes_and_clears_notes()
    {
        var page = new MemoryPageViewModel { Confirm = (_, _) => Task.FromResult(true) };
        page.Load(new MemoryPlaces(Global, "Acme Corp", Mine, CanWriteWorkspace: true));

        Assert.Equal(["Everywhere", "In Acme Corp"], page.Sections.Select(section => section.Title), StringComparer.Ordinal);
        var everywhere = page.Sections[0];
        everywhere.Notes[0].EditCommand.Execute(null);
        everywhere.Notes[0].EditText = "Use doas on OpenBSD.";
        everywhere.Notes[0].SaveCommand.Execute(null);
        everywhere.Notes[1].DeleteCommand.Execute(null);
        page.Sections[1].ClearCommand.Execute(null);

        Assert.Equal(["Use doas on OpenBSD."], MemoryFile.Notes(Global), StringComparer.Ordinal);
        Assert.Empty(MemoryFile.Notes(Mine));
        Assert.False(page.Sections[1].HasNotes);
    }

    [Fact]
    public void Leaves_memory_alone_unless_clearing_is_confirmed_or_allowed()
    {
        var page = new MemoryPageViewModel { Confirm = (_, _) => Task.FromResult(false) };
        page.Load(new MemoryPlaces(Global, "Acme Corp", Mine, CanWriteWorkspace: false));

        page.Sections[0].ClearCommand.Execute(null);
        page.Sections[1].Notes[0].DeleteCommand.Execute(null);

        Assert.Equal(2, MemoryFile.Notes(Global).Count);
        Assert.Single(MemoryFile.Notes(Mine));
        Assert.False(page.Sections[1].Notes[0].CanWrite);
    }

    [Fact]
    public void Shows_only_the_memory_for_everywhere_in_the_default_workspace()
    {
        var page = new MemoryPageViewModel();

        page.Load(new MemoryPlaces(Global, null, null, CanWriteWorkspace: true));

        Assert.Equal("Everywhere", Assert.Single(page.Sections).Title);
    }

    [Fact]
    public void Moves_the_memory_kept_in_the_old_global_folder_once()
    {
        var legacy = Path.Combine(_folder, "legacy");
        MemoryFile.Add(Path.Combine(legacy, MemoryFile.FileName), "Old note.");
        var appData = Path.Combine(_folder, "data");

        var path = MemoryLocation.Locate(appData, legacy);

        Assert.Equal(["Old note."], MemoryFile.Notes(path), StringComparer.Ordinal);
        Assert.False(File.Exists(Path.Combine(legacy, MemoryFile.FileName)));
    }

    [AvaloniaFact]
    public void Renders_the_page()
    {
        var window = new SettingsWindow { Memory = new MemoryPlaces(Global, "Acme Corp", Mine, CanWriteWorkspace: true) };
        window.Show();
        window.ShowPage(SettingsWindow.MemoryPageIndex);
        Dispatcher.UIThread.RunJobs();

        Directory.CreateDirectory("rendered");
        using var frame = window.CaptureRenderedFrame();
        frame?.Save("rendered/settings-memory.png", new PngBitmapEncoderOptions());

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).ToList();
        Assert.Contains("In Acme Corp", texts, StringComparer.Ordinal);
        Assert.Contains("Restarts go through the load balancer drain script first.", texts, StringComparer.Ordinal);
        window.Close();
    }
}
