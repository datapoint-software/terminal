using Datapoint.Terminal.Editor;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class DocumentViewModelTests : IDisposable
{
    private const string Notes = """
        # web1

        <!-- datapoint:agent:start overview -->
        Public web server.
        <!-- datapoint:agent:end overview -->

        - Sundays
        - Ask Rita

        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "document-" + Guid.NewGuid().ToString("N"));

    public DocumentViewModelTests() => Directory.CreateDirectory(_folder);

    private string FilePath => Path.Combine(_folder, "web1.md");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private DocumentViewModel Open(string text)
    {
        File.WriteAllText(FilePath, text);
        return new DocumentViewModel(MarkdownFile.Open(FilePath, out _)!);
    }

    private static BlockViewModel Shown(DocumentViewModel document, int index) => document.Blocks.Where(block => block.IsShown).ElementAt(index);

    [Fact]
    public void Shows_the_agents_markers_nowhere_and_keeps_them_exactly()
    {
        var document = Open(Notes);

        Assert.DoesNotContain(document.Blocks.Where(block => block.IsShown), block => block.Body.Contains("datapoint:agent", StringComparison.Ordinal));
        var overview = Shown(document, 1);
        document.Edit(overview);
        overview.Body = "Public web server for the shop.";
        document.OnBodyEdited(overview);
        document.Commit();

        Assert.Equal(Notes.Replace("Public web server.", "Public web server for the shop.", StringComparison.Ordinal), document.Text);
        Assert.True(document.Save());
        Assert.Equal(document.Text, File.ReadAllText(FilePath));
    }

    [Fact]
    public void Enter_continues_a_list_and_on_an_empty_item_ends_it()
    {
        var document = Open(Notes);
        var focus = new List<(BlockViewModel Block, int Caret)>();
        document.FocusRequested = (block, caret) => focus.Add((block, caret));
        var last = Shown(document, 3);
        document.Edit(last);

        document.Split(last, last.Body.Length);
        var item = focus[^1].Block;
        Assert.Equal("- ", item.Body);
        Assert.Equal(2, focus[^1].Caret);

        document.Split(item, 2);
        Assert.Equal(string.Empty, item.Body);
        Assert.Equal(BlockKind.Paragraph, item.Kind);

        item.Body = "After the list.";
        document.Commit();
        Assert.EndsWith("- Ask Rita\n\nAfter the list.\n", document.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Enter_in_a_paragraph_starts_a_new_one_and_backspace_joins_them_again()
    {
        var document = Open("One two\n");
        var paragraph = Shown(document, 0);
        document.Edit(paragraph);

        document.Split(paragraph, 3);
        Assert.Equal("One\n\ntwo\n", document.Text);

        document.JoinWithPrevious(Shown(document, 1));
        document.Commit();
        Assert.Equal("Onetwo\n", document.Text);
    }

    [Fact]
    public void Typed_markdown_becomes_a_heading_straight_away_and_blocks_on_commit()
    {
        var document = Open("Text\n");
        var block = Shown(document, 0);
        document.Edit(block);

        block.Body = "# Title";
        document.OnBodyEdited(block);
        Assert.Equal(BlockKind.Heading1, block.Kind);

        block.Body = "# Title\n\nBody";
        document.Commit();
        Assert.Equal<BlockKind>([BlockKind.Heading1, BlockKind.Paragraph], [.. document.Blocks.Where(item => item.IsShown).Select(item => item.Kind)]);
        Assert.Equal("# Title\n\nBody\n", document.Text);
    }

    [Fact]
    public void Starts_an_empty_document_and_saves_as_the_user_types()
    {
        var document = new DocumentViewModel(MarkdownFile.Open(Path.Combine(_folder, "hosts", "db1.md"), out _)!);
        var saves = 0;
        document.SaveDue = () => saves++;
        Assert.True(document.IsEmpty);

        document.AddAtEnd();
        var block = Shown(document, 0);
        block.Body = "# db1";
        document.OnBodyEdited(block);

        Assert.Equal(1, saves);
        Assert.True(document.IsDirty);
        Assert.True(document.Save());
        Assert.Equal("# db1\n", File.ReadAllText(Path.Combine(_folder, "hosts", "db1.md")));
    }

    [Fact]
    public void Reloads_quietly_without_changes_and_asks_with_them()
    {
        var document = Open("Mine\n");
        File.WriteAllText(FilePath, "Theirs\n");
        document.OnChangedOnDisk();
        Assert.False(document.HasConflict);
        Assert.Equal("Theirs\n", document.Text);

        var block = Shown(document, 0);
        document.Edit(block);
        block.Body = "Mine again";
        document.OnBodyEdited(block);
        File.WriteAllText(FilePath, "Theirs again\n");
        document.OnChangedOnDisk();
        Assert.True(document.HasConflict);
        Assert.False(document.Save());

        document.KeepMineCommand.Execute(null);
        Assert.False(document.HasConflict);
        Assert.Equal("Mine again\n", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Show_markdown_edits_the_whole_source()
    {
        var document = Open(Notes);
        document.IsSourceMode = true;
        Assert.Equal(Notes, document.SourceText);
        Assert.False(document.IsDirty);

        document.SourceText = Notes + "- Third\n";
        Assert.True(document.IsDirty);
        document.IsSourceMode = false;

        Assert.Equal("- Third", document.Blocks.Last(block => block.IsShown).Body);
        Assert.Equal(Notes + "- Third\n", document.Text);
    }
}
