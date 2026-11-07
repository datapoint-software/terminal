using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class DocumentViewTests : IDisposable
{
    private const string HostNotes = """
        # web1

        <!-- datapoint:agent:start overview -->
        ## Overview

        Public web server for the shop: nginx in front of three app containers, behind the load balancer.
        <!-- datapoint:agent:end overview -->

        ## My notes

        - Maintenance window is **Sundays**, 02:00–04:00.
        - Ask Rita before restarting `nginx`.
        - [ ] Rotate the logs
        - [x] Renew the certificate

        ```bash
        df -h
        ```

        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "document-view-" + Guid.NewGuid().ToString("N"));

    public DocumentViewTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private (Window Window, DocumentView View, DocumentViewModel Document) Show(string text)
    {
        var path = Path.Combine(_folder, "web1.md");
        File.WriteAllText(path, text);
        var document = new DocumentViewModel(MarkdownFile.Open(path, out _)!)
        {
            Host = new HostHeaderViewModel("web1", "production", "deploy@web1.example.com", () => { }) { OpenText = "1 open" },
        };
        var view = new DocumentView { DataContext = document };
        var window = new Window { Width = 820, Height = 760, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, document);
    }

    private static BlockViewModel Shown(DocumentViewModel document, int index) => document.Blocks.Where(block => block.IsShown).ElementAt(index);

    private static TextBox EditingBox(DocumentView view) => view.GetVisualDescendants().OfType<TextBox>().Single(box => box.IsEffectivelyVisible && box.Classes.Contains("block"));

    private static void Save(Window window, string name)
    {
        Directory.CreateDirectory("rendered");
        using var frame = window.CaptureRenderedFrame();
        frame?.Save($"rendered/{name}.png", new PngBitmapEncoderOptions());
    }

    [AvaloniaFact]
    public void Renders_the_document_and_the_block_being_edited_as_markdown()
    {
        var (window, view, document) = Show(HostNotes);
        Save(window, "document");

        document.Edit(Shown(document, 4), 25);
        Dispatcher.UIThread.RunJobs();
        Save(window, "document-editing");

        var box = EditingBox(view);
        Assert.Equal("- Maintenance window is **Sundays**, 02:00–04:00.", box.Text);
        Assert.True(box.IsFocused);
        Assert.Equal(25, box.CaretIndex);
        Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text?.Contains("datapoint:agent", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public void A_click_on_rendered_text_edits_it_there()
    {
        var (window, view, document) = Show("Ask **Rita** first.\n");
        var rendered = view.GetVisualDescendants().OfType<MarkdownView>().First(item => item.IsEffectivelyVisible);
        var point = rendered.TranslatePoint(new Point(1, 10), window)!.Value;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Shown(document, 0), document.EditingBlock);
        Assert.Equal("Ask **Rita** first.", EditingBox(view).Text);
    }

    [AvaloniaFact]
    public void Enter_and_backspace_split_and_join_blocks()
    {
        var (window, view, document) = Show("- Sundays\n");
        document.Edit(Shown(document, 0));
        Dispatcher.UIThread.RunJobs();

        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("Mondays");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("- Sundays\n- Mondays\n", document.Text);

        var box = EditingBox(view);
        box.CaretIndex = 0;
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("- SundaysMondays\n", document.Text);
    }

    [AvaloniaFact]
    public void Formats_the_selection_and_shows_the_whole_source()
    {
        var (window, view, document) = Show("Ask Rita first.\n");
        document.Edit(Shown(document, 0));
        Dispatcher.UIThread.RunJobs();
        var box = EditingBox(view);
        box.SelectionStart = 4;
        box.SelectionEnd = 8;

        Assert.True(view.Format("**"));
        Assert.Equal("Ask **Rita** first.\n", document.Text);

        document.IsSourceMode = true;
        Dispatcher.UIThread.RunJobs();
        Save(window, "document-source");
        Assert.Equal("Ask **Rita** first.\n", view.GetVisualDescendants().OfType<TextBox>().Single(item => item.IsEffectivelyVisible).Text);
    }

    [AvaloniaFact]
    public void Shows_a_conflict_with_both_choices()
    {
        var (window, _, document) = Show("Mine\n");
        var block = Shown(document, 0);
        document.Edit(block);
        block.Body = "Mine, edited";
        document.OnBodyEdited(block);
        File.WriteAllText(document.Path, "Theirs\n");
        document.OnChangedOnDisk();
        Dispatcher.UIThread.RunJobs();
        Save(window, "document-conflict");

        var buttons = window.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content as string).ToList();
        Assert.Contains("Keep my version", buttons, StringComparer.Ordinal);
        Assert.Contains("Use the version on disk", buttons, StringComparer.Ordinal);
    }
}
